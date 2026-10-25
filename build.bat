@echo off
setlocal
cd /d "%~dp0"
if not defined GAME set "GAME=%~dp0.."
set "MANAGED=%GAME%\CharaStudio_Data\Managed"
set "BEP=%GAME%\BepInEx"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "decompiled\done.txt" call "%~dp0decompile.bat" nopause

if not exist bin mkdir bin
echo Building StudioCharaFilter... > build.log
"%CSC%" /nologo /noconfig /nostdlib+ /target:library /optimize+ /warn:3 /out:"bin\KKS_StudioCharaFilter.dll" ^
 /reference:"%MANAGED%\mscorlib.dll" ^
 /reference:"%MANAGED%\System.dll" ^
 /reference:"%MANAGED%\System.Core.dll" ^
 /reference:"%MANAGED%\System.Xml.dll" ^
 /reference:"%MANAGED%\UnityEngine.dll" ^
 /reference:"%MANAGED%\UnityEngine.CoreModule.dll" ^
 /reference:"%MANAGED%\UnityEngine.UI.dll" ^
 /reference:"%MANAGED%\UnityEngine.UIModule.dll" ^
 /reference:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
 /reference:"%MANAGED%\UnityEngine.TextRenderingModule.dll" ^
 /reference:"%MANAGED%\UnityEngine.InputLegacyModule.dll" ^
 /reference:"%MANAGED%\UnityEngine.ImageConversionModule.dll" ^
 /reference:"%MANAGED%\Unity.TextMeshPro.dll" ^
 /reference:"%MANAGED%\UniRx.dll" ^
 /reference:"%MANAGED%\Assembly-CSharp.dll" ^
 /reference:"%MANAGED%\Assembly-CSharp-firstpass.dll" ^
 /reference:"%BEP%\core\BepInEx.dll" ^
 /reference:"%BEP%\core\0Harmony.dll" ^
 /reference:"%BEP%\plugins\KKSAPI.dll" ^
 /reference:"%BEP%\plugins\KKS_BepisPlugins\KKS_ExtensibleSaveFormat.dll" ^
 /recurse:"src\*.cs" >> build.log 2>&1
if errorlevel 1 goto :failed

if exist "%BEP%\plugins\StudioCharaFilter.dll" (
  if not exist releases mkdir releases
  move /y "%BEP%\plugins\StudioCharaFilter.dll" "releases\old_StudioCharaFilter.dll" >> build.log
  echo Moved the old StudioCharaFilter.dll out of plugins into _ModDev\releases >> build.log
)
copy /y "bin\KKS_StudioCharaFilter.dll" "%BEP%\plugins\KKS_StudioCharaFilter.dll" >> build.log
if errorlevel 1 (
  echo Could not copy the DLL into BepInEx\plugins - close CharaStudio and run build.bat again. >> build.log
  goto :failed
)
echo BUILD OK - installed to BepInEx\plugins\KKS_StudioCharaFilter.dll >> build.log
type build.log
goto :end

:failed
echo BUILD FAILED >> build.log
type build.log

:end
if /i not "%~1"=="nopause" pause
