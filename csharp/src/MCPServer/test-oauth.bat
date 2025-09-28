@echo off
setlocal enabledelayedexpansion

echo ?? Testing OAuth 2.0-Only MCP Server
echo ====================================

set BASE_URL=http://localhost:5000

echo ?? 1. Getting server information...
curl -s "%BASE_URL%/info"
echo.
echo.

echo ?? 2. Getting OAuth token for coordinator agent...
curl -s -X POST "%BASE_URL%/oauth/token" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "grant_type=client_credentials" ^
  -d "client_id=coordinator-agent-client" ^
  -d "client_secret=coordinator-secret-2024" ^
  -d "scope=mcp:tools:coordinator" > token_response.json

echo OAuth Response:
type token_response.json
echo.

REM Use PowerShell to parse JSON properly
for /f "usebackq delims=" %%i in (`powershell -command "(Get-Content token_response.json | ConvertFrom-Json).access_token"`) do set ACCESS_TOKEN=%%i

if "!ACCESS_TOKEN!"=="" (
    echo ? Failed to get OAuth token
    echo Trying alternative parsing method...
    
    REM Fallback method using findstr
    for /f "tokens=2 delims=:, " %%a in ('findstr "access_token" token_response.json') do (
        set TOKEN_RAW=%%a
        set TOKEN_RAW=!TOKEN_RAW:"=!
        set ACCESS_TOKEN=!TOKEN_RAW!
    )
)

if "!ACCESS_TOKEN!"=="" (
    echo ? Still failed to parse OAuth token
    echo Please check if the server is running and OAuth endpoint is working
    pause
    exit /b 1
)

echo ? Got OAuth token: !ACCESS_TOKEN:~0,50!...
echo.

echo ?? 3. Listing MCP tools with OAuth token...
curl -s -X POST "%BASE_URL%/mcp/tools/list" ^
  -H "Authorization: Bearer !ACCESS_TOKEN!" ^
  -H "Content-Type: application/json"
echo.
echo.

echo ?? 4. Testing Echo tool with OAuth...
curl -s -X POST "%BASE_URL%/mcp/tools/call" ^
  -H "Authorization: Bearer !ACCESS_TOKEN!" ^
  -H "Content-Type: application/json" ^
  -d "{\"name\": \"Echo\", \"arguments\": {\"message\": \"OAuth 2.0 Authentication Working!\"}}"
echo.
echo.

echo ?? 5. Testing ReverseEcho tool with OAuth...
curl -s -X POST "%BASE_URL%/mcp/tools/call" ^
  -H "Authorization: Bearer !ACCESS_TOKEN!" ^
  -H "Content-Type: application/json" ^
  -d "{\"name\": \"ReverseEcho\", \"arguments\": {\"message\": \"OAuth Test\"}}"
echo.
echo.

echo ? 6. Validating OAuth token...
curl -s -X POST "%BASE_URL%/oauth/validate" ^
  -H "Authorization: Bearer !ACCESS_TOKEN!"
echo.
echo.

echo ?? 7. Testing without token (should fail)...
curl -s -X POST "%BASE_URL%/mcp/tools/list" ^
  -H "Content-Type: application/json"
echo.
echo.

echo ?? 8. Listing available OAuth clients...
curl -s "%BASE_URL%/oauth/clients"
echo.
echo.

echo ?? OAuth 2.0-only MCP Server testing completed!
echo.
echo ?? To test other agents, use their respective client credentials:
echo    • Customer: customer-agent-client / customer-secret-2024
echo    • Sales: sales-agent-client / sales-secret-2024
echo    • Transactions: transactions-agent-client / transactions-secret-2024
echo.
echo ?? Example commands for other agents:
echo.
echo For Customer agent:
echo curl -X POST %BASE_URL%/oauth/token -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=client_credentials&client_id=customer-agent-client&client_secret=customer-secret-2024&scope=mcp:tools:customer"
echo.
echo For Sales agent:
echo curl -X POST %BASE_URL%/oauth/token -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=client_credentials&client_id=sales-agent-client&client_secret=sales-secret-2024&scope=mcp:tools:sales"
echo.

REM Clean up temp file
del token_response.json 2>nul

echo Press any key to exit...
pause >nul