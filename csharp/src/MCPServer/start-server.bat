@echo off
echo Starting MCP Server...
echo ========================

REM Check if .NET 9 is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo .NET is not installed. Please install .NET 9 SDK.
    pause
    exit /b 1
)

REM Check if port 5000 is in use and offer to kill the process
echo Checking if port 5000 is available...
netstat -ano | findstr :5000 >nul 2>&1
if %errorlevel% equ 0 (
    echo Port 5000 is currently in use.
    echo The server will try to use an alternative port automatically.
    echo If you want to free up port 5000, you can run: netstat -ano ^| findstr :5000
    echo Then use: taskkill /PID [PID_NUMBER] /F
    echo.
)

REM Build the project
echo Building MCPServer...
dotnet build

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build successful!
echo.

REM Start the server
echo Starting server...
echo The server will attempt to use port 5000, or find an available alternative port.
echo Server URLs will be displayed once the server starts.
echo.
echo Press Ctrl+C to stop the server
echo.

dotnet run

REM Check if the server started successfully
if %errorlevel% neq 0 (
    echo.
    echo Server failed to start. Common issues:
    echo - Port conflicts: Another service might be using the required ports
    echo - Missing dependencies: Ensure all NuGet packages are restored
    echo - Configuration issues: Check appsettings.json
    echo.
    pause
    exit /b 1
)

echo Server stopped.
pause