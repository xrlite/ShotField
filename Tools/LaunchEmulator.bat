@echo off
rem PICO emulator launcher. All paths come from environment variables with
rem defaults derived from %USERPROFILE% / %LOCALAPPDATA%, so a fresh clone
rem works without editing this file and no local username is baked in.
rem
rem Override when needed:
rem   set PICO_HOME=<dir holding .pico>            default %USERPROFILE%\.pico
rem   set PICO_SDK_EMULATOR=<...>\sdk\<ver>\emulator  default %LOCALAPPDATA%\PICO\sdk\6.2\emulator
rem   set PICO_AVD_NAME=<avd>                      default Pico_ShotField_G0
setlocal

if "%PICO_HOME%"=="" set "PICO_HOME=%USERPROFILE%\.pico"
if "%PICO_SDK_EMULATOR%"=="" set "PICO_SDK_EMULATOR=%LOCALAPPDATA%\PICO\sdk\6.2\emulator"
if "%PICO_AVD_NAME%"=="" set "PICO_AVD_NAME=Pico_ShotField_G0"

set "ANDROID_AVD_HOME=%PICO_HOME%\avd"
set "ANDROID_SDK_ROOT=%PICO_SDK_EMULATOR%\system-images"
set "ANDROID_SDK_HOME=%PICO_HOME%"

if not exist "%PICO_SDK_EMULATOR%\emulator.exe" (
    echo [LaunchEmulator] emulator.exe not found under "%PICO_SDK_EMULATOR%"
    echo                Run:  dir "%LOCALAPPDATA%\PICO\sdk"
    echo                then set PICO_SDK_EMULATOR to the emulator folder of your SDK version.
    exit /b 2
)

start "" "%PICO_SDK_EMULATOR%\emulator.exe" -avd "%PICO_AVD_NAME%"
