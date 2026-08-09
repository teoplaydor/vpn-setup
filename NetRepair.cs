using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace NetRepair
{
    internal static class Program
    {
        private const string Version = "2.1";

        private static StreamWriter _log;
        private static readonly List<string> Done = new List<string>();
        private static readonly List<string> Failed = new List<string>();
        private static readonly List<string> Culprits = new List<string>();
        private static readonly List<string> State = new List<string>();
        private static bool _dryRun;
        private static bool _undo;

        // Признаки VPN/прокси/DPI-софта. Совпадение ищется в имени службы,
        // отображаемом имени, имени процесса и пути к файлу.
        private static readonly string[] Marks =
        {
            "warp-svc", "cloudflare", "amnezia", "wireguard", "openvpn", "nordvpn", "expressvpn",
            "protonvpn", "surfshark", "windscribe", "tunnelbear", "psiphon", "outline",
            "tailscale", "zerotier", "hamachi", "softether", "sing-box", "singbox", "xray",
            "v2ray", "clash", "mihomo", "hysteria", "shadowsocks", "proxifier", "netfilter",
            "windivert", "tun2socks", "furious", "nekoray", "nekobox", "hiddify", "zapret",
            "goodbyedpi", "winws", "naiveproxy", "trojan-go", "wintun", "tap-windows", "tapinstall"
        };

        // Системные компоненты, которые нельзя трогать, даже если имя похоже.
        private static readonly string[] Whitelist =
        {
            "mpssvc", "bfe", "policyagent", "ikeext", "rasman", "remoteaccess", "sstpsvc",
            "wlansvc", "dhcp", "dnscache", "nsi", "netprofm", "nlasvc", "winhttpautoproxysvc",
            "windefend", "wdnissvc", "wscsvc", "netman", "netsetupsvc", "wcmsvc",
            // WarpJITSvc - системный растеризатор графики Windows, к Cloudflare
            // отношения не имеет; его отключение ломает отрисовку.
            "warpjitsvc", "wudfsvc", "wdiservicehost"
        };

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "NetRepair " + Version + " - восстановление сети Windows";

            foreach (string a in args)
            {
                string s = a.ToLowerInvariant();
                if (s == "/dry" || s == "-dry" || s == "--dry") _dryRun = true;
                if (s == "/undo" || s == "-undo" || s == "--undo") _undo = true;
            }

            if (!IsAdmin())
            {
                if (RelaunchAsAdmin(args)) return 0;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("  Нужны права администратора: правой кнопкой -> \"Запуск от имени администратора\".");
                Console.ResetColor();
                Pause();
                return 1;
            }

            OpenLog();

            if (_undo)
            {
                RunUndo();
                CloseLog();
                Pause();
                return 0;
            }

            Banner();

            if (!_dryRun && !Confirm()) { Say("Отменено.", ConsoleColor.Yellow); CloseLog(); Pause(); return 0; }

            try
            {
                Step_Snapshot();
                Step_KillProcesses();
                Step_DisableServices();
                Step_DisableDrivers();
                Step_DisableAdapters();
                Step_CleanAutorun();
                Step_ResetProxy();
                Step_ResetFirewall();
                Step_ResetStack();
                Step_ResetRoutes();
                Step_ResetDns();
                Step_FlushCaches();
                Step_RenewDhcp();
                Step_CheckHosts();
                Step_DisableFastStartup();
                Step_Verify();
            }
            catch (Exception ex)
            {
                Fail("Непредвиденная ошибка: " + ex.Message);
            }

            Summary();
            CloseLog();
            Pause();
            return Failed.Count == 0 ? 0 : 2;
        }

        // ================================================================ шаги

        private static void Step_Snapshot()
        {
            Header("1/16  Снимок состояния (в лог)");
            Capture("ipconfig", "/all");
            Capture("route", "print");
            Capture("netsh", "advfirewall show allprofiles state");
            Capture("netsh", "winhttp show proxy");
            Ok("Состояние записано");
        }

        private static void Step_KillProcesses()
        {
            Header("2/16  Остановка процессов VPN/прокси-клиентов");
            int killed = 0;
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch (Exception ex) { Fail("Список процессов: " + ex.Message); return; }

            foreach (Process p in all)
            {
                string name;
                try { name = p.ProcessName; } catch { continue; }
                if (!Matches(name)) continue;
                if (IsWhitelisted(name)) continue;

                Log("  процесс: " + name + " (PID " + p.Id + ")");
                Remember("процесс " + name);
                if (_dryRun) { killed++; continue; }
                try { p.Kill(); p.WaitForExit(5000); killed++; }
                catch (Exception ex) { Log("    не удалось: " + ex.Message); }
            }

            Ok(killed > 0
                ? (_dryRun ? "Найдено процессов: " : "Завершено процессов: ") + killed
                : "Посторонних процессов нет");
        }

        private static void Step_DisableServices()
        {
            Header("3/16  Службы VPN/прокси: остановка и снятие с автозапуска");
            // Именно здесь живут Cloudflare WARP и AmneziaVPN: как службы, а не
            // как процессы с узнаваемым именем.
            int hit = 0;
            ServiceController[] svcs;
            try { svcs = ServiceController.GetServices(); }
            catch (Exception ex) { Fail("Список служб: " + ex.Message); return; }

            foreach (ServiceController sc in svcs)
            {
                string sn, dn;
                try { sn = sc.ServiceName; dn = sc.DisplayName; } catch { continue; }
                if (IsWhitelisted(sn)) continue;
                if (!Matches(sn) && !Matches(dn)) continue;

                Log("  служба: " + sn + "  (" + dn + ")");
                Remember("служба " + sn + " (" + dn + ")");
                hit++;
                if (_dryRun) continue;

                try
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                        Log("    остановлена");
                    }
                }
                catch (Exception ex) { Log("    остановка: " + ex.Message); }

                State.Add("service\t" + sn + "\t" + GetStartType(sn));
                Run("sc", "config \"" + sn + "\" start= disabled", 20000);
            }

            Ok(hit > 0 ? "Обработано служб: " + hit : "Посторонних служб нет");
        }

        private static void Step_DisableDrivers()
        {
            Header("4/16  Драйверы-перехватчики: остановка и снятие с автозапуска");
            int hit = 0;
            string outp = Run("sc", "query type= driver state= all", 60000);
            foreach (string line in outp.Split('\n'))
            {
                string t = line.Trim();
                if (!t.StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase) &&
                    !t.StartsWith("Имя_службы:", StringComparison.OrdinalIgnoreCase)) continue;

                int c = t.IndexOf(':');
                if (c < 0) continue;
                string name = t.Substring(c + 1).Trim();
                if (name.Length == 0) continue;
                if (IsWhitelisted(name)) continue;
                if (!Matches(name)) continue;

                Log("  драйвер: " + name);
                Remember("драйвер " + name);
                hit++;
                if (_dryRun) continue;

                State.Add("driver\t" + name + "\t" + GetStartType(name));
                Run("sc", "stop \"" + name + "\"", 20000);
                Run("sc", "config \"" + name + "\" start= disabled", 20000);
            }
            Ok(hit > 0 ? "Обработано драйверов: " + hit : "Драйверов-перехватчиков нет");
        }

        private static void Step_DisableAdapters()
        {
            Header("5/16  Отключение виртуальных сетевых адаптеров");
            int hit = 0;
            string outp = Run("netsh", "interface show interface", 30000);
            foreach (string line in outp.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length == 0) continue;
                string[] p = t.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 4) continue;
                string name = p[3].Trim();
                if (name.Length == 0) continue;
                if (!Matches(name)) continue;

                Log("  адаптер: " + name);
                Remember("адаптер " + name);
                hit++;
                if (_dryRun) continue;
                State.Add("adapter\t" + name + "\tenabled");
                Run("netsh", "interface set interface name=\"" + name + "\" admin=disable", 30000);
            }
            Ok(hit > 0 ? "Отключено адаптеров: " + hit : "Лишних адаптеров нет");
        }

        private static void Step_CleanAutorun()
        {
            Header("6/16  Очистка автозапуска");
            int hit = 0;
            hit += CleanRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
            hit += CleanRunKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            hit += CleanRunKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run");
            Ok(hit > 0 ? "Убрано из автозапуска: " + hit : "Автозапуск чист");
        }

        private static int CleanRunKey(RegistryKey root, string path)
        {
            int n = 0;
            try
            {
                using (RegistryKey k = root.OpenSubKey(path, !_dryRun))
                {
                    if (k == null) return 0;
                    foreach (string v in k.GetValueNames())
                    {
                        object val = k.GetValue(v);
                        string data = val == null ? "" : val.ToString();
                        if (!Matches(v) && !Matches(data)) continue;

                        Log("  автозапуск: " + v + " = " + data);
                        Remember("автозапуск " + v);
                        n++;
                        if (_dryRun) continue;
                        try { k.DeleteValue(v, false); } catch (Exception ex) { Log("    " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { Log("  ключ " + path + ": " + ex.Message); }
            return n;
        }

        private static void Step_ResetProxy()
        {
            Header("7/16  Сброс системного прокси");
            const string inet = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
            SetRegDword(Registry.CurrentUser, inet, "ProxyEnable", 0);
            DeleteRegValue(Registry.CurrentUser, inet, "ProxyServer");
            DeleteRegValue(Registry.CurrentUser, inet, "AutoConfigURL");
            DeleteRegValue(Registry.CurrentUser, inet, "ProxyOverride");
            Run("netsh", "winhttp reset proxy", 20000);
            Ok("Прокси отключён");
        }

        private static void Step_ResetFirewall()
        {
            Header("8/16  Сброс правил брандмауэра к заводским");
            Run("netsh", "advfirewall reset", 60000);
            Run("netsh", "advfirewall set allprofiles state on", 30000);
            Ok("Правила сброшены, защита включена");
        }

        private static void Step_ResetStack()
        {
            Header("9/16  Пересоздание Winsock и стека TCP/IP");
            Run("netsh", "winsock reset", 60000);
            Run("netsh", "winsock reset catalog", 60000);
            Run("netsh", "int ip reset", 60000);
            Run("netsh", "int ipv4 reset", 60000);
            Run("netsh", "int ipv6 reset", 60000);
            Ok("Стек помечен к пересозданию (нужна перезагрузка)");
        }

        private static void Step_ResetRoutes()
        {
            Header("10/16  Очистка таблицы маршрутов");
            Run("route", "-f", 30000);
            Ok("Маршруты очищены");
        }

        private static void Step_ResetDns()
        {
            Header("11/16  DNS на автоматическое получение");
            foreach (string iface in GetIpv4Interfaces())
            {
                Log("  интерфейс: " + iface);
                Run("netsh", "interface ipv4 set dnsservers name=\"" + iface + "\" source=dhcp validate=no", 30000);
                Run("netsh", "interface ipv6 set dnsservers name=\"" + iface + "\" source=dhcp validate=no", 30000);
            }
            Ok("DNS возвращён роутеру");
        }

        private static void Step_FlushCaches()
        {
            Header("12/16  Очистка кэшей DNS, ARP, NetBIOS");
            Run("ipconfig", "/flushdns", 30000);
            Run("arp", "-d *", 30000);
            Run("nbtstat", "-R", 30000);
            Run("nbtstat", "-RR", 30000);
            Ok("Кэши очищены");
        }

        private static void Step_RenewDhcp()
        {
            Header("13/16  Переполучение адреса от роутера");
            Run("ipconfig", "/release", 60000);
            Run("ipconfig", "/renew", 90000);
            Run("ipconfig", "/registerdns", 60000);
            Ok("Адрес получен заново");
        }

        private static void Step_CheckHosts()
        {
            Header("14/16  Проверка файла hosts");
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            try
            {
                if (!File.Exists(path)) { Ok("Файл hosts отсутствует"); return; }

                var bad = new List<string>();
                foreach (string l in File.ReadAllLines(path))
                {
                    string t = l.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    if (t.StartsWith("127.0.0.1") && t.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) > 0) continue;
                    if (t.StartsWith("::1") && t.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) > 0) continue;
                    bad.Add(t);
                }

                if (bad.Count == 0) { Ok("Файл hosts в порядке"); return; }

                foreach (string b in bad) Log("  запись: " + b);
                if (_dryRun) { Ok("Посторонних записей: " + bad.Count + " (будут убраны с копией рядом)"); return; }

                string backup = path + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(path, backup, true);
                File.WriteAllText(path,
                    "# Restored by NetRepair\r\n127.0.0.1       localhost\r\n::1             localhost\r\n");
                Ok("Убрано записей: " + bad.Count + ", копия: " + Path.GetFileName(backup));
            }
            catch (Exception ex) { Fail("hosts: " + ex.Message); }
        }

        private static void Step_DisableFastStartup()
        {
            Header("15/16  Отключение \"Быстрого запуска\"");
            if (!_dryRun) State.Add("faststartup\tHiberbootEnabled\t" + GetFastStartup());
            // Из-за него состояние ядра поднимается из гибернации: после
            // "Завершения работы" сеть жива, после "Перезагрузки" - снова сломана.
            SetRegDword(Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0);
            Ok("Быстрый запуск отключён - загрузка станет предсказуемой");
        }

        private static void Step_Verify()
        {
            Header("16/16  Проверка связи");
            if (_dryRun) { Ok("В рабочем режиме здесь проверка шлюза, интернета и DNS"); return; }

            string gw = GetDefaultGateway();
            if (!string.IsNullOrEmpty(gw))
            {
                string r = Run("ping", "-n 2 " + gw, 20000);
                if (r.IndexOf("TTL=", StringComparison.OrdinalIgnoreCase) >= 0) Ok("Роутер отвечает (" + gw + ")");
                else Fail("Роутер не отвечает");
            }
            else Fail("Шлюз по умолчанию не найден");

            string r2 = Run("ping", "-n 2 8.8.8.8", 20000);
            if (r2.IndexOf("TTL=", StringComparison.OrdinalIgnoreCase) >= 0) Ok("Интернет отвечает");
            else Fail("Интернет не отвечает - перезагрузите компьютер");

            string r3 = Run("nslookup", "ya.ru", 20000);
            if (r3.IndexOf("Address", StringComparison.OrdinalIgnoreCase) >= 0) Ok("DNS работает");
            else Fail("DNS не отвечает - перезагрузите компьютер");
        }

        // ============================================================= утилиты

        // Тип запуска службы или драйвера: boot|system|auto|demand|disabled.
        private static string GetStartType(string name)
        {
            try
            {
                string o = Run("sc", "qc \"" + name + "\"", 15000);
                foreach (string line in o.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.IndexOf("START_TYPE", StringComparison.OrdinalIgnoreCase) < 0 &&
                        t.IndexOf("Тип_запуска", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (t.IndexOf("BOOT_START", StringComparison.OrdinalIgnoreCase) >= 0) return "boot";
                    if (t.IndexOf("SYSTEM_START", StringComparison.OrdinalIgnoreCase) >= 0) return "system";
                    if (t.IndexOf("AUTO_START", StringComparison.OrdinalIgnoreCase) >= 0) return "auto";
                    if (t.IndexOf("DEMAND_START", StringComparison.OrdinalIgnoreCase) >= 0) return "demand";
                    if (t.IndexOf("DISABLED", StringComparison.OrdinalIgnoreCase) >= 0) return "disabled";
                }
            }
            catch (Exception ex) { Log("  тип запуска " + name + ": " + ex.Message); }
            return "demand"; // безопасный вариант: запуск по требованию
        }

        private static string GetFastStartup()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Power"))
                {
                    if (k == null) return "1";
                    object v = k.GetValue("HiberbootEnabled");
                    return v == null ? "1" : v.ToString();
                }
            }
            catch { return "1"; }
        }

        // ================================================================ откат

        private static void RunUndo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  ============================================================");
            Console.WriteLine("     NetRepair " + Version + " - ОТКАТ изменений");
            Console.WriteLine("  ============================================================");
            Console.ResetColor();
            Console.WriteLine();

            string sp = StatePath();
            if (File.Exists(sp))
            {
                Console.WriteLine("  Найден файл состояния, восстанавливаю точно как было.");
                Console.WriteLine();
                foreach (string line in File.ReadAllLines(sp))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 3) continue;
                    RestoreOne(p[0], p[1], p[2]);
                }
                try { File.Delete(sp); } catch { }
            }
            else
            {
                Console.WriteLine("  Файла состояния нет - восстанавливаю по типовым значениям.");
                Console.WriteLine();
                // Драйверы виртуальных адаптеров: запуск по требованию.
                foreach (string d in new[] { "wintun", "tap0901", "wg", "Wintun" })
                    RestoreOne("driver", d, "demand");
                // Адаптеры включаем обратно.
                foreach (string a in GetMatchingAdapters())
                    RestoreOne("adapter", a, "enabled");
                RestoreOne("faststartup", "HiberbootEnabled", "1");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Откат завершён. Запустите клиент от администратора и включите TUN.");
            Console.ResetColor();
        }

        private static void RestoreOne(string kind, string name, string value)
        {
            switch (kind)
            {
                case "service":
                case "driver":
                    string o = Run("sc", "qc \"" + name + "\"", 15000);
                    if (o.IndexOf("1060", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("    - " + name + ": не установлен, пропуск");
                        return;
                    }
                    Run("sc", "config \"" + name + "\" start= " + value, 20000);
                    Console.WriteLine("    - " + kind + " " + name + " -> " + value);
                    break;

                case "adapter":
                    Run("netsh", "interface set interface name=\"" + name + "\" admin=enable", 30000);
                    Console.WriteLine("    - адаптер " + name + " -> включён");
                    break;

                case "faststartup":
                    int v;
                    if (!int.TryParse(value, out v)) v = 1;
                    SetRegDword(Registry.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", v);
                    Console.WriteLine("    - быстрый запуск -> " + (v == 1 ? "включён" : "выключен"));
                    break;
            }
        }

        private static List<string> GetMatchingAdapters()
        {
            var list = new List<string>();
            string outp = Run("netsh", "interface show interface", 30000);
            foreach (string line in outp.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length == 0) continue;
                string[] p = t.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 4) continue;
                string name = p[3].Trim();
                if (Matches(name) && !list.Contains(name)) list.Add(name);
            }
            return list;
        }

        private static string StatePath()
        {
            string dir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            return Path.Combine(dir, "NetRepair.state.txt");
        }

        private static void SaveState()
        {
            if (_dryRun || State.Count == 0) return;
            try { File.WriteAllLines(StatePath(), State.ToArray(), Encoding.UTF8); }
            catch (Exception ex) { Log("Сохранение состояния: " + ex.Message); }
        }

        private static bool Matches(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string l = s.ToLowerInvariant();
            foreach (string m in Marks) if (l.Contains(m)) return true;
            return false;
        }

        private static bool IsWhitelisted(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string l = s.ToLowerInvariant();
            foreach (string w in Whitelist) if (l == w) return true;
            return false;
        }

        private static void Remember(string s) { if (!Culprits.Contains(s)) Culprits.Add(s); }

        private static List<string> GetIpv4Interfaces()
        {
            var list = new List<string>();
            string outp = Run("netsh", "interface ipv4 show interfaces", 30000);
            foreach (string line in outp.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length == 0) continue;
                string[] parts = t.Split(new[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                int idx;
                if (!int.TryParse(parts[0], out idx)) continue;
                string name = parts[4].Trim();
                if (name.Length == 0) continue;
                if (name.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!list.Contains(name)) list.Add(name);
            }
            return list;
        }

        private static string GetDefaultGateway()
        {
            string outp = Run("route", "print 0.0.0.0", 20000);
            foreach (string line in outp.Split('\n'))
            {
                string t = line.Trim();
                if (!t.StartsWith("0.0.0.0")) continue;
                string[] p = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3 && p[2].IndexOf('.') > 0 && p[2] != "0.0.0.0") return p[2];
            }
            return null;
        }

        private static string Run(string exe, string args, int timeoutMs)
        {
            Log("  > " + exe + " " + args);
            if (_dryRun && !IsReadOnly(exe, args)) return string.Empty;

            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.GetEncoding(866),
                    StandardErrorEncoding = Encoding.GetEncoding(866)
                };

                using (var p = Process.Start(psi))
                {
                    string so = p.StandardOutput.ReadToEnd();
                    string se = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        Log("    (превышено время ожидания)");
                        return string.Empty;
                    }
                    string all = so + se;
                    foreach (string l in all.Split('\n'))
                        if (l.Trim().Length > 0) Log("    " + l.TrimEnd());
                    return all;
                }
            }
            catch (Exception ex)
            {
                Log("    ошибка запуска: " + ex.Message);
                return string.Empty;
            }
        }

        // В режиме просмотра читающие команды выполнять можно и нужно:
        // без них нечего показывать.
        private static bool IsReadOnly(string exe, string args)
        {
            string a = (args ?? "").ToLowerInvariant();
            string e = (exe ?? "").ToLowerInvariant();
            if (e == "ipconfig" && a.Contains("/all")) return true;
            if (e == "route" && a.StartsWith("print")) return true;
            if (e == "netsh" && a.Contains("show")) return true;
            if (e == "sc" && a.StartsWith("query")) return true;
            return false;
        }

        private static void Capture(string exe, string args) { Run(exe, args, 45000); }

        private static void SetRegDword(RegistryKey root, string path, string name, int value)
        {
            try
            {
                if (_dryRun) { Log("  > reg: " + path + "\\" + name + " = " + value); return; }
                using (RegistryKey k = root.CreateSubKey(path))
                {
                    if (k == null) { Log("  ветка недоступна: " + path); return; }
                    k.SetValue(name, value, RegistryValueKind.DWord);
                    Log("  установлено " + name + " = " + value);
                }
            }
            catch (Exception ex) { Log("  реестр (" + name + "): " + ex.Message); }
        }

        private static void DeleteRegValue(RegistryKey root, string path, string name)
        {
            try
            {
                if (_dryRun) { Log("  > reg delete: " + path + "\\" + name); return; }
                using (RegistryKey k = root.OpenSubKey(path, true))
                {
                    if (k == null || k.GetValue(name) == null) return;
                    k.DeleteValue(name, false);
                    Log("  удалено " + name);
                }
            }
            catch (Exception ex) { Log("  реестр (" + name + "): " + ex.Message); }
        }

        private static bool IsAdmin()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static bool RelaunchAsAdmin(string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        // ================================================================== UI

        private static void Banner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  ============================================================");
            Console.WriteLine("     NetRepair " + Version + " - полное восстановление сети Windows");
            Console.WriteLine("  ============================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Будет сделано:");
            Console.WriteLine("    - завершены процессы VPN и прокси-клиентов");
            Console.WriteLine("    - остановлены их СЛУЖБЫ и сняты с автозапуска");
            Console.WriteLine("    - остановлены драйверы-перехватчики трафика");
            Console.WriteLine("    - отключены виртуальные сетевые адаптеры");
            Console.WriteLine("    - вычищен автозапуск");
            Console.WriteLine("    - сброшены прокси, брандмауэр, Winsock и стек TCP/IP");
            Console.WriteLine("    - очищены маршруты и кэши, обновлён адрес");
            Console.WriteLine("    - отключён \"Быстрый запуск\" (из-за него лечение слетало)");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Пароли Wi-Fi не затрагиваются. В конце нужна ПЕРЕЗАГРУЗКА.");
            Console.ResetColor();
            if (_dryRun)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine();
                Console.WriteLine("  РЕЖИМ ПРОСМОТРА (/dry): только показывает, ничего не меняет.");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        private static bool Confirm()
        {
            Console.Write("  Начать? [Y/N]: ");
            string s = Console.ReadLine();
            if (s == null) return false;
            s = s.Trim().ToLowerInvariant();
            return s == "y" || s == "yes" || s == "д" || s == "да" || s == "";
        }

        private static void Header(string t)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  " + t);
            Console.ResetColor();
            Log("");
            Log("=== " + t + " ===");
        }

        private static void Ok(string t)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("      [ok] " + t);
            Console.ResetColor();
            Done.Add(t);
            Log("[ok] " + t);
        }

        private static void Fail(string t)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("      [!!] " + t);
            Console.ResetColor();
            Failed.Add(t);
            Log("[!!] " + t);
        }

        private static void Say(string t, ConsoleColor c)
        {
            Console.ForegroundColor = c;
            Console.WriteLine("  " + t);
            Console.ResetColor();
            Log(t);
        }

        private static void Summary()
        {
            SaveState();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ============================================================");
            Console.WriteLine("     Готово. Шагов успешно: " + Done.Count + ", с замечаниями: " + Failed.Count);
            Console.WriteLine("  ============================================================");
            Console.ResetColor();

            if (Culprits.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Найдено и обезврежено:");
                Console.ResetColor();
                foreach (string c in Culprits) Console.WriteLine("    - " + c);
                Console.WriteLine();
                Console.WriteLine("  Это и есть источник проблемы. Чтобы не вернулось - удалите");
                Console.WriteLine("  эти программы через \"Программы и компоненты\" (appwiz.cpl).");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Если VPN-клиент нужен и TUN-режим перестал включаться -");
                Console.WriteLine("  верните всё командой:  NetRepair.exe /undo");
                Console.ResetColor();
            }

            if (Failed.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Замечания:");
                foreach (string f in Failed) Console.WriteLine("    - " + f);
            }

            Console.WriteLine();
            Console.WriteLine("  Отчёт: " + LogPath());

            if (_dryRun) return;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ВАЖНО: перезагрузка обязательна - без неё сброс стека не применится.");
            Console.ResetColor();
            Console.Write("  Перезагрузить сейчас? [Y/N]: ");
            string s = Console.ReadLine();
            if (s == null) return;
            s = s.Trim().ToLowerInvariant();
            if (s == "y" || s == "yes" || s == "д" || s == "да")
            {
                Run("shutdown", "/r /t 5 /c \"NetRepair: применение изменений\"", 15000);
                Console.WriteLine("  Перезагрузка через 5 секунд...");
            }
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.Write("  Нажмите Enter для выхода...");
            try { Console.ReadLine(); } catch { }
        }

        // ================================================================= лог

        private static string LogPath()
        {
            string dir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            return Path.Combine(dir, "NetRepair.log");
        }

        private static void OpenLog()
        {
            try
            {
                _log = new StreamWriter(LogPath(), false, Encoding.UTF8) { AutoFlush = true };
                _log.WriteLine("NetRepair " + Version + ", запуск " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                _log.WriteLine("Windows: " + Environment.OSVersion);
                _log.WriteLine(new string('-', 60));
            }
            catch { _log = null; }
        }

        private static void Log(string s)
        {
            if (_log == null) return;
            try { _log.WriteLine(s); } catch { }
        }

        private static void CloseLog()
        {
            if (_log == null) return;
            try { _log.WriteLine(new string('-', 60)); _log.WriteLine("Завершено " + DateTime.Now); _log.Dispose(); }
            catch { }
            _log = null;
        }
    }
}
