using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace V2raySetup
{
    internal static class Program
    {
        private const string Version = "1.0";
        private const string Api = "https://api.github.com/repos/2dust/v2rayN/releases/latest";
        private const string AssetName = "v2rayN-windows-64.zip";

        // Эталонный конфиг v2rayN со всеми секциями. Создаётся программой
        // только при корректном выходе, поэтому пишем его сами.
        private const string ConfigTemplate =
            "ew0KICAiSW5kZXhJZCI6ICJAQElOREVYSURAQCIsDQogICJTdWJJbmRleElkIjogbnVsbCwNCiAgIkNvcmVCYXNpY0l0ZW0iOiB7" +
            "DQogICAgIkxvZ0VuYWJsZWQiOiBmYWxzZSwNCiAgICAiTG9nbGV2ZWwiOiAid2FybmluZyIsDQogICAgIkRlZkZpbmdlcnByaW50" +
            "IjogbnVsbCwNCiAgICAiRGVmVXNlckFnZW50IjogbnVsbCwNCiAgICAiU2VuZFRocm91Z2giOiAiIiwNCiAgICAiQmluZEludGVy" +
            "ZmFjZSI6ICIiLA0KICAgICJFbmFibGVGcmFnbWVudCI6IGZhbHNlLA0KICAgICJFbmFibGVGaW5hbEZyYWdtZW50IjogZmFsc2Us" +
            "DQogICAgIkVuYWJsZUNhY2hlRmlsZTRTYm94IjogdHJ1ZQ0KICB9LA0KICAiVHVuTW9kZUl0ZW0iOiB7DQogICAgIkVuYWJsZVR1" +
            "biI6IEBAVFVOQEAsDQogICAgIkF1dG9Sb3V0ZSI6IHRydWUsDQogICAgIlN0cmljdFJvdXRlIjogZmFsc2UsDQogICAgIlN0YWNr" +
            "IjogImd2aXNvciIsDQogICAgIk10dSI6IDkwMDAsDQogICAgIkVuYWJsZUlQdjZBZGRyZXNzIjogdHJ1ZSwNCiAgICAiSWNtcFJv" +
            "dXRpbmciOiAicnVsZSIsDQogICAgIkVuYWJsZUxlZ2FjeVByb3RlY3QiOiBmYWxzZSwNCiAgICAiUm91dGVFeGNsdWRlQWRkcmVz" +
            "cyI6IG51bGwsDQogICAgIklQdjRBZGRyZXNzIjogbnVsbCwNCiAgICAiSVB2NkFkZHJlc3MiOiBudWxsDQogIH0sDQogICJLY3BJ" +
            "dGVtIjogew0KICAgICJNdHUiOiAxMzUwLA0KICAgICJUdGkiOiA1MCwNCiAgICAiVXBsaW5rQ2FwYWNpdHkiOiAxMiwNCiAgICAi" +
            "RG93bmxpbmtDYXBhY2l0eSI6IDEwMCwNCiAgICAiQ3duZE11bHRpcGxpZXIiOiAxLA0KICAgICJNYXhTZW5kaW5nV2luZG93Ijog" +
            "MjA5NzE1Mg0KICB9LA0KICAiR3JwY0l0ZW0iOiB7DQogICAgIklkbGVUaW1lb3V0IjogNjAsDQogICAgIkhlYWx0aENoZWNrVGlt" +
            "ZW91dCI6IDIwLA0KICAgICJQZXJtaXRXaXRob3V0U3RyZWFtIjogZmFsc2UsDQogICAgIkluaXRpYWxXaW5kb3dzU2l6ZSI6IDAN" +
            "CiAgfSwNCiAgIlJvdXRpbmdCYXNpY0l0ZW0iOiB7DQogICAgIkRvbWFpblN0cmF0ZWd5IjogIklQT25EZW1hbmQiLA0KICAgICJE" +
            "b21haW5TdHJhdGVneTRTaW5nYm94IjogbnVsbCwNCiAgICAiUm91dGluZ0luZGV4SWQiOiAiQEBST1VURUlEQEAiDQogIH0sDQog" +
            "ICJHdWlJdGVtIjogew0KICAgICJBdXRvUnVuIjogZmFsc2UsDQogICAgIkVuYWJsZVN0YXRpc3RpY3MiOiBmYWxzZSwNCiAgICAi" +
            "RGlzcGxheVJlYWxUaW1lU3BlZWQiOiBmYWxzZSwNCiAgICAiS2VlcE9sZGVyRGVkdXBsIjogZmFsc2UsDQogICAgIkF1dG9VcGRh" +
            "dGVJbnRlcnZhbCI6IDAsDQogICAgIlRyYXlNZW51U2VydmVyc0xpbWl0IjogMjAsDQogICAgIkVuYWJsZUhXQSI6IGZhbHNlLA0K" +
            "ICAgICJFbmFibGVMb2ciOiB0cnVlLA0KICAgICJSb290Q2VydFByb3ZpZGVyIjogInN5c3RlbSINCiAgfSwNCiAgIk1zZ1VJSXRl" +
            "bSI6IHsNCiAgICAiTWFpbk1zZ0ZpbHRlciI6ICIiLA0KICAgICJBdXRvUmVmcmVzaCI6IHRydWUNCiAgfSwNCiAgIlVpSXRlbSI6" +
            "IHsNCiAgICAiRW5hYmxlQXV0b0FkanVzdE1haW5MdkNvbFdpZHRoIjogZmFsc2UsDQogICAgIk1haW5HaXJkSGVpZ2h0MSI6IDAs" +
            "DQogICAgIk1haW5HaXJkSGVpZ2h0MiI6IDAsDQogICAgIk1haW5HaXJkT3JpZW50YXRpb24iOiAxLA0KICAgICJDb2xvclByaW1h" +
            "cnlOYW1lIjogbnVsbCwNCiAgICAiQ3VycmVudFRoZW1lIjogbnVsbCwNCiAgICAiQ3VycmVudExhbmd1YWdlIjogImVuIiwNCiAg" +
            "ICAiQ3VycmVudEZvbnRGYW1pbHkiOiBudWxsLA0KICAgICJDdXJyZW50Rm9udFNpemUiOiAwLA0KICAgICJFbmFibGVEcmFnRHJv" +
            "cFNvcnQiOiBmYWxzZSwNCiAgICAiRG91YmxlQ2xpY2syQWN0aXZhdGUiOiBmYWxzZSwNCiAgICAiQXV0b0hpZGVTdGFydHVwIjog" +
            "ZmFsc2UsDQogICAgIkhpZGUyVHJheVdoZW5DbG9zZSI6IGZhbHNlLA0KICAgICJNYWNPU1Nob3dJbkRvY2siOiBmYWxzZSwNCiAg" +
            "ICAiTWFpbkNvbHVtbkl0ZW0iOiBbXSwNCiAgICAiV2luZG93U2l6ZUl0ZW0iOiBbDQogICAgICB7DQogICAgICAgICJUeXBlTmFt" +
            "ZSI6ICJTdWJFZGl0V2luZG93IiwNCiAgICAgICAgIldpZHRoIjogNzAwLA0KICAgICAgICAiSGVpZ2h0IjogNjUwDQogICAgICB9" +
            "LA0KICAgICAgew0KICAgICAgICAiVHlwZU5hbWUiOiAiT3B0aW9uU2V0dGluZ1dpbmRvdyIsDQogICAgICAgICJXaWR0aCI6IDEw" +
            "MDAsDQogICAgICAgICJIZWlnaHQiOiA3MDANCiAgICAgIH0sDQogICAgICB7DQogICAgICAgICJUeXBlTmFtZSI6ICJETlNTZXR0" +
            "aW5nV2luZG93IiwNCiAgICAgICAgIldpZHRoIjogMTAwMCwNCiAgICAgICAgIkhlaWdodCI6IDcwMA0KICAgICAgfSwNCiAgICAg" +
            "IHsNCiAgICAgICAgIlR5cGVOYW1lIjogIlJvdXRpbmdSdWxlU2V0dGluZ1dpbmRvdyIsDQogICAgICAgICJXaWR0aCI6IDEwMDAs" +
            "DQogICAgICAgICJIZWlnaHQiOiA3MDANCiAgICAgIH0sDQogICAgICB7DQogICAgICAgICJUeXBlTmFtZSI6ICJSb3V0aW5nU2V0" +
            "dGluZ1dpbmRvdyIsDQogICAgICAgICJXaWR0aCI6IDEwMDAsDQogICAgICAgICJIZWlnaHQiOiA3MDANCiAgICAgIH0sDQogICAg" +
            "ICB7DQogICAgICAgICJUeXBlTmFtZSI6ICJGdWxsQ29uZmlnVGVtcGxhdGVXaW5kb3ciLA0KICAgICAgICAiV2lkdGgiOiAxMDAw" +
            "LA0KICAgICAgICAiSGVpZ2h0IjogNzAwDQogICAgICB9LA0KICAgICAgew0KICAgICAgICAiVHlwZU5hbWUiOiAiR2xvYmFsSG90" +
            "a2V5U2V0dGluZ1dpbmRvdyIsDQogICAgICAgICJXaWR0aCI6IDcwMCwNCiAgICAgICAgIkhlaWdodCI6IDUwMA0KICAgICAgfQ0K" +
            "ICAgIF0sDQogICAgIkhpZGVDb2x1bW5JcEluZm8iOiBmYWxzZQ0KICB9LA0KICAiQ29uc3RJdGVtIjogew0KICAgICJTdWJDb252" +
            "ZXJ0VXJsIjogbnVsbCwNCiAgICAiR2VvU291cmNlVXJsIjogImh0dHBzOi8vZ2l0aHViLmNvbS9ydW5ldGZyZWVkb20vcnVzc2lh" +
            "LXYycmF5LXJ1bGVzLWRhdC9yZWxlYXNlcy9sYXRlc3QvZG93bmxvYWQvezB9LmRhdCIsDQogICAgIlNyc1NvdXJjZVVybCI6ICJo" +
            "dHRwczovL3Jhdy5naXRodWJ1c2VyY29udGVudC5jb20vcnVuZXRmcmVlZG9tL3J1c3NpYS12MnJheS1ydWxlcy1kYXQvcmVsZWFz" +
            "ZS9zaW5nLWJveC9ydWxlLXNldC17MH0vezF9LnNycyIsDQogICAgIlJvdXRlUnVsZXNUZW1wbGF0ZVNvdXJjZVVybCI6ICJodHRw" +
            "czovL3Jhdy5naXRodWJ1c2VyY29udGVudC5jb20vcnVuZXRmcmVlZG9tL3J1c3NpYS12MnJheS1jdXN0b20tcm91dGluZy1saXN0" +
            "L21haW4vdjJyYXlOL3RlbXBsYXRlLmpzb24iDQogIH0sDQogICJTcGVlZFRlc3RJdGVtIjogew0KICAgICJTcGVlZFRlc3RUaW1l" +
            "b3V0IjogMTAsDQogICAgIlNwZWVkVGVzdFVybCI6ICJodHRwczovL2NhY2hlZmx5LmNhY2hlZmx5Lm5ldC81MG1iLnRlc3QiLA0K" +
            "ICAgICJTcGVlZFBpbmdUZXN0VXJsIjogImh0dHBzOi8vd3d3Lmdvb2dsZS5jb20vZ2VuZXJhdGVfMjA0IiwNCiAgICAiTWl4ZWRD" +
            "b25jdXJyZW5jeUNvdW50IjogNSwNCiAgICAiSVBBUElVcmwiOiBudWxsLA0KICAgICJVZHBUZXN0VGFyZ2V0IjogIm50cDpwb29s" +
            "Lm50cC5vcmciLA0KICAgICJTcGVlZFRlc3RQYWdlU2l6ZSI6IG51bGwsDQogICAgIlNwZWVkVGVzdERlbGF5SW50ZXJ2YWwiOiBu" +
            "dWxsDQogIH0sDQogICJNdXg0UmF5SXRlbSI6IHsNCiAgICAiQ29uY3VycmVuY3kiOiA4LA0KICAgICJYdWRwQ29uY3VycmVuY3ki" +
            "OiAxNiwNCiAgICAiWHVkcFByb3h5VURQNDQzIjogInJlamVjdCINCiAgfSwNCiAgIk11eDRTYm94SXRlbSI6IHsNCiAgICAiUHJv" +
            "dG9jb2wiOiAiaDJtdXgiLA0KICAgICJNYXhDb25uZWN0aW9ucyI6IDgsDQogICAgIlBhZGRpbmciOiBudWxsDQogIH0sDQogICJI" +
            "eXN0ZXJpYUl0ZW0iOiB7DQogICAgIlVwTWJwcyI6IDEwMCwNCiAgICAiRG93bk1icHMiOiAxMDAsDQogICAgIkhvcEludGVydmFs" +
            "IjogMzANCiAgfSwNCiAgIkNsYXNoVUlJdGVtIjogew0KICAgICJSdWxlTW9kZSI6IDAsDQogICAgIkVuYWJsZUlQdjYiOiBmYWxz" +
            "ZSwNCiAgICAiRW5hYmxlTWl4aW5Db250ZW50IjogZmFsc2UsDQogICAgIlByb3hpZXNTb3J0aW5nIjogMCwNCiAgICAiUHJveGll" +
            "c0F1dG9SZWZyZXNoIjogZmFsc2UsDQogICAgIlByb3hpZXNBdXRvRGVsYXlUZXN0SW50ZXJ2YWwiOiAxMCwNCiAgICAiQ29ubmVj" +
            "dGlvbnNBdXRvUmVmcmVzaCI6IGZhbHNlLA0KICAgICJDb25uZWN0aW9uc1JlZnJlc2hJbnRlcnZhbCI6IDIsDQogICAgIkNvbm5l" +
            "Y3Rpb25zQ29sdW1uSXRlbSI6IFtdDQogIH0sDQogICJTeXN0ZW1Qcm94eUl0ZW0iOiB7DQogICAgIlN5c1Byb3h5VHlwZSI6IEBA" +
            "UFJPWFlAQCwNCiAgICAiU3lzdGVtUHJveHlFeGNlcHRpb25zIjogImxvY2FsaG9zdDsxMjcuKjsxMC4qOzE3Mi4xNi4qOzE3Mi4x" +
            "Ny4qOzE3Mi4xOC4qOzE3Mi4xOS4qOzE3Mi4yMC4qOzE3Mi4yMS4qOzE3Mi4yMi4qOzE3Mi4yMy4qOzE3Mi4yNC4qOzE3Mi4yNS4q" +
            "OzE3Mi4yNi4qOzE3Mi4yNy4qOzE3Mi4yOC4qOzE3Mi4yOS4qOzE3Mi4zMC4qOzE3Mi4zMS4qOzE5Mi4xNjguKiIsDQogICAgIk5v" +
            "dFByb3h5TG9jYWxBZGRyZXNzIjogdHJ1ZSwNCiAgICAiU3lzdGVtUHJveHlBZHZhbmNlZFByb3RvY29sIjogbnVsbCwNCiAgICAi" +
            "Q3VzdG9tU3lzdGVtUHJveHlQYWNQYXRoIjogIiIsDQogICAgIkN1c3RvbVN5c3RlbVByb3h5U2NyaXB0UGF0aCI6IG51bGwNCiAg" +
            "fSwNCiAgIldlYkRhdkl0ZW0iOiB7DQogICAgIlVybCI6IG51bGwsDQogICAgIlVzZXJOYW1lIjogbnVsbCwNCiAgICAiUGFzc3dv" +
            "cmQiOiBudWxsLA0KICAgICJEaXJOYW1lIjogbnVsbA0KICB9LA0KICAiQ2hlY2tVcGRhdGVJdGVtIjogew0KICAgICJDaGVja1By" +
            "ZVJlbGVhc2VVcGRhdGUiOiBmYWxzZSwNCiAgICAiU2VsZWN0ZWRDb3JlVHlwZXMiOiBudWxsDQogIH0sDQogICJGcmFnbWVudDRS" +
            "YXlJdGVtIjogew0KICAgICJQYWNrZXRzIjogInRsc2hlbGxvIiwNCiAgICAiTGVuZ3RocyI6IFsNCiAgICAgICI1MC0xMDAiDQog" +
            "ICAgXSwNCiAgICAiRGVsYXlzIjogWw0KICAgICAgIjEwLTIwIg0KICAgIF0sDQogICAgIk1heFNwbGl0IjogIjAiLA0KICAgICJM" +
            "ZW5ndGgiOiBudWxsLA0KICAgICJJbnRlcnZhbCI6IG51bGwNCiAgfSwNCiAgIkluYm91bmQiOiBbDQogICAgew0KICAgICAgIkxv" +
            "Y2FsUG9ydCI6IDEwODA4LA0KICAgICAgIlByb3RvY29sIjogInNvY2tzIiwNCiAgICAgICJVZHBFbmFibGVkIjogdHJ1ZSwNCiAg" +
            "ICAgICJTbmlmZmluZ0VuYWJsZWQiOiB0cnVlLA0KICAgICAgIkRlc3RPdmVycmlkZSI6IFsiaHR0cCIsInRscyIsInF1aWMiXSwN" +
            "CiAgICAgICJSb3V0ZU9ubHkiOiBmYWxzZSwNCiAgICAgICJBbGxvd0xBTkNvbm4iOiB0cnVlLA0KICAgICAgIk5ld1BvcnQ0TEFO" +
            "IjogZmFsc2UsDQogICAgICAiVXNlciI6ICIiLA0KICAgICAgIlBhc3MiOiAiIiwNCiAgICAgICJTZWNvbmRMb2NhbFBvcnRFbmFi" +
            "bGVkIjogZmFsc2UNCiAgICB9DQogIF0sDQogICJHbG9iYWxIb3RrZXlzIjogW10sDQogICJDb3JlVHlwZUl0ZW0iOiBbDQogICAg" +
            "ew0KICAgICAgIkNvbmZpZ1R5cGUiOiAxLA0KICAgICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZp" +
            "Z1R5cGUiOiAyLA0KICAgICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZpZ1R5cGUiOiAzLA0KICAg" +
            "ICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZpZ1R5cGUiOiA0LA0KICAgICAgIkNvcmVUeXBlIjog" +
            "Mg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZpZ1R5cGUiOiA1LA0KICAgICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAg" +
            "ew0KICAgICAgIkNvbmZpZ1R5cGUiOiA2LA0KICAgICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZp" +
            "Z1R5cGUiOiA3LA0KICAgICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZpZ1R5cGUiOiA4LA0KICAg" +
            "ICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZpZ1R5cGUiOiA5LA0KICAgICAgIkNvcmVUeXBlIjog" +
            "Mg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZpZ1R5cGUiOiAxMCwNCiAgICAgICJDb3JlVHlwZSI6IDINCiAgICB9LA0KICAg" +
            "IHsNCiAgICAgICJDb25maWdUeXBlIjogMTEsDQogICAgICAiQ29yZVR5cGUiOiAyDQogICAgfSwNCiAgICB7DQogICAgICAiQ29u" +
            "ZmlnVHlwZSI6IDEyLA0KICAgICAgIkNvcmVUeXBlIjogMg0KICAgIH0sDQogICAgew0KICAgICAgIkNvbmZpZ1R5cGUiOiAxMDEs" +
            "DQogICAgICAiQ29yZVR5cGUiOiAyDQogICAgfSwNCiAgICB7DQogICAgICAiQ29uZmlnVHlwZSI6IDEwMiwNCiAgICAgICJDb3Jl" +
            "VHlwZSI6IDINCiAgICB9DQogIF0sDQogICJTaW1wbGVETlNJdGVtIjogew0KICAgICJVc2VTeXN0ZW1Ib3N0cyI6IGZhbHNlLA0K" +
            "ICAgICJBZGRDb21tb25Ib3N0cyI6IHRydWUsDQogICAgIkZha2VJUCI6IGZhbHNlLA0KICAgICJHbG9iYWxGYWtlSXAiOiB0cnVl" +
            "LA0KICAgICJGYWtlSVBSYW5nZSI6IG51bGwsDQogICAgIkJsb2NrQmluZGluZ1F1ZXJ5IjogdHJ1ZSwNCiAgICAiRGlyZWN0RE5T" +
            "IjogIjc3Ljg4LjguOCIsDQogICAgIlJlbW90ZUROUyI6ICI4LjguOC44LGh0dHBzOi8vZG5zLmdvb2dsZS9kbnMtcXVlcnksMS4x" +
            "LjEuMSIsDQogICAgIkJvb3RzdHJhcEROUyI6ICI3Ny44OC44LjgiLA0KICAgICJTdHJhdGVneTRGcmVlZG9tIjogIiIsDQogICAg" +
            "IlN0cmF0ZWd5NFByb3h5IjogIiIsDQogICAgIlN0cmF0ZWd5NFByb3h5RGlhbCI6IG51bGwsDQogICAgIlNlcnZlU3RhbGUiOiBm" +
            "YWxzZSwNCiAgICAiUGFyYWxsZWxRdWVyeSI6IGZhbHNlLA0KICAgICJIb3N0cyI6ICIiLA0KICAgICJEaXJlY3RFeHBlY3RlZElQ" +
            "cyI6IG51bGwsDQogICAgIkVuYWJsZUhhcHB5RXllYmFsbHMiOiBudWxsDQogIH0sDQogICJIYXBweUV5ZWJhbGxzNFJheUl0ZW0i" +
            "OiB7DQogICAgIlRyeURlbGF5TXMiOiAyNTAsDQogICAgIlByaW9yaXRpemVJUHY2IjogZmFsc2UsDQogICAgIkludGVybGVhdmUi" +
            "OiAxLA0KICAgICJNYXhDb25jdXJyZW50VHJ5IjogNA0KICB9DQp9";


        private static string _root;      // папка, где лежит установщик
        private static string _target;    // папка установки
        private static bool _enableTun;
        private static string _link;
        private static string _profileId = "";

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Установка v2rayN " + Version;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;

            // Страховка: окно не должно закрываться молча ни при каких ошибках.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    Console.WriteLine();
                    Err("Непредвиденный сбой: " + (e.ExceptionObject == null ? "?" : e.ExceptionObject.ToString()));
                    Console.WriteLine();
                    Console.Write("  Нажмите Enter для выхода...");
                    Console.ReadLine();
                }
                catch { }
            };

            _root = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            _target = Path.Combine(_root, "v2rayN");

            Banner();

            // Служебный режим для проверки на готовой папке:
            //   V2raySetup.exe /dir=C:\путь vless://...
            // Пропускает скачивание, распаковку и запуск программы.
            string testDir = null;
            foreach (string a in args)
                if (a.StartsWith("/dir=", StringComparison.OrdinalIgnoreCase))
                    testDir = a.Substring(5).Trim('"');

            // Служебный режим: только скачать и распаковать, ничего не запуская.
            foreach (string a in args)
            {
                if (!a.Equals("/dltest", StringComparison.OrdinalIgnoreCase)) continue;
                _target = Path.Combine(Path.GetTempPath(), "v2rayN_dltest_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                string z = Download();
                if (z == null) { Pause(); return 2; }
                bool okx = Extract(z);
                Console.WriteLine();
                Console.WriteLine(okx ? "  Распаковано в: " + _target : "  Распаковка не удалась");
                if (okx && File.Exists(Path.Combine(_target, "v2rayN.exe")))
                    Ok("v2rayN.exe на месте");
                Pause();
                return 0;
            }

            try
            {
                if (!AskLink(args)) return 1;

                if (testDir != null)
                {
                    _target = testDir;
                    Console.WriteLine();
                    Console.WriteLine("  Служебный режим: работаю с папкой " + _target);
                    _enableTun = false;
                    if (!AddProfile()) return 5;
                    PatchSettings();
                    SetRouting();
                    Verify();
                    CreateShortcut();
                    Pause();
                    return 0;
                }

                AskTun();

                string zip = Download();
                if (zip == null) return 2;

                if (!Extract(zip)) return 3;

                if (!Initialize()) return 4;

                if (!AddProfile()) return 5;
                PatchSettings();
                SetRouting();
                DownloadGeo();
                Verify();
                CreateShortcut();
                AskAutostart();

                Launch();
                Finish();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Err("Ошибка: " + ex.Message);
                Console.WriteLine();
                Console.WriteLine("  Попробуйте запустить установщик ещё раз.");
            }

            Pause();
            return 0;
        }

        // ------------------------------------------------------------- ввод

        private static bool AskLink(string[] args)
        {
            foreach (string a in args)
                if (a.StartsWith("vless://", StringComparison.OrdinalIgnoreCase)) _link = a.Trim();

            while (string.IsNullOrEmpty(_link))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  Вставьте ссылку подключения (начинается с vless://)");
                Console.ResetColor();
                Console.WriteLine("  Правый клик в окне вставляет из буфера обмена.");
                Console.WriteLine();
                Console.Write("  > ");

                string s = Console.ReadLine();
                if (s == null) return false;
                s = s.Trim();

                if (s.Length == 0) { Err("Пустая строка."); continue; }
                if (!s.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                {
                    Err("Ссылка должна начинаться с vless://");
                    continue;
                }
                if (s.IndexOf('@') < 0)
                {
                    Err("Ссылка выглядит неполной - нет разделителя '@'.");
                    continue;
                }
                _link = s;
            }
            return true;
        }

        private static void AskTun()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  Включить режим TUN?");
            Console.ResetColor();
            Console.WriteLine("    Да  - через VPN пойдёт ВЕСЬ трафик: Discord, игры, все программы.");
            Console.WriteLine("          Программу нужно будет всегда запускать от администратора.");
            Console.WriteLine("    Нет - только браузеры и обычные программы. Проще и безопаснее.");
            Console.WriteLine();
            Console.Write("  Включить TUN? [Y/N]: ");

            string s = Console.ReadLine();
            s = (s ?? "").Trim().ToLowerInvariant();
            _enableTun = (s == "y" || s == "yes" || s == "д" || s == "да");
            Console.WriteLine(_enableTun ? "  Выбрано: TUN включён." : "  Выбрано: только системный прокси.");
        }

        // --------------------------------------------------------- скачивание

        private static string Download()
        {
            Step("Ищу последнюю версию v2rayN");

            string json;
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "v2raySetup");
                    json = wc.DownloadString(Api);
                }
            }
            catch (Exception ex)
            {
                Err("Не удалось связаться с GitHub: " + ex.Message);
                Console.WriteLine("  Проверьте интернет и попробуйте снова.");
                return null;
            }

            string tag = Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            string url = null;

            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                string u = m.Groups[1].Value;
                if (u.EndsWith(AssetName, StringComparison.OrdinalIgnoreCase)) { url = u; break; }
            }

            if (url == null) { Err("В релизе нет файла " + AssetName); return null; }

            Ok("Найдена версия " + (tag ?? "?"));

            string zip = Path.Combine(Path.GetTempPath(), "v2rayN_" + (tag ?? "latest") + ".zip");
            if (File.Exists(zip) && new FileInfo(zip).Length > 50L * 1024 * 1024)
            {
                Ok("Архив уже скачан, использую его");
                return zip;
            }

            Step("Скачиваю (около 160 МБ, это займёт время)");
            string part = zip + ".part";

            try
            {
                // Синхронное скачивание: всё в одном потоке, любая ошибка
                // ловится здесь же и показывается человеку, а не роняет окно.
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "v2raySetup";
                req.Timeout = 60000;
                req.ReadWriteTimeout = 120000;

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (Stream src = resp.GetResponseStream())
                using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    long total = resp.ContentLength;
                    long got = 0;
                    byte[] buf = new byte[81920];
                    int last = -1;
                    int n;

                    while (src != null && (n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        dst.Write(buf, 0, n);
                        got += n;

                        int pct = total > 0 ? (int)(got * 100 / total) : 0;
                        if (pct != last)
                        {
                            last = pct;
                            try
                            {
                                Console.Write("\r      " + Bar(pct) + " " + pct + "%  " +
                                              (got / 1048576) + " из " + (total / 1048576) + " МБ   ");
                            }
                            catch { }
                        }
                    }
                }
                Console.WriteLine();

                var fi = new FileInfo(part);
                if (fi.Length < 10L * 1024 * 1024)
                    throw new Exception("файл скачался не полностью (" + (fi.Length / 1024) + " КБ)");

                if (File.Exists(zip)) File.Delete(zip);
                File.Move(part, zip);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Err("Скачивание не удалось: " + ex.Message);
                Console.WriteLine("      Проверьте интернет. Если он идёт через VPN,");
                Console.WriteLine("      попробуйте временно его выключить и запустить снова.");
                try { if (File.Exists(part)) File.Delete(part); } catch { }
                return null;
            }

            Ok("Скачано");
            return zip;
        }

        private static string Bar(int pct)
        {
            int n = pct / 5;
            return "[" + new string('#', n) + new string('.', 20 - n) + "]";
        }

        // --------------------------------------------------------- распаковка

        private static bool Extract(string zip)
        {
            Step("Распаковываю в папку " + Path.GetFileName(_target));

            try
            {
                if (Directory.Exists(_target))
                {
                    Console.WriteLine("      Папка уже существует.");
                    Console.Write("      Переустановить с нуля? Настройки будут потеряны [Y/N]: ");
                    string a = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                    if (a == "y" || a == "yes" || a == "д" || a == "да")
                    {
                        KillV2rayN();
                        System.Threading.Thread.Sleep(1500);
                        Directory.Delete(_target, true);
                    }
                    else
                    {
                        Ok("Оставляю существующую установку");
                        return true;
                    }
                }

                string temp = Path.Combine(Path.GetTempPath(), "v2rayN_unpack_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(temp);

                Console.WriteLine("      Распаковка примерно 400 МБ, подождите...");
                try
                {
                    ZipFile.ExtractToDirectory(zip, temp);
                }
                catch (Exception ex1)
                {
                    // Запасной путь: распаковка средствами Windows. Нужен, если
                    // в системе нет нужной версии .NET Framework.
                    Console.WriteLine("      Штатная распаковка не удалась (" + ex1.Message + "),");
                    Console.WriteLine("      пробую средствами Windows...");
                    if (!ExtractViaShell(zip, temp))
                    {
                        Err("Распаковать архив не удалось.");
                        Console.WriteLine("      Распакуйте вручную: " + zip);
                        Console.WriteLine("      и положите содержимое в папку " + _target);
                        return false;
                    }
                }

                // В архиве может быть вложенная папка - находим ту, где лежит exe.
                string src = temp;
                if (!File.Exists(Path.Combine(src, "v2rayN.exe")))
                {
                    foreach (string d in Directory.GetDirectories(temp))
                        if (File.Exists(Path.Combine(d, "v2rayN.exe"))) { src = d; break; }
                }

                if (!File.Exists(Path.Combine(src, "v2rayN.exe")))
                {
                    Err("В архиве не найден v2rayN.exe");
                    return false;
                }

                Directory.Move(src, _target);
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }

                Ok("Распаковано");
                return true;
            }
            catch (Exception ex)
            {
                Err("Распаковка не удалась: " + ex.Message);
                return false;
            }
        }

        // Распаковка через проводник Windows - работает без .NET-библиотек.
        private static bool ExtractViaShell(string zip, string dest)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("Shell.Application");
                if (t == null) return false;

                object shell = Activator.CreateInstance(t);
                object zipFolder = t.InvokeMember("NameSpace",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { zip });
                object destFolder = t.InvokeMember("NameSpace",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { dest });
                if (zipFolder == null || destFolder == null) return false;

                object items = zipFolder.GetType().InvokeMember("Items",
                    System.Reflection.BindingFlags.InvokeMethod, null, zipFolder, null);

                // 16 - не спрашивать подтверждений, 4 - без диалога прогресса
                destFolder.GetType().InvokeMember("CopyHere",
                    System.Reflection.BindingFlags.InvokeMethod, null, destFolder,
                    new object[] { items, 16 });

                // CopyHere работает асинхронно - ждём появления файлов.
                for (int i = 0; i < 600; i++)
                {
                    System.Threading.Thread.Sleep(1000);
                    try
                    {
                        if (Directory.GetFileSystemEntries(dest).Length > 0)
                        {
                            long size = 0;
                            foreach (string f in Directory.GetFiles(dest, "*", SearchOption.AllDirectories))
                                size += new FileInfo(f).Length;
                            if (size > 100L * 1024 * 1024) { System.Threading.Thread.Sleep(5000); break; }
                        }
                    }
                    catch { }
                    if (i % 10 == 0) Console.Write(".");
                }
                Console.WriteLine();
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------ инициализация

        private static bool Initialize()
        {
            string db = Path.Combine(_target, "guiConfigs", "guiNDB.db");
            if (File.Exists(db)) { Ok("Конфигурация уже создана"); return true; }

            Step("Первый запуск для создания конфигурации");
            try
            {
                var psi = new ProcessStartInfo(Path.Combine(_target, "v2rayN.exe"))
                {
                    WorkingDirectory = _target,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex) { Err("Не удалось запустить: " + ex.Message); return false; }

            for (int i = 0; i < 60; i++)
            {
                System.Threading.Thread.Sleep(1000);
                if (File.Exists(db)) break;
                if (i % 5 == 0) Console.Write(".");
            }
            Console.WriteLine();

            System.Threading.Thread.Sleep(3000);
            KillV2rayN();
            System.Threading.Thread.Sleep(2000);

            if (!File.Exists(db)) { Err("Конфигурация не создалась"); return false; }
            Ok("Конфигурация создана");
            return true;
        }

        private static void KillV2rayN()
        {
            foreach (string n in new[] { "v2rayN", "xray", "sing-box", "mihomo" })
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName(n))
                    {
                        try { p.Kill(); p.WaitForExit(5000); } catch { }
                    }
                }
                catch { }
            }
        }

        // --------------------------------------------------------- настройки

        private static void PatchSettings()
        {
            Step("Применяю настройки");
            string dir = Path.Combine(_target, "guiConfigs");
            string path = Path.Combine(dir, "guiNConfig.json");

            try
            {
                Directory.CreateDirectory(dir);

                // v2rayN записывает guiNConfig.json только при корректном выходе,
                // поэтому после первого запуска файла ещё нет. Пишем сами -
                // из готового шаблона со всеми секциями.
                if (!File.Exists(path))
                {
                    string tpl = Encoding.UTF8.GetString(Convert.FromBase64String(ConfigTemplate));
                    tpl = tpl.Replace("@@INDEXID@@", _profileId)
                             .Replace("@@TUN@@", _enableTun ? "true" : "false")
                             .Replace("@@PROXY@@", _enableTun ? "0" : "1")
                             .Replace("@@ROUTEID@@", RoutingId);
                    File.WriteAllText(path, tpl, new UTF8Encoding(false));
                    Ok("Файл настроек создан со всеми параметрами");
                    return;
                }
            }
            catch (Exception ex) { Err("Создание настроек: " + ex.Message); }

            try
            {
                string j = File.ReadAllText(path, Encoding.UTF8);

                // Файл уже был - правим секции по месту.
                string tun =
                    "{\r\n" +
                    "    \"EnableTun\": " + (_enableTun ? "true" : "false") + ",\r\n" +
                    "    \"AutoRoute\": true,\r\n" +
                    "    \"StrictRoute\": false,\r\n" +
                    "    \"Stack\": \"gvisor\",\r\n" +
                    "    \"Mtu\": 9000,\r\n" +
                    "    \"EnableIPv6Address\": true,\r\n" +
                    "    \"IcmpRouting\": \"rule\",\r\n" +
                    "    \"EnableLegacyProtect\": false,\r\n" +
                    "    \"RouteExcludeAddress\": null,\r\n" +
                    "    \"IPv4Address\": null,\r\n" +
                    "    \"IPv6Address\": null\r\n" +
                    "  }";
                j = SetSection(j, "TunModeItem", tun);

                string routing =
                    "{\r\n" +
                    "    \"DomainStrategy\": \"IPOnDemand\",\r\n" +
                    "    \"DomainStrategy4Singbox\": null,\r\n" +
                    "    \"RoutingIndexId\": \"" + RoutingId + "\"\r\n" +
                    "  }";
                j = SetSection(j, "RoutingBasicItem", routing);

                // Системный прокси нужен только когда TUN выключен.
                j = SetNum(j, "SysProxyType", _enableTun ? 0 : 1);

                // Sniffing без DestOverride бесполезен: правила по доменам
                // не сработают, Xray будет видеть только IP.
                j = SetBool(j, "SniffingEnabled", true);
                j = SetBool(j, "UdpEnabled", true);
                j = SetArr(j, "DestOverride", "\"http\", \"tls\", \"quic\"");

                // Активный сервер - наш профиль.
                if (_profileId.Length > 0) j = SetStr(j, "IndexId", _profileId);

                File.WriteAllText(path, j, new UTF8Encoding(false));
                Ok("Настройки записаны");
            }
            catch (Exception ex) { Err("Настройки: " + ex.Message); }
        }

        private static string SetBool(string j, string key, bool val)
        {
            return Regex.Replace(j, "\"" + key + "\"\\s*:\\s*(true|false)",
                "\"" + key + "\": " + (val ? "true" : "false"), RegexOptions.IgnoreCase);
        }

        private static string SetNum(string j, string key, int val)
        {
            return Regex.Replace(j, "\"" + key + "\"\\s*:\\s*-?\\d+",
                "\"" + key + "\": " + val, RegexOptions.IgnoreCase);
        }

        // Заменяет целую секцию JSON по имени. Если секции нет - добавляет.
        // Границы блока ищутся по балансу скобок, поэтому вложенность не ломается.
        private static string SetSection(string j, string name, string body)
        {
            Match m = Regex.Match(j, "\"" + name + "\"\\s*:\\s*\\{", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                int last = j.LastIndexOf('}');
                if (last < 0) return j;
                string sep = j.Substring(0, last).TrimEnd().EndsWith(",") ? "" : ",";
                return j.Substring(0, last) + sep + "\r\n  \"" + name + "\": " + body + "\r\n}" +
                       j.Substring(last + 1);
            }

            int start = j.IndexOf('{', m.Index);
            int depth = 0;
            int end = -1;
            bool inStr = false;
            for (int i = start; i < j.Length; i++)
            {
                char c = j[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) { end = i; break; } }
            }
            if (end < 0) return j;

            return j.Substring(0, start) + body + j.Substring(end + 1);
        }

        private static string SetArr(string j, string key, string items)
        {
            return Regex.Replace(j, "\"" + key + "\"\\s*:\\s*\\[[^\\]]*\\]",
                "\"" + key + "\": [" + items + "]", RegexOptions.IgnoreCase);
        }

        private static string SetStr(string j, string key, string val)
        {
            return Regex.Replace(j, "\"" + key + "\"\\s*:\\s*\"[^\"]*\"",
                "\"" + key + "\": \"" + val + "\"", RegexOptions.IgnoreCase);
        }

        // ----------------------------------------------------------- профиль

        private static bool AddProfile()
        {
            Step("Добавляю профиль подключения");

            Uri u;
            try { u = new Uri(_link); }
            catch (Exception ex) { Err("Ссылка не разобрана: " + ex.Message); return false; }

            string uuid = Uri.UnescapeDataString(u.UserInfo ?? "");
            string host = u.Host;
            int port = u.Port > 0 ? u.Port : 443;
            string remarks = Uri.UnescapeDataString(u.Fragment.TrimStart('#'));
            if (remarks.Length == 0) remarks = host;

            var q = ParseQuery(u.Query);
            string network = Get(q, "type", "tcp");
            string security = Get(q, "security", "none");
            string sni = Get(q, "sni", "");
            string alpn = Get(q, "alpn", "");
            string fp = Get(q, "fp", "");
            string path = Get(q, "path", "");
            string qhost = Get(q, "host", "");
            string mode = Get(q, "mode", "");
            string flow = Get(q, "flow", "");
            string enc = Get(q, "encryption", "none");
            string pbk = Get(q, "pbk", "");
            string sid = Get(q, "sid", "");
            string spx = Get(q, "spx", "");
            string extra = Get(q, "extra", "");

            string protoExtra = "{\"Flow\":\"" + Esc(flow) + "\",\"VlessEncryption\":\"" + Esc(enc) + "\"}";
            string transportExtra = "{\"Host\":\"" + Esc(qhost) + "\",\"Path\":\"" + Esc(path) +
                                    "\",\"XhttpMode\":\"" + Esc(mode) + "\",\"XhttpExtra\":\"" + EscJson(extra) + "\"}";

            string indexId = NewIndexId();
            _profileId = indexId;

            string sql =
                "INSERT OR REPLACE INTO ProfileItem " +
                "(IndexId,ConfigType,ConfigVersion,IsSub,Remarks,Address,Port,Password," +
                "Network,StreamSecurity,Sni,Alpn,Fingerprint,PublicKey,ShortId,SpiderX," +
                "ProtoExtra,TransportExtra) VALUES ('" +
                Q(indexId) + "',5,4,0,'" + Q(remarks) + "','" + Q(host) + "'," + port + ",'" + Q(uuid) + "','" +
                Q(network) + "','" + Q(security) + "','" + Q(sni) + "','" + Q(alpn) + "','" + Q(fp) + "','" +
                Q(pbk) + "','" + Q(sid) + "','" + Q(spx) + "','" + Q(protoExtra) + "','" + Q(transportExtra) + "');";

            if (!Sql(sql)) return false;

            Ok("Профиль добавлен: " + remarks);
            return true;
        }

        // Фиксированный идентификатор набора правил: и в базе, и в конфиге
        // должен быть один и тот же, иначе активный набор не совпадёт.
        private const string RoutingId = "7100000000000000001";

        private static void SetRouting()
        {
            Step("Настраиваю маршрутизацию");

            // Набор не ищем среди готовых - создаём свой. Готовые в разных
            // сборках называются по-разному или отсутствуют вовсе.
            // Только те категории, которые есть в ЛЮБОМ geosite.dat. Правило
            // geosite:ru-available-only-inside существует лишь в русской сборке
            // geo-файлов, и на стандартной ядро не стартует вовсе.
            string rules =
                "[" +
                "{\"Id\":\"7100000000000000011\",\"Port\":\"\",\"OutboundTag\":\"direct\",\"Protocol\":[\"bittorrent\"],\"Enabled\":true,\"Remarks\":\"Торренты напрямую\"}," +
                "{\"Id\":\"7100000000000000013\",\"OutboundTag\":\"direct\",\"Ip\":[\"geoip:private\"],\"Enabled\":true,\"Remarks\":\"Локальная сеть напрямую\"}," +
                "{\"Id\":\"7100000000000000015\",\"OutboundTag\":\"direct\",\"Ip\":[\"geoip:ru\"],\"Domain\":[\"domain:ru\",\"domain:su\",\"domain:рф\"],\"Enabled\":true,\"Remarks\":\"Российские напрямую\"}," +
                "{\"Id\":\"7100000000000000016\",\"Port\":\"0-65535\",\"OutboundTag\":\"proxy\",\"Enabled\":true,\"Remarks\":\"Остальное через VPN\"}" +
                "]";

            bool ok = Sql("UPDATE RoutingItem SET IsActive=0;");
            ok &= Sql("INSERT OR REPLACE INTO RoutingItem " +
                      "(Id,Remarks,Url,RuleSet,RuleNum,Enabled,Locked,DomainStrategy,Sort,IsActive) VALUES ('" +
                      RoutingId + "','РФ напрямую, остальное через VPN','','" + Q(rules) +
                      "',6,1,0,'IPOnDemand',1,1);");

            if (ok) Ok("Правила созданы и включены");
            else Err("Не удалось записать правила маршрутизации");
        }

        // Русские geo-файлы: точные списки российских адресов и доменов.
        // Не критичны - правила работают и на стандартных, - но заметно точнее.
        private static void DownloadGeo()
        {
            Step("Обновляю списки российских адресов");
            string bin = Path.Combine(_target, "bin");
            try { Directory.CreateDirectory(bin); } catch { }

            string[] names = { "geoip", "geosite" };
            int done = 0;

            foreach (string n in names)
            {
                string url = "https://github.com/runetfreedom/russia-v2ray-rules-dat/releases/latest/download/" + n + ".dat";
                string dst = Path.Combine(bin, n + ".dat");
                string tmp = dst + ".part";
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "v2raySetup";
                    req.Timeout = 60000;
                    req.ReadWriteTimeout = 120000;

                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (Stream s = resp.GetResponseStream())
                    using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        long total = resp.ContentLength, got = 0;
                        byte[] buf = new byte[81920];
                        int last = -1, k;
                        while (s != null && (k = s.Read(buf, 0, buf.Length)) > 0)
                        {
                            fs.Write(buf, 0, k);
                            got += k;
                            int pct = total > 0 ? (int)(got * 100 / total) : 0;
                            if (pct != last)
                            {
                                last = pct;
                                try { Console.Write("\r      " + n + ".dat  " + Bar(pct) + " " + pct + "%   "); }
                                catch { }
                            }
                        }
                    }
                    Console.WriteLine();

                    if (new FileInfo(tmp).Length < 1024 * 100) throw new Exception("файл слишком мал");
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(tmp, dst);
                    done++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Err(n + ".dat не обновлён: " + ex.Message);
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }

            if (done == names.Length) Ok("Списки обновлены - маршрутизация будет точнее");
            else Ok("Останутся стандартные списки, правила всё равно работают");
        }

        // ------------------------------------------------------------ SQLite

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        [DllImport("e_sqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);

        [DllImport("e_sqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr cb, IntPtr arg, out IntPtr err);

        [DllImport("e_sqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        private static bool Sql(string sql)
        {
            string db = Path.Combine(_target, "guiConfigs", "guiNDB.db");
            if (!File.Exists(db)) { Err("База данных не найдена"); return false; }

            try
            {
                SetDllDirectory(_target);
                IntPtr h;
                int rc = sqlite3_open_v2(Encoding.UTF8.GetBytes(db + "\0"), out h, 2 /*READWRITE*/, IntPtr.Zero);
                if (rc != 0) { Err("База данных недоступна (код " + rc + ")"); return false; }

                IntPtr err;
                rc = sqlite3_exec(h, Encoding.UTF8.GetBytes(sql + "\0"), IntPtr.Zero, IntPtr.Zero, out err);
                sqlite3_close(h);

                if (rc != 0) { Err("Запись в базу не удалась (код " + rc + ")"); return false; }
                return true;
            }
            catch (Exception ex) { Err("База данных: " + ex.Message); return false; }
        }

        // ----------------------------------------------------------- утилиты

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return d;
            foreach (string part in query.TrimStart('?').Split('&'))
            {
                if (part.Length == 0) continue;
                int i = part.IndexOf('=');
                string k = i < 0 ? part : part.Substring(0, i);
                string v = i < 0 ? "" : part.Substring(i + 1);
                d[Uri.UnescapeDataString(k)] = Uri.UnescapeDataString(v);
            }
            return d;
        }

        private static string Get(Dictionary<string, string> d, string k, string def)
        {
            string v;
            return d.TryGetValue(k, out v) && v != null ? v : def;
        }

        private static string Q(string s) { return (s ?? "").Replace("'", "''"); }

        private static string Esc(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // Вложенный JSON внутри строкового поля JSON: экранируем дважды.
        private static string EscJson(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
                            .Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string NewIndexId()
        {
            byte[] b = Guid.NewGuid().ToByteArray();
            ulong v = BitConverter.ToUInt64(b, 0) % 9000000000000000000UL + 1000000000000000000UL;
            return v.ToString();
        }

        private static string Match(string s, string pattern)
        {
            Match m = Regex.Match(s, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        // Читает записанное обратно и показывает, что реально применилось.
        private static void Verify()
        {
            Step("Проверяю, что настройки применились");
            string path = Path.Combine(_target, "guiConfigs", "guiNConfig.json");
            try
            {
                string j = File.ReadAllText(path, Encoding.UTF8);
                Check(j, "EnableTun", _enableTun ? "true" : "false");
                Check(j, "StrictRoute", "false");
                Check(j, "EnableLegacyProtect", "false");
                Check(j, "Stack", "\"gvisor\"");
                Check(j, "Mtu", "9000");
                Check(j, "SysProxyType", _enableTun ? "0" : "1");
                Check(j, "RoutingIndexId", "\"" + RoutingId + "\"");

                bool dest = Regex.IsMatch(j, "\"DestOverride\"\\s*:\\s*\\[\\s*\"http\"");
                Line("DestOverride = http,tls,quic", dest);
            }
            catch (Exception ex) { Err("Проверка: " + ex.Message); }
        }

        private static void Check(string j, string key, string expected)
        {
            Match m = Regex.Match(j, "\"" + key + "\"\\s*:\\s*([^,\\r\\n}]+)", RegexOptions.IgnoreCase);
            string actual = m.Success ? m.Groups[1].Value.Trim() : "(нет ключа)";
            Line(key + " = " + actual, m.Success &&
                 string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase));
        }

        private static void Line(string text, bool ok)
        {
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine("      " + (ok ? "[ok] " : "[!!] ") + text);
            Console.ResetColor();
        }

        // Ярлык на рабочем столе, чтобы человеку не искать папку.
        private static void CreateShortcut()
        {
            Step("Создаю ярлык на рабочем столе");
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desktop, "VPN.lnk");
                string exe = Path.Combine(_target, "v2rayN.exe");

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) { Err("Не удалось создать ярлык"); return; }

                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = sc.GetType();

                st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { exe });
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { _target });
                st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { exe + ",0" });
                st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "Запуск VPN" });
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);

                Ok("Ярлык \"VPN\" на рабочем столе");
            }
            catch (Exception ex) { Err("Ярлык: " + ex.Message); }
        }

        private static void AskAutostart()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  Запускать VPN автоматически при включении компьютера?");
            Console.ResetColor();
            Console.Write("  [Y/N]: ");
            string s = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (s != "y" && s != "yes" && s != "д" && s != "да")
            {
                Console.WriteLine("  Хорошо, будете запускать сами ярлыком.");
                return;
            }

            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k == null) { Err("Автозапуск: ветка недоступна"); return; }
                    k.SetValue("v2rayN", "\"" + Path.Combine(_target, "v2rayN.exe") + "\"");
                }
                Ok("Автозапуск включён");
                if (_enableTun)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("      Внимание: с включённым TUN автозапуск сработает без прав");
                    Console.WriteLine("      администратора, и TUN не поднимется. Запускайте ярлыком.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex) { Err("Автозапуск: " + ex.Message); }
        }

        private static void Launch()
        {
            Step("Запускаю v2rayN");
            try
            {
                var psi = new ProcessStartInfo(Path.Combine(_target, "v2rayN.exe"))
                {
                    WorkingDirectory = _target,
                    UseShellExecute = true
                };
                if (_enableTun) psi.Verb = "runas"; // TUN требует прав администратора
                Process.Start(psi);
                Ok("Запущено");
            }
            catch (Exception ex) { Err("Запуск: " + ex.Message); }
        }

        // ---------------------------------------------------------------- UI

        private static void Banner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  ============================================================");
            Console.WriteLine("     Установка VPN-клиента v2rayN");
            Console.WriteLine("  ============================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Что произойдёт:");
            Console.WriteLine("    1. Скачается свежая версия v2rayN с официального GitHub");
            Console.WriteLine("    2. Распакуется в папку рядом с этим файлом");
            Console.WriteLine("    3. Добавится ваш профиль подключения");
            Console.WriteLine("    4. Настроится маршрутизация: РФ напрямую, остальное через VPN");
            Console.WriteLine("    5. Программа запустится и будет готова к работе");
        }

        private static void Step(string s)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  " + s);
            Console.ResetColor();
        }

        private static void Ok(string s)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("      [ok] " + s);
            Console.ResetColor();
        }

        private static void Err(string s)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("      [!!] " + s);
            Console.ResetColor();
        }

        private static void Finish()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ============================================================");
            Console.WriteLine("     Готово. VPN установлен и запущен.");
            Console.WriteLine("  ============================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Дальше всё просто: запускаете ярлык \"VPN\" на рабочем столе -");
            Console.WriteLine("  программа подключается сама, ничего нажимать не нужно.");
            Console.WriteLine("  Значок висит в трее у часов, выход - через меню \"Выход\".");
            Console.WriteLine();
            Console.WriteLine("  Папка установки: " + _target);
            Console.WriteLine();
            if (_enableTun)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  TUN включён: запускайте v2rayN только от администратора,");
                Console.WriteLine("  и выходите через меню \"Выход\", а не крестиком.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("  Если понадобится Discord-голос или игры - включите TUN");
                Console.WriteLine("  в самой программе и перезапустите её от администратора.");
            }
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.Write("  Нажмите Enter для выхода...");
            try { Console.ReadLine(); } catch { }
        }
    }
}

