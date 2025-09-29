@echo off
setlocal enabledelayedexpansion

REM Test script for MCP Server with SSE support
echo Testing MCP Server with SSE support...
echo.

set BASE_URL=http://localhost:5000
REM Use correct client credentials that match the OAuth controller
set CLIENT_ID=coordinator-agent-client
set CLIENT_SECRET=coordinator-secret-key-2024
set SCOPE=mcp:tools

echo Using OAuth credentials:
echo Client ID: %CLIENT_ID%
echo Scope: %SCOPE%
echo.

echo 1. Testing server availability...
curl -s -f "%BASE_URL%/health" >nul 2>&1
if errorlevel 1 (
    echo X Server is not running or not accessible at %BASE_URL%
    echo Please start the server with: dotnet run
    exit /b 1
)
echo + Server is running!

echo.
echo 2. Getting OAuth token...
curl -s -X POST "%BASE_URL%/oauth/token" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "grant_type=client_credentials&client_id=%CLIENT_ID%&client_secret=%CLIENT_SECRET%&scope=%SCOPE%" > token_response.json

echo Token response:
type token_response.json
echo.

echo.
echo NOTE: For the remaining tests, you'll need to extract the access_token from token_response.json
echo       and replace YOUR_ACCESS_TOKEN in the commands below.
echo.

REM For demonstration, showing the commands that would work with the token
echo 3. Testing health endpoint...
curl -s "%BASE_URL%/health"

echo.
echo 4. Testing MCP capabilities endpoint...
curl -s "%BASE_URL%/mcp/capabilities"

echo.
echo 5. Testing OAuth clients endpoint...
curl -s "%BASE_URL%/oauth/clients"

echo.
echo 6. Testing MCP tools list (REST API)...
echo curl -s -X POST "%BASE_URL%/mcp/tools/list" ^
echo   -H "Authorization: Bearer YOUR_ACCESS_TOKEN" ^
echo   -H "Content-Type: application/json" ^
echo   -d "{}"

echo.
echo 7. Testing MCP protocol endpoint...
echo curl -s -X POST "%BASE_URL%/mcp" ^
echo   -H "Authorization: Bearer YOUR_ACCESS_TOKEN" ^
echo   -H "Content-Type: application/json" ^
echo   -d "{\"jsonrpc\": \"2.0\", \"method\": \"tools/list\", \"id\": \"test-1\"}"

echo.
echo 8. Testing tool execution...
echo curl -s -X POST "%BASE_URL%/mcp" ^
echo   -H "Authorization: Bearer YOUR_ACCESS_TOKEN" ^
echo   -H "Content-Type: application/json" ^
echo   -d "{\"jsonrpc\": \"2.0\", \"method\": \"tools/call\", \"id\": \"test-2\", \"params\": {\"name\": \"SearchOffers\", \"arguments\": {\"query\": \"high interest savings\"}}}"

echo.
echo 9. Testing Server-Sent Events endpoint...
echo curl -s -N "%BASE_URL%/mcp/sse" ^
echo   -H "Authorization: Bearer YOUR_ACCESS_TOKEN" ^
echo   -H "Accept: text/event-stream"

echo.
echo Testing script completed!
echo.
echo For easier testing on Windows, use the PowerShell script instead:
echo   .\test-mcp-sse.ps1
echo   or
echo   .\test-oauth-quick.ps1
echo.
echo Available OAuth clients for testing:
echo - coordinator-agent-client / coordinator-secret-key-2024
echo - customer-agent-client / customer-secret-key-2024  
echo - sales-agent-client / sales-secret-key-2024
echo - transactions-agent-client / transactions-secret-key-2024

del token_response.json >nul 2>&1
endlocal