@echo off
REM LlaModem NSSM Service Installer
REM Run this as Administrator after publishing to C:\LlaModem\

set SERVICE_NAME=LlaModem
set INSTALL_DIR=C:\LlaModem
set APP=%INSTALL_DIR%\LlaModem.exe
set LOG_DIR=%INSTALL_DIR%\logs

echo ============================================
echo  LlaModem Windows Service Installer
echo ============================================
echo.

REM Check if NSSM exists
if not exist "%INSTALL_DIR%\nssm.exe" (
    echo ERROR: nssm.exe not found in %INSTALL_DIR%
    echo Download from https://nssm.cc/release/nssm-2.24.zip
    goto :error
)

REM Check if app exists
if not exist "%APP%" (
    echo ERROR: LlaModem.exe not found in %INSTALL_DIR%
    echo Run 'dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o %INSTALL_DIR%' first
    goto :error
)

REM Create logs directory
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo [1/5] Installing service...
"%INSTALL_DIR%\nssm.exe" install "%SERVICE_NAME%" "%APP%"

echo [2/5] Setting auto-start...
"%INSTALL_DIR%\nssm.exe" set "%SERVICE_NAME%" Start SERVICE_AUTO_START

echo [3/5] Setting working directory...
"%INSTALL_DIR%\nssm.exe" set "%SERVICE_NAME%" Startup Directory "%INSTALL_DIR%"

echo [4/5] Configuring log rotation...
"%INSTALL_DIR%\nssm.exe" set "%SERVICE_NAME%" StdOut "%LOG_DIR%\nssm-stdout.log"
"%INSTALL_DIR%\nssm.exe" set "%SERVICE_NAME%" StdErr "%LOG_DIR%\nssm-stderr.log"
"%INSTALL_DIR%\nssm.exe" set "%SERVICE_NAME%" StdOutRotation 10485760
"%INSTALL_DIR%\nssm.exe" set "%SERVICE_NAME%" StdErrRotation 10485760

echo [5/5] Starting service...
"%INSTALL_DIR%\nssm.exe" start "%SERVICE_NAME%"

echo.
echo ============================================
echo  Service installed and started!
echo ============================================
echo.
"%INSTALL_DIR%\nssm.exe" status "%SERVICE_NAME%"
echo.
echo Manage with: nssm restart ^| stop ^| edit %SERVICE_NAME%
echo Or use services.msc to find "%SERVICE_NAME%"
goto :success

:error
exit /b 1

:success
exit /b 0
