@echo off
set "VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\18\Community\"
set "VCToolsInstallDir=C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.50.35717\"
set "WindowsSdkDir=C:\Program Files (x86)\Windows Kits\10\"

cd Mostlylucid.BotDetection.Console
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true -p:PublishSingleFile=true
