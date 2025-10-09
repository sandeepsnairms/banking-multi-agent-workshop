# Test MCP Server functionality with enhanced debugging
$ErrorActionPreference = "Stop"

Write-Host "?? Testing MCP Server with Enhanced Debugging..." -ForegroundColor Green

# Test 1: Health endpoint
try {
    Write-Host "?? Testing health endpoint..." -ForegroundColor Yellow
    $healthResponse = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET
    Write-Host "? Health check passed: $($healthResponse.Status)" -ForegroundColor Green
    Write-Host "   Version: $($healthResponse.Version)" -ForegroundColor Gray
    Write-Host "   Timestamp: $($healthResponse.Timestamp)" -ForegroundColor Gray
} catch {
    Write-Host "? Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "?? Make sure MCP Server is running on port 5000" -ForegroundColor Yellow
    exit 1
}

# Test 2: MCP tools/list endpoint without API key
try {
    Write-Host "?? Testing MCP tools/list WITHOUT API key..." -ForegroundColor Yellow
    
    $mcpRequest = @{
        jsonrpc = "2.0"
        id = 1
        method = "tools/list"
        params = @{}
    } | ConvertTo-Json -Depth 10

    $headers = @{
        "Content-Type" = "application/json"
        "Accept" = "application/json"
    }

    Write-Host "?? Sending request: $mcpRequest" -ForegroundColor Gray
    $mcpResponse = Invoke-RestMethod -Uri "http://localhost:5000/mcp" -Method POST -Body $mcpRequest -Headers $headers
    
    if ($mcpResponse.result -and $mcpResponse.result.tools) {
        Write-Host "? MCP tools/list successful WITHOUT API key!" -ForegroundColor Green
        Write-Host "   Found $($mcpResponse.result.tools.Count) tools:" -ForegroundColor Gray
        foreach ($tool in $mcpResponse.result.tools) {
            Write-Host "   - $($tool.name): $($tool.description)" -ForegroundColor Gray
        }
    } else {
        Write-Host "? MCP tools/list returned unexpected format" -ForegroundColor Red
        Write-Host "Response: $($mcpResponse | ConvertTo-Json -Depth 10)" -ForegroundColor Red
    }
} catch {
    Write-Host "? MCP tools/list failed WITHOUT API key: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $errorDetails = $_.Exception.Response.Content.ReadAsStringAsync().Result
        Write-Host "Error details: $errorDetails" -ForegroundColor Red
    }
}

# Test 3: MCP tools/list endpoint WITH API key
try {
    Write-Host "?? Testing MCP tools/list WITH API key..." -ForegroundColor Yellow
    
    $mcpRequest = @{
        jsonrpc = "2.0"
        id = 2
        method = "tools/list"
        params = @{}
    } | ConvertTo-Json -Depth 10

    $headers = @{
        "Content-Type" = "application/json"
        "Accept" = "application/json"
        "X-MCP-API-Key" = "dev-mcp-api-key-12345"
    }

    Write-Host "?? Sending request with API key: $mcpRequest" -ForegroundColor Gray
    $mcpResponse = Invoke-RestMethod -Uri "http://localhost:5000/mcp" -Method POST -Body $mcpRequest -Headers $headers
    
    if ($mcpResponse.result -and $mcpResponse.result.tools) {
        Write-Host "? MCP tools/list successful WITH API key!" -ForegroundColor Green
        Write-Host "   Found $($mcpResponse.result.tools.Count) tools:" -ForegroundColor Gray
        foreach ($tool in $mcpResponse.result.tools) {
            Write-Host "   - $($tool.name): $($tool.description)" -ForegroundColor Gray
        }
    } else {
        Write-Host "? MCP tools/list returned unexpected format" -ForegroundColor Red
        Write-Host "Response: $($mcpResponse | ConvertTo-Json -Depth 10)" -ForegroundColor Red
    }
} catch {
    Write-Host "? MCP tools/list failed WITH API key: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $errorDetails = $_.Exception.Response.Content.ReadAsStringAsync().Result
        Write-Host "Error details: $errorDetails" -ForegroundColor Red
    }
}

# Test 4: Call a simple tool
try {
    Write-Host "?? Testing MCP tool call (GetCurrentDateTime)..." -ForegroundColor Yellow
    
    $toolCallRequest = @{
        jsonrpc = "2.0"
        id = 3
        method = "tools/call"
        params = @{
            name = "GetCurrentDateTime"
            arguments = @{}
        }
    } | ConvertTo-Json -Depth 10

    $headers = @{
        "Content-Type" = "application/json"
        "Accept" = "application/json"
        "X-MCP-API-Key" = "dev-mcp-api-key-12345"
    }

    Write-Host "?? Calling GetCurrentDateTime tool..." -ForegroundColor Gray
    $toolResponse = Invoke-RestMethod -Uri "http://localhost:5000/mcp" -Method POST -Body $toolCallRequest -Headers $headers
    
    if ($toolResponse.result) {
        Write-Host "? Tool call successful!" -ForegroundColor Green
        Write-Host "   Result: $($toolResponse.result | ConvertTo-Json -Depth 10)" -ForegroundColor Gray
    } else {
        Write-Host "? Tool call failed - no result" -ForegroundColor Red
        Write-Host "Response: $($toolResponse | ConvertTo-Json -Depth 10)" -ForegroundColor Red
    }
} catch {
    Write-Host "? Tool call failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $errorDetails = $_.Exception.Response.Content.ReadAsStringAsync().Result
        Write-Host "Error details: $errorDetails" -ForegroundColor Red
    }
}

Write-Host "" 
Write-Host "?? MCP Server debug testing completed!" -ForegroundColor Green
Write-Host "?? Server is running at http://localhost:5000/mcp" -ForegroundColor Cyan
Write-Host "?? Check the MCP Server console output for detailed debug logs" -ForegroundColor Yellow