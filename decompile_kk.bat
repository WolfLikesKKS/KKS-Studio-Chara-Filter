@echo off
setlocal
cd /d "%~dp0"
rem Decompiles the Koikatsu (KK) game code that the KK build of the plugin talks to, into decompiled_kk\ (not published).
if "%KKGAME%"=="" set "KKGAME=%~dp0..\..\[ScrewThisNoise] Koikatsu BetterRepack RX22 dev"
set "MANAGED=%KKGAME%\CharaStudio_Data\Managed"
set "BEP=%KKGAME%\BepInEx"
set "DNSPY=%KKGAME%\[MODDING] Tools\dnSpy v6.1.5\dnSpy.Console.exe"
if not exist "%DNSPY%" set "DNSPY=%~dp0..\[MODDING] Tools\dnSpy v6.1.5\dnSpy.Console.exe"
if not exist "%MANAGED%\Assembly-CSharp.dll" (
  echo Koikatsu not found at "%KKGAME%". Set KKGAME to your Koikatsu folder and run again.
  goto :end
)
if not exist "%DNSPY%" (
  echo dnSpy.Console.exe not found.
  goto :end
)
if not exist decompiled_kk mkdir decompiled_kk
echo Decompiling Koikatsu code. This takes a few minutes, please wait...
echo === Assembly-CSharp === > decompiled_kk\decompile.log
"%DNSPY%" --no-sln -o "decompiled_kk\Assembly-CSharp" "%MANAGED%\Assembly-CSharp.dll" >> decompiled_kk\decompile.log 2>&1
call :plugin "%BEP%\plugins\KKAPI.dll" KKAPI
call :plugin "%BEP%\plugins\StudioCharaSort.dll" StudioCharaSort
call :plugin "%BEP%\plugins\KK_BrowserFolders.dll" BrowserFolders
call :plugin "%BEP%\plugins\KK_BrowserFolders_Hooks_KK.dll" BrowserFolders_Hooks
call :plugin "%BEP%\plugins\KK_Additional_Card_Info.dll" AdditionalCardInfo
call :plugin "%BEP%\plugins\IllusionFixes\KK_Fix_CharacterListOptimizations.dll" CharacterListOptimizations
call :plugin "%BEP%\plugins\KK_Plugins\KK_ReloadCharaListOnChange.dll" ReloadCharaListOnChange
call :plugin "%BEP%\plugins\KK_Plugins\KK_MoreOutfits.dll" MoreOutfits
call :plugin "%BEP%\plugins\2155X\KK_StudioMiscSearch.dll" StudioMiscSearch
echo Done. > decompiled_kk\done.txt
echo Decompile finished.
goto :end

:plugin
echo === %2 === >> decompiled_kk\decompile.log
"%DNSPY%" --no-sln --asm-path "%MANAGED%;%BEP%\core;%BEP%\plugins" -o "decompiled_kk\%2" %1 >> decompiled_kk\decompile.log 2>&1
if errorlevel 1 "%DNSPY%" --no-sln -o "decompiled_kk\%2" %1 >> decompiled_kk\decompile.log 2>&1
exit /b 0

:end
if /i not "%~1"=="nopause" pause
