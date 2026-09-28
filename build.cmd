@echo off
rem Build GBF.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /win32manifest:GBF.manifest /out:GBF.exe Program.cs GbfWindow.cs Settings.cs Browsers.cs SettingsForm.cs
