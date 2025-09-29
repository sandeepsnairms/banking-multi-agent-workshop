# PowerShell script for testing MCP Server with SSE support
Write-Host "Testing MCP Server with SSE support..." -ForegroundColor Green

$BASE_URL = "http://localhost:5000"

# Use correct client credentials that match the OAuth controller
$CLIENT_ID = "coordinator-agent-client"
$CLIENT_SECRET = "coordinator-secret-key-2024"
$SCOPE = "mcp:tools"

Write-Host "`nUsing OAuth credentials:" -ForegroundColor Yellow
Write-Host "Client ID: $CLIENT_ID" -ForegroundColor White
Write-Host "Scope: $SCOPE" -ForegroundColor White

Write-Host "`n1. Testing server availability..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-RestMethod -Uri "$BASE_URL/health" -Method Get -TimeoutSec 5
    Write-Host "Server is running!" -ForegroundColor Green
    Write-Host ($healthResponse | ConvertTo-Json -Depth 3) -ForegroundColor Cyan
}
catch {
    Write-Host "Error: Server is not responding at $BASE_URL" -ForegroundColor Red
    Write-Host "Make sure the MCP server is running with: dotnet run" -ForegroundColor Yellow
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n2. Getting OAuth token..." -ForegroundColor Yellow

# Prepare the form data for OAuth token request
$tokenBody = @{
    grant_type = "client_credentials"
    client_id = $CLIENT_ID
    client_secret = $CLIENT_SECRET
    scope = $SCOPE
}

try {
    # Get OAuth token
    $tokenResponse = Invoke-RestMethod -Uri "$BASE_URL/oauth/token" `
        -Method Post `
        -ContentType "application/x-www-form-urlencoded" `
        -Body $tokenBody `
        -TimeoutSec 10

    Write-Host "Token response received successfully!" -ForegroundColor Green
    $ACCESS_TOKEN = $tokenResponse.access_token

    if (-not $ACCESS_TOKEN) {
        Write-Host "Failed to get access token from response" -ForegroundColor Red
        Write-Host "Token response: $($tokenResponse | ConvertTo-Json)" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Access token obtained: $($ACCESS_TOKEN.Substring(0, [Math]::Min(20, $ACCESS_TOKEN.Length)))..." -ForegroundColor Green
    Write-Host "Token expires in: $($tokenResponse.expires_in) seconds" -ForegroundColor Green
}
catch {
    Write-Host "Error getting OAuth token: $($_.Exception.Message)" -ForegroundColor Red
    
    # Additional debugging information
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode
        Write-Host "HTTP Status Code: $statusCode" -ForegroundColor Red
        
        try {
            $errorResponse = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorResponse)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Error response body: $errorBody" -ForegroundColor Red
        }
        catch {
            Write-Host "Could not read error response" -ForegroundColor Yellow
        }
    }
    
    Write-Host "`nTroubleshooting tips:" -ForegroundColor Yellow
    Write-Host "1. Make sure the MCP server is running on $BASE_URL" -ForegroundColor White
    Write-Host "2. Check if the OAuth endpoint is accessible: $BASE_URL/oauth/token" -ForegroundColor White
    Write-Host "3. Verify the client credentials in the server configuration" -ForegroundColor White
    exit 1
}

