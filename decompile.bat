@echo off
setlocal
cd /d "%~dp0"
if not defined GAME set "GAME=%~dp0.."
set "MANAGED=%GAME%\CharaStudio_Data\Managed"
set "BEP=%GAME%\BepInEx"
set "DNSPY=%GAME%\[MODDING] Tools\dnSpy v6.1.5\dnSpy.Console.exe"
if not exist "%DNSPY%" (
  echo dnSpy.Console.exe not found at "%DNSPY%"
  goto :end
)
if not exist decompiled mkdir decompiled
echo Decompiling game code. This takes a few minutes, please wait...
echo === Assembly-CSharp === > decompiled\decompile.log
"%DNSPY%" --no-sln -o "decompiled\Assembly-CSharp" "%MANAGED%\Assembly-CSharp.dll" >> decompiled\decompile.log 2>&1
call :plugin "%BEP%\plugins\KKSAPI.dll" KKSAPI
call :plugin "%BEP%\plugins\StudioCharaSort.dll" StudioCharaSort
call :plugin "%BEP%\plugins\KKS_BrowserFolders.dll" BrowserFolders
call :plugin "%BEP%\plugins\KKS_BrowserFolders_Hooks.dll" BrowserFolders_Hooks
call :plugin "%BEP%\plugins\KKS_Additional_Card_Info.dll" AdditionalCardInfo
call :plugin "%BEP%\plugins\IllusionFixes\KKS_Fix_CharacterListOptimizations.dll" CharacterListOptimizations
call :plugin "%BEP%\plugins\KKS_Plugins\KKS_ReloadCharaListOnChange.dll" ReloadCharaListOnChange
call :plugin "%BEP%\plugins\2155X\KKS_StudioMiscSearch.dll" StudioMiscSearch
echo Done. > decompiled\done.txt
echo Decompile finished.
goto :end

:plugin
echo === %2 === >> decompiled\decompile.log
"%DNSPY%" --no-sln --asm-path "%MANAGED%;%BEP%\core;%BEP%\plugins" -o "decompiled\%2" %1 >> decompiled\decompile.log 2>&1
if errorlevel 1 "%DNSPY%" --no-sln -o "decompiled\%2" %1 >> decompiled\decompile.log 2>&1
exit /b 0

:end
if /i not "%~1"=="nopause" pause
