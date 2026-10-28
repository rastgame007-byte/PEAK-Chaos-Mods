@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
title PeakJujutsu - УДАЛЕНИЕ
set "GAME="
if not "%~1"=="" if exist "%~1\PEAK.exe" set "GAME=%~1"
if not defined GAME (
  set "STEAM="
  for /f "tokens=2,*" %%a in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul ^| find "SteamPath"') do set "STEAM=%%b"
  if defined STEAM (
    set "STEAM=!STEAM:/=\!"
    if exist "!STEAM!\steamapps\common\PEAK\PEAK.exe" set "GAME=!STEAM!\steamapps\common\PEAK"
    if not defined GAME if exist "!STEAM!\steamapps\libraryfolders.vdf" (
      for /f tokens^=4^ delims^=^" %%p in ('findstr /c:"\"path\"" "!STEAM!\steamapps\libraryfolders.vdf"') do (
        set "LIB=%%p"
        set "LIB=!LIB:\\=\!"
        if not defined GAME if exist "!LIB!\steamapps\common\PEAK\PEAK.exe" set "GAME=!LIB!\steamapps\common\PEAK"
      )
    )
  )
)
if not defined GAME for %%d in (C D E F G) do (
  if not defined GAME if exist "%%d:\Program Files (x86)\Steam\steamapps\common\PEAK\PEAK.exe" set "GAME=%%d:\Program Files (x86)\Steam\steamapps\common\PEAK"
  if not defined GAME if exist "%%d:\SteamLibrary\steamapps\common\PEAK\PEAK.exe" set "GAME=%%d:\SteamLibrary\steamapps\common\PEAK"
)
if not defined GAME (
  echo  Папку игры найти не удалось.
  set /p "GAME=  Вставь путь к папке PEAK и нажми Enter: "
  set "GAME=!GAME:"=!"
)
if not exist "!GAME!\PEAK.exe" (
  echo  В папке "!GAME!" нет PEAK.exe. Удаление отменено.
  pause
  exit /b 1
)
echo.
echo  Игра: !GAME!
echo  Закрой PEAK, если она запущена.
echo.
echo  [1] Убрать только PeakJujutsu (его настройки и отчёты). BepInEx и другие моды останутся.
echo  [2] Убрать ВСЁ: PeakJujutsu + BepInEx + загрузчик (игра станет совсем чистой).
echo  [N] Отмена.
echo.
choice /c 12N /m "Выбери"
if errorlevel 3 exit /b 0
set "MODE=1"
if errorlevel 2 set "MODE=2"

del /q "!GAME!\BepInEx\plugins\PeakJujutsu.dll" 2>nul
del /q "!GAME!\BepInEx\config\com.friends.peakjujutsu.cfg" 2>nul
del /q "!GAME!\BepInEx\PeakJujutsu_*.txt" 2>nul

if "!MODE!"=="2" (
  if exist "!GAME!\BepInEx" rmdir /s /q "!GAME!\BepInEx"
  del /q "!GAME!\winhttp.dll" 2>nul
  del /q "!GAME!\doorstop_config.ini" 2>nul
  del /q "!GAME!\.doorstop_version" 2>nul
  del /q "!GAME!\changelog.txt" 2>nul
  echo.
  echo  Готово: мод и BepInEx удалены. Игра как из Steam.
) else (
  echo.
  echo  Готово: PeakJujutsu удалён.
)
echo  Если что-то осталось странным: Steam - PEAK - Свойства - Установленные файлы - Проверить целостность.
echo.
pause
