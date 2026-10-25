@echo off
setlocal
cd /d "%~dp0"
rem Builds the Koikatsu / Koikatsu Party version (KK_StudioCharaFilter.dll) from the same src\ folder.
rem Set KKGAME to your Koikatsu folder if it isn't next to this game's folder.
rem Looked for next to this game's folder: from the repo folder (..\..) or from _PerfDev\charafilter (..\..\..).
if "%KKGAME%"=="" if exist "%~dp0..\..\[ScrewThisNoise] Koikatsu BetterRepack RX22 dev\CharaStudio_Data" set "KKGAME=%~dp0..\..\[ScrewThisNoise] Koikatsu BetterRepack RX22 dev"
if "%KKGAME%"=="" if exist "%~dp0..\..\..\[ScrewThisNoise] Koikatsu BetterRepack RX22 dev\CharaStudio_Data" set "KKGAME=%~dp0..\..\..\[ScrewThisNoise] Koikatsu BetterRepack RX22 dev"
if "%KKGAME%"=="" set "KKGAME=%~dp0..\..\[ScrewThisNoise] Koikatsu BetterRepack RX22 dev"
set "MANAGED=%KKGAME%\CharaStudio_Data\Managed"
set "BEP=%KKGAME%\BepInEx"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%MANAGED%\Assembly-CSharp.dll" (
  echo Koikatsu not found at "%KKGAME%". Set KKGAME to your Koikatsu folder and run again. > build_kk.log
  goto :failed
)

if not exist bin mkdir bin
rem The version being built, read from the source.
set "VER=?"
for /f tokens^=2^ delims^=^" %%v in ('findstr /c:"const string Version" "src\Plugin.cs"') do set "VER=%%v"
echo Building KK_StudioCharaFilter v%VER%... > build_kk.log
"%CSC%" /nologo /noconfig /nostdlib+ /target:library /optimize+ /warn:3 /define:KK /out:"bin\KK_StudioCharaFilter.dll" ^
 /reference:"%MANAGED%\mscorlib.dll" ^
 /reference:"%MANAGED%\System.dll" ^
 /reference:"%MANAGED%\System.Core.dll" ^
 /reference:"%MANAGED%\System.Xml.dll" ^
 /reference:"%MANAGED%\UnityEngine.dll" ^
 /reference:"%MANAGED%\UnityEngine.UI.dll" ^
 /reference:"%MANAGED%\Assembly-CSharp.dll" ^
 /reference:"%MANAGED%\Assembly-CSharp-firstpass.dll" ^
 /reference:"%BEP%\core\BepInEx.dll" ^
 /reference:"%BEP%\core\0Harmony.dll" ^
 /reference:"%BEP%\plugins\KKAPI.dll" ^
 /reference:"%BEP%\plugins\KK_BepisPlugins\ExtensibleSaveFormat.dll" ^
 /recurse:"src\*.cs" >> build_kk.log 2>&1
if errorlevel 1 goto :failed

copy /y "bin\KK_StudioCharaFilter.dll" "%BEP%\plugins\KK_StudioCharaFilter.dll" >> build_kk.log
if errorlevel 1 (
  echo Could not copy the DLL into Koikatsu's BepInEx\plugins - close CharaStudio and run build_kk.bat again. >> build_kk.log
  goto :failed
)
echo BUILD OK - v%VER% - installed to Koikatsu's BepInEx\plugins\KK_StudioCharaFilter.dll >> build_kk.log
type build_kk.log
goto :end

:failed
echo BUILD FAILED >> build_kk.log
type build_kk.log

:end
if /i not "%~1"=="nopause" pause
