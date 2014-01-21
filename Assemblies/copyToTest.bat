@echo off

if "%1" NEQ "release" if "%1" NEQ "debug" goto usage
if "%2"=="" goto usage

setlocal
set PATH=%PATH%;%~dp0..\Externals\Mono 2.0
cd %1

pdb2mdb.exe %~dp0%1\CommandLine.dll
pdb2mdb.exe %~dp0%1\Common.dll
pdb2mdb.exe %~dp0%1\SVNBackend.dll
pdb2mdb.exe %~dp0%1\P4Backend.dll
pdb2mdb.exe %~dp0%1\UnityVersionControl.dll
pdb2mdb.exe %~dp0%1\TeamFeatures.dll
pdb2mdb.exe %~dp0%1\RendererInspectors.dll
xcopy /y %~dp0%1\*.dll %2
xcopy /y %~dp0%1\*.mdb %2
if "%3" NEQ "" xcopy /y /R %~dp0%1\*.dll %3
if "%3" NEQ "" xcopy /y /R %~dp0%1\*.mdb %3
if "%4" NEQ "" xcopy /y /R %~dp0%1\*.dll %4
if "%4" NEQ "" xcopy /y /R %~dp0%1\*.mdb %4

cd ..
endlocal

@echo Done.
goto :eof
:usage
@echo Usage: %0 [debug^|release] ^<destination path^>
exit /B 1

