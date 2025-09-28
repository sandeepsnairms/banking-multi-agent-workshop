@echo off
echo ?? Simple OAuth 2.0 MCP Server Test
echo ==================================

set BASE_URL=http://localhost:5000

echo.
echo ?? Step 1: Get server information
echo Command: curl -s "%BASE_URL%/info"
echo.
curl -s "%BASE_URL%/info"
echo.
echo.

echo ?? Step 2: Get OAuth token (save output to use manually)
echo Command: curl -X POST "%BASE_URL%/oauth/token" -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=client_credentials&client_id=coordinator-agent-client&client_secret=coordinator-secret-2024&scope=mcp:tools:coordinator"
echo.
curl -X POST "%BASE_URL%/oauth/token" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "grant_type=client_credentials&client_id=coordinator-agent-client&client_secret=coordinator-secret-2024&scope=mcp:tools:coordinator"
echo.
echo.

echo ?? Step 3: Manual testing instructions
echo.
echo Copy the "access_token" value from above (without quotes)
echo Then replace YOUR_TOKEN in the commands below:
echo.
echo List tools:
echo curl -X POST "%BASE_URL%/mcp/tools/list" -H "Authorization: Bearer YOUR_TOKEN" -H "Content-Type: application/json"
echo.
echo Test Echo:
echo curl -X POST "%BASE_URL%/mcp/tools/call" -H "Authorization: Bearer YOUR_TOKEN" -H "Content-Type: application/json" -d "{\"name\": \"Echo\", \"arguments\": {\"message\": \"Hello OAuth!\"}}"
echo.
echo Test ReverseEcho:
echo curl -X POST "%BASE_URL%/mcp/tools/call" -H "Authorization: Bearer YOUR_TOKEN" -H "Content-Type: application/json" -d "{\"name\": \"ReverseEcho\", \"arguments\": {\"message\": \"Test 123\"}}"
echo.
echo Validate token:
echo curl -X POST "%BASE_URL%/oauth/validate" -H "Authorization: Bearer YOUR_TOKEN"
echo.
echo Test without token (should fail):
echo curl -X POST "%BASE_URL%/mcp/tools/list" -H "Content-Type: application/json"
echo.
echo.

echo ?? Other OAuth clients:
echo.
echo Customer agent:
echo curl -X POST "%BASE_URL%/oauth/token" -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=client_credentials&client_id=customer-agent-client&client_secret=customer-secret-2024&scope=mcp:tools:customer"
echo.
echo Sales agent:
echo curl -X POST "%BASE_URL%/oauth/token" -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=client_credentials&client_id=sales-agent-client&client_secret=sales-secret-2024&scope=mcp:tools:sales"
echo.
echo Transactions agent:
echo curl -X POST "%BASE_URL%/oauth/token" -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=client_credentials&client_id=transactions-agent-client&client_secret=transactions-secret-2024&scope=mcp:tools:transactions"
echo.

pause