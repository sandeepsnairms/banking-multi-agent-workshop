# Quick OAuth test script for MCP Server
Write-Host "Quick OAuth Test for MCP Server" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green

$BASE_URL = "http://localhost:5000"

# Test if server is running
Write-Host "`n1. Testing if server is running..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$BASE_URL/health" -Method Get -TimeoutSec 5 -UseBasicParsing
    Write-Host "? Server is running!" -ForegroundColor Green
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor White
}
catch {
    Write-Host "? Server is not running or not accessible" -ForegroundColor Red
    Write-Host "Please start the server with: dotnet run" -ForegroundColor Yellow
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test OAuth clients endpoint
Write-Host "`n2. Getting available OAuth clients..." -ForegroundColor Yellow
try {
    $clientsResponse = Invoke-RestMethod -Uri "$BASE_URL/oauth/clients" -Method Get
    Write-Host "? Available OAuth clients:" -ForegroundColor Green
    foreach ($client in $clientsResponse.clients) {
        Write-Host "  - Client ID: $($client.clientId)" -ForegroundColor White
        Write-Host "    Scopes: $($client.allowedScopes -join ', ')" -ForegroundColor Gray
    }
}
catch {
    Write-Host "? Error getting OAuth clients: $($_.Exception.Message)" -ForegroundColor Red
}

# Test OAuth token with coordinator client
Write-Host "`n3. Testing OAuth token with coordinator client..." -ForegroundColor Yellow
$CLIENT_ID = "coordinator-agent-client"
$CLIENT_SECRET = "coordinator-secret-key-2024"
$SCOPE = "mcp:tools"

$tokenBody = @{
    grant_type = "client_credentials"
    client_id = $CLIENT_ID
    client_secret = $CLIENT_SECRET
    scope = $SCOPE
}

try {
    $tokenResponse = Invoke-RestMethod -Uri "$BASE_URL/oauth/token" `
        -Method Post `
        -ContentType "application/x-www-form-urlencoded" `
        -Body $tokenBody

    Write-Host "? OAuth token obtained successfully!" -ForegroundColor Green
    Write-Host "Token Type: $($tokenResponse.token_type)" -ForegroundColor White
    Write-Host "Expires In: $($tokenResponse.expires_in) seconds" -ForegroundColor White
    Write-Host "Scope: $($tokenResponse.scope)" -ForegroundColor White
    Write-Host "Token (first 30 chars): $($tokenResponse.access_token.Substring(0, [Math]::Min(30, $tokenResponse.access_token.Length)))..." -ForegroundColor White

    # Test token validation
    Write-Host "`n4. Testing token validation..." -ForegroundColor Yellow
    try {
        $headers = @{
            "Authorization" = "Bearer $($tokenResponse.access_token)"
        }
        
        $validateResponse = Invoke-RestMethod -Uri "$BASE_URL/oauth/validate" `
            -Method Post `
            -Headers $headers

        Write-Host "? Token validation successful!" -ForegroundColor Green
        Write-Host "Client ID: $($validateResponse.clientId)" -ForegroundColor White
        Write-Host "Scopes: $($validateResponse.scopes -join ', ')" -ForegroundColor White
    }
    catch {
        Write-Host "? Token validation failed: $($_.Exception.Message)" -ForegroundColor Red
    }

    # Test MCP tools list
    Write-Host "`n5. Testing MCP tools list..." -ForegroundColor Yellow
    try {
        $toolsResponse = Invoke-RestMethod -Uri "$BASE_URL/mcp/tools/list" `
            -Method Post `
            -Headers $headers `
            -Body "{}" `
            -ContentType "application/json"

        Write-Host "? MCP tools retrieved successfully!" -ForegroundColor Green
        Write-Host "Number of tools: $($toolsResponse.tools.Count)" -ForegroundColor White
        
        if ($toolsResponse.tools.Count -gt 0) {
            Write-Host "Available tools:" -ForegroundColor White
            foreach ($tool in $toolsResponse.tools) {
                Write-Host "  - $($tool.name): $($tool.description)" -ForegroundColor Gray
            }
        }
    }
    catch {
        Write-Host "? Error getting MCP tools: $($_.Exception.Message)" -ForegroundColor Red
    }
}
catch {
    Write-Host "? OAuth token request failed!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode
        Write-Host "Status Code: $statusCode" -ForegroundColor Red
    }
}

Write-Host "`n" -ForegroundColor White
Write-Host "Quick test completed!" -ForegroundColor Green
Write-Host "If all tests passed, you can run the full test with: .\test-mcp-sse.ps1" -ForegroundColor Yellow