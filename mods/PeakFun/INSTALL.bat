@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
title PeakFun - УСТАНОВКА
set "SRC=%~dp01_COPY_INTO_GAME_FOLDER"
if not exist "%SRC%\BepInEx\plugins\PeakFun.dll" (
  echo.
  echo  Не вижу папку 1_COPY_INTO_GAME_FOLDER рядом с этим файлом.
  echo  Сначала РАСПАКУЙ архив целиком, потом запускай INSTALL.bat.
  echo.
  pause
  exit /b 1
)
set "GAME="
if not "%~1"=="" if exist "%~1\PEAK.exe" set "GAME=%~1"

rem --- поиск через Steam (реестр + библиотеки) ---
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
  echo  Steam - правой кнопкой по PEAK - Управление - Просмотреть локальные файлы - скопируй путь из адресной строки.
  set /p "GAME=  Вставь путь к папке PEAK и нажми Enter: "
  set "GAME=!GAME:"=!"
)
if not exist "!GAME!\PEAK.exe" (
  echo.
  echo  В папке "!GAME!" нет PEAK.exe. Установка отменена.
  pause
  exit /b 1
)
echo.
echo  Игра найдена: !GAME!
echo  Закрой PEAK, если она запущена.
echo.
echo  Будет скопировано: BepInEx, winhttp.dll, doorstop_config.ini и мод PeakFun.dll
echo  Это ВРЕМЕННО: UNINSTALL.bat уберёт мод и вернёт игру как была.
echo.
choice /c YN /m "Установить"
if errorlevel 2 exit /b 0
xcopy "%SRC%\*" "!GAME!\" /e /i /y /h >nul
if errorlevel 1 (
  echo  Ошибка копирования. Проверь, что игра закрыта, и запусти ещё раз.
  pause
  exit /b 1
)
> "!GAME!\BepInEx\PeakFun_INSTALLED_BY_BAT.txt" echo installed %date% %time%
echo.
echo  ГОТОВО. Запусти PEAK: главное меню будет в стиле BACKROOMS: PEAK.
echo  В забеге: F5 - меню мода, вкладка "Backrooms x16".
echo  Чтобы убрать мод: запусти UNINSTALL.bat.
echo.
pause