Write-Host "`n3. Testing MCP capabilities endpoint..." -ForegroundColor Yellow
try {
    $capabilitiesResponse = Invoke-RestMethod -Uri "$BASE_URL/mcp/capabilities" -Method Get
    Write-Host "MCP Capabilities:" -ForegroundColor Green
    Write-Host ($capabilitiesResponse | ConvertTo-Json -Depth 3) -ForegroundColor Cyan
}
catch {
    Write-Host "Error testing capabilities endpoint: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n4. Testing OAuth client list endpoint..." -ForegroundColor Yellow
try {
    $clientsResponse = Invoke-RestMethod -Uri "$BASE_URL/oauth/clients" -Method Get
    Write-Host "Available OAuth clients:" -ForegroundColor Green
    Write-Host ($clientsResponse | ConvertTo-Json -Depth 3) -ForegroundColor Cyan
}
catch {
    Write-Host "Error testing clients endpoint: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n5. Testing MCP tools list (REST API)..." -ForegroundColor Yellow
try {
    $headers = @{
        "Authorization" = "Bearer $ACCESS_TOKEN"
        "Content-Type" = "application/json"
    }
    
    $toolsResponse = Invoke-RestMethod -Uri "$BASE_URL/mcp/tools/list" `
        -Method Post `
        -Headers $headers `
        -Body "{}"
    
    Write-Host "Available tools:" -ForegroundColor Green
    Write-Host ($toolsResponse | ConvertTo-Json -Depth 4) -ForegroundColor Cyan
}
catch {
    Write-Host "Error testing tools list: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n6. Testing MCP protocol endpoint..." -ForegroundColor Yellow
try {
    $mcpMessage = @{
        jsonrpc = "2.0"
        method = "tools/list"
        id = "test-1"
    } | ConvertTo-Json

    $mcpResponse = Invoke-RestMethod -Uri "$BASE_URL/mcp" `
        -Method Post `
        -Headers $headers `
        -Body $mcpMessage
    
    Write-Host "MCP Protocol Response:" -ForegroundColor Green
    Write-Host ($mcpResponse | ConvertTo-Json -Depth 4) -ForegroundColor Cyan
}
catch {
    Write-Host "Error testing MCP protocol endpoint: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n7. Testing tool execution..." -ForegroundColor Yellow
try {
    $toolCallMessage = @{
        jsonrpc = "2.0"
        method = "tools/call"
        id = "test-2"
        params = @{
            name = "SearchOffers"
            arguments = @{
                query = "high interest savings"
            }
        }
    } | ConvertTo-Json -Depth 3

    $toolResponse = Invoke-RestMethod -Uri "$BASE_URL/mcp" `
        -Method Post `
        -Headers $headers `
        -Body $toolCallMessage
    
    Write-Host "Tool Execution Response:" -ForegroundColor Green
    Write-Host ($toolResponse | ConvertTo-Json -Depth 4) -ForegroundColor Cyan
}
catch {
    Write-Host "Error testing tool execution: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n8. Testing Server-Sent Events endpoint..." -ForegroundColor Yellow
Write-Host "Method 1: Using HttpClient (PowerShell 6+ / .NET Core approach)" -ForegroundColor Yellow

try {
    # Use HttpClient approach for better SSE support
    Add-Type -AssemblyName System.Net.Http
    
    $httpClient = New-Object System.Net.Http.HttpClient
    $httpClient.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $ACCESS_TOKEN)
    $httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache")
    $httpClient.DefaultRequestHeaders.Accept.Add((New-Object System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream")))
    
    Write-Host "Connecting to SSE stream using HttpClient..." -ForegroundColor Yellow
    
    $cancellationTokenSource = New-Object System.Threading.CancellationTokenSource
    $cancellationTokenSource.CancelAfter([TimeSpan]::FromSeconds(10))
    
    $response = $httpClient.GetAsync("$BASE_URL/mcp/sse", [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead, $cancellationTokenSource.Token).Result
    
    if ($response.IsSuccessStatusCode) {
        Write-Host "? SSE connection established successfully!" -ForegroundColor Green
        Write-Host "Response Status: $($response.StatusCode)" -ForegroundColor White
        Write-Host "Content Type: $($response.Content.Headers.ContentType)" -ForegroundColor White
        
        $stream = $response.Content.ReadAsStreamAsync().Result
        $reader = New-Object System.IO.StreamReader($stream)
        
        $lineCount = 0
        $startTime = Get-Date
        
        Write-Host "SSE Stream Output (first 15 lines or 8 seconds):" -ForegroundColor Green
        while ($lineCount -lt 15 -and ((Get-Date) - $startTime).TotalSeconds -lt 8) {
            try {
                if ($reader.Peek() -ge 0) {
                    $line = $reader.ReadLine()
                    if ($line) {
                        Write-Host $line -ForegroundColor Cyan
                        $lineCount++
                    }
                }
                else {
                    Start-Sleep -Milliseconds 100
                }
            }
            catch {
                break
            }
        }
        
        $reader.Close()
        $stream.Close()
    }
    else {
        Write-Host "? SSE connection failed with status: $($response.StatusCode)" -ForegroundColor Red
    }
    
    $response.Dispose()
    $httpClient.Dispose()
    
    Write-Host "SSE HttpClient test completed" -ForegroundColor Green
}
catch {
    Write-Host "Error with HttpClient SSE test: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "Inner exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}

Write-Host "`n9. Testing SSE with WebRequest (Alternative method)..." -ForegroundColor Yellow
try {
    # Alternative approach using WebRequest with proper Accept header handling
    $request = [System.Net.HttpWebRequest]::Create("$BASE_URL/mcp/sse")
    $request.Method = "GET"
    $request.Headers.Add("Authorization", "Bearer $ACCESS_TOKEN")
    $request.Headers.Add("Cache-Control", "no-cache")
    
    # Use the Accept property instead of Headers.Add for Accept header
    $request.Accept = "text/event-stream"
    $request.Timeout = 8000  # 8 seconds
    
    Write-Host "Connecting to SSE stream using WebRequest..." -ForegroundColor Yellow
    
    $response = $request.GetResponse()
    Write-Host "? SSE WebRequest connection established!" -ForegroundColor Green
    Write-Host "Content Type: $($response.ContentType)" -ForegroundColor White
    
    $stream = $response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    
    $startTime = Get-Date
    $lineCount = 0
    
    Write-Host "SSE WebRequest Output (first 10 lines or 6 seconds):" -ForegroundColor Green
    while (((Get-Date) - $startTime).TotalSeconds -lt 6 -and $lineCount -lt 10) {
        try {
            $line = $reader.ReadLine()
            if ($line) {
                Write-Host $line -ForegroundColor Cyan
                $lineCount++
            }
        }
        catch {
            break
        }
    }
    
    $reader.Close()
    $response.Close()
    
    Write-Host "SSE WebRequest test completed successfully" -ForegroundColor Green
}
catch {
    Write-Host "Error with WebRequest SSE test: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n10. Testing with curl (if available)..." -ForegroundColor Yellow
try {
    $curlAvailable = $false
    try {
        $curlVersion = & curl --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            $curlAvailable = $true
        }
    }
    catch {
        # curl not available
    }
    
    if ($curlAvailable) {
        Write-Host "Curl is available. Testing SSE with curl for 5 seconds..." -ForegroundColor Green
        
        $curlArgs = @(
            "-s", "-N"
            "$BASE_URL/mcp/sse"
            "-H", "Authorization: Bearer $ACCESS_TOKEN"
            "-H", "Accept: text/event-stream"
            "-H", "Cache-Control: no-cache"
            "--max-time", "5"
        )
        
        Write-Host "Curl SSE Output:" -ForegroundColor Green
        & curl @curlArgs | Select-Object -First 10
        Write-Host "Curl SSE test completed" -ForegroundColor Green
    }
    else {
        Write-Host "Curl not available, skipping curl-based SSE test" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "Curl test error: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n? Testing complete!" -ForegroundColor Green
Write-Host "`nSummary:" -ForegroundColor Yellow
Write-Host "- Server availability: ? Tested" -ForegroundColor White
Write-Host "- OAuth authentication: ? Tested" -ForegroundColor White
Write-Host "- MCP capabilities: ? Tested" -ForegroundColor White
Write-Host "- OAuth clients list: ? Tested" -ForegroundColor White
Write-Host "- REST API tools list: ? Tested" -ForegroundColor White
Write-Host "- MCP protocol messages: ? Tested" -ForegroundColor White
Write-Host "- Tool execution: ? Tested" -ForegroundColor White
Write-Host "- Server-Sent Events (HttpClient): ? Tested" -ForegroundColor White
Write-Host "- Server-Sent Events (WebRequest): ? Tested" -ForegroundColor White
Write-Host "- Server-Sent Events (Curl): ? Tested (if available)" -ForegroundColor White

Write-Host "`nTo run this script:" -ForegroundColor Cyan
Write-Host "1. Start the MCP server: dotnet run (in MCPServer directory)" -ForegroundColor White
Write-Host "2. Run this script: .\test-mcp-sse.ps1" -ForegroundColor White
Write-Host "3. Make sure PowerShell execution policy allows scripts" -ForegroundColor White

Write-Host "`nSSE Testing Methods Used:" -ForegroundColor Yellow
Write-Host "- HttpClient with proper Accept header (.NET approach)" -ForegroundColor White
Write-Host "- WebRequest with Accept property (Classic approach)" -ForegroundColor White
Write-Host "- Curl command line tool (if available)" -ForegroundColor White

Write-Host "`nIf SSE tests show heartbeat messages, the SSE implementation is working correctly!" -ForegroundColor Green