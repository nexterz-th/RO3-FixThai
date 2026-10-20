@echo off
rem RO3-FixThai: builds the Thai text map from your own game and puts it where RO3Emu reads it.
rem Set the three folders below, then run this file. It needs the .NET 10 SDK.
setlocal

rem The game folder (the one with ro3.exe). The client must ship Localization_th (a Thai/SEA client).
set GAME=C:\Ragnarok3Online\Client
rem The RO3Emu folder (the one with RO3Emu.exe).
set RO3EMU=C:\Ragnarok3Online\RO3Emu
rem An ro3emu-server checkout (git clone https://github.com/aethai/ro3emu-server).
set RO3EMU_SRC=C:\WebApp\ro3emu-server

set HERE=%~dp0
set ENGLISH=
if exist "%RO3EMU%\data\loc_en.tsv" set ENGLISH=--english "%RO3EMU%\data\loc_en.tsv"

dotnet run -c Release --project "%HERE%tools\ThaiLoc" -p:Ro3EmuSrc="%RO3EMU_SRC%" -- "%GAME%" "%HERE%out\loc_th.tsv" %ENGLISH% --extra "%HERE%translations\extra_th.tsv"
if errorlevel 1 goto failed

rem RO3Emu.exe join copies data\loc_en.tsv next to the game; the game reads ro3emu\loc_en.tsv.
if exist "%RO3EMU%\data\loc_en.tsv" if not exist "%RO3EMU%\data\loc_en.tsv.english" copy /y "%RO3EMU%\data\loc_en.tsv" "%RO3EMU%\data\loc_en.tsv.english" >nul
copy /y "%HERE%out\loc_th.tsv" "%RO3EMU%\data\loc_en.tsv" >nul
if not exist "%GAME%\ro3emu" mkdir "%GAME%\ro3emu"
copy /y "%HERE%out\loc_th.tsv" "%GAME%\ro3emu\loc_en.tsv" >nul
echo.
echo Thai installed. Start the game with RO3Emu.exe join ^<server^> as usual.
pause
exit /b 0

:failed
echo.
echo ThaiLoc failed: see the message above.
pause
exit /b 1
