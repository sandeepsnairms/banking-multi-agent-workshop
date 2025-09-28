# OAuth 2.0 MCP Server Test Script for Windows PowerShell
Write-Host "?? Testing OAuth 2.0 MCP Server" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

# Function to find the server URL
function Find-ServerUrl {
    $possibleUrls = @(
        "http://localhost:5000",
        "http://localhost:5001", 
        "http://localhost:5002",
        "http://localhost:5003"
    )
    
    foreach ($url in $possibleUrls) {
        try {
            Write-Host "Trying server at $url..." -ForegroundColor Gray
            $response = Invoke-RestMethod -Uri "$url/health" -Method Get -TimeoutSec 5
            Write-Host "? Server found at $url" -ForegroundColor Green
            return $url
        } catch {
            # Continue to next URL
        }
    }
    
    return $null
}

# Function to make HTTP request with proper headers
function Invoke-HttpRequest {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [string]$ContentType = "application/json"
    )
    
    try {
        $webRequest = [System.Net.HttpWebRequest]::Create($Uri)
        $webRequest.Method = $Method
        $webRequest.ContentType = $ContentType
        $webRequest.UserAgent = "PowerShell-OAuth-Test/1.0"
        
        # Add custom headers
        foreach ($header in $Headers.GetEnumerator()) {
            if ($header.Key -eq "Authorization") {
                $webRequest.Headers.Add("Authorization", $header.Value)
            } elseif ($header.Key -eq "Content-Type") {
                # Already set above
            } else {
                $webRequest.Headers.Add($header.Key, $header.Value)
            }
        }
        
        # Add body if provided
        if ($Body) {
            $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($Body)
            $webRequest.ContentLength = $bodyBytes.Length
            $requestStream = $webRequest.GetRequestStream()
            $requestStream.Write($bodyBytes, 0, $bodyBytes.Length)
            $requestStream.Close()
        } else {
            $webRequest.ContentLength = 0
        }
        
        $response = $webRequest.GetResponse()
        $responseStream = $response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($responseStream)
        $content = $reader.ReadToEnd()
        $reader.Close()
        $response.Close()
        
        return $content | ConvertFrom-Json
    } catch [System.Net.WebException] {
        $errorResponse = $_.Exception.Response
        if ($errorResponse) {
            $errorStream = $errorResponse.GetResponseStream()
            $errorReader = New-Object System.IO.StreamReader($errorStream)
            $errorContent = $errorReader.ReadToEnd()
            $errorReader.Close()
            throw "HTTP Error: $($errorResponse.StatusDescription) - $errorContent"
        } else {
            throw "Network Error: $($_.Exception.Message)"
        }
    }
}

# Find the server
Write-Host "?? Looking for MCP server..." -ForegroundColor Cyan
$baseUrl = Find-ServerUrl

if ($null -eq $baseUrl) {
    Write-Host "? MCP Server not found on any common port" -ForegroundColor Red
    Write-Host "   Make sure the server is running with: dotnet run" -ForegroundColor Yellow
    Write-Host "   Or use the start-server.bat script" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Using server at: $baseUrl" -ForegroundColor Green
Write-Host ""

Write-Host "?? 1. Getting server health information..." -ForegroundColor Cyan
try {
    $serverInfo = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    Write-Host "Server Info:" -ForegroundColor Yellow
    $serverInfo | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "? Server not responding. Make sure the MCP server is running on $baseUrl" -ForegroundColor Red
    Write-Host "   Error details: $($_.Exception.Message)" -ForegroundColor Gray
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

Write-Host "?? 2. Getting OAuth token for coordinator agent..." -ForegroundColor Cyan
try {
    # Use form data for OAuth token request
    $tokenBodyString = "grant_type=client_credentials&client_id=coordinator-agent-client&client_secret=coordinator-secret-2024&scope=mcp:tools"
    
    Write-Host "   Request details:" -ForegroundColor Gray
    Write-Host "   - URL: $baseUrl/oauth/token" -ForegroundColor Gray
    Write-Host "   - Method: POST" -ForegroundColor Gray
    Write-Host "   - Content-Type: application/x-www-form-urlencoded" -ForegroundColor Gray
    
    # Use WebRequest for OAuth token to ensure it works
    $tokenRequest = [System.Net.HttpWebRequest]::Create("$baseUrl/oauth/token")
    $tokenRequest.Method = "POST"
    $tokenRequest.ContentType = "application/x-www-form-urlencoded"
    
    $tokenBodyBytes = [System.Text.Encoding]::UTF8.GetBytes($tokenBodyString)
    $tokenRequest.ContentLength = $tokenBodyBytes.Length
    $tokenRequestStream = $tokenRequest.GetRequestStream()
    $tokenRequestStream.Write($tokenBodyBytes, 0, $tokenBodyBytes.Length)
    $tokenRequestStream.Close()
    
    $tokenResponse = $tokenRequest.GetResponse()
    $tokenResponseStream = $tokenResponse.GetResponseStream()
    $tokenReader = New-Object System.IO.StreamReader($tokenResponseStream)
    $tokenContent = $tokenReader.ReadToEnd()
    $tokenReader.Close()
    $tokenResponse.Close()
    
    $tokenData = $tokenContent | ConvertFrom-Json
    $accessToken = $tokenData.access_token
    
    Write-Host "? Got OAuth token: $($accessToken.Substring(0, [Math]::Min(50, $accessToken.Length)))..." -ForegroundColor Green
    Write-Host "Token Type: $($tokenData.token_type)" -ForegroundColor Yellow
    Write-Host "Expires In: $($tokenData.expires_in) seconds" -ForegroundColor Yellow
    Write-Host "Scope: $($tokenData.scope)" -ForegroundColor Yellow
} catch {
    Write-Host "? Failed to get OAuth token" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

Write-Host "?? 3. Listing MCP tools with OAuth token..." -ForegroundColor Cyan
try {
    $headers = @{ 
        "Authorization" = "Bearer $accessToken"
    }
    
    $toolsResponse = Invoke-HttpRequest -Uri "$baseUrl/mcp/tools/list" -Method "POST" -Headers $headers
    Write-Host "Available Tools:" -ForegroundColor Yellow
    $toolsResponse | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "? Failed to list tools: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "?? 4. Testing Banking tools availability..." -ForegroundColor Cyan
try {
    $headers = @{ 
        "Authorization" = "Bearer $accessToken"
    }
    
    # Test if we have any banking tools available
    $toolsList = Invoke-HttpRequest -Uri "$baseUrl/mcp/tools/list" -Method "POST" -Headers $headers
    $bankingToolCount = $toolsList.tools.Count
    
    if ($bankingToolCount -gt 0) {
        Write-Host "? Found $bankingToolCount banking tools available" -ForegroundColor Green
        Write-Host "Banking Tools:" -ForegroundColor Yellow
        foreach ($tool in $toolsList.tools) {
            Write-Host "  - $($tool.name): $($tool.description)" -ForegroundColor White
        }
    } else {
        Write-Host "?? No banking tools found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "? Failed to test banking tools: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "? 5. Validating OAuth token..." -ForegroundColor Cyan
try {
    $headers = @{ 
        "Authorization" = "Bearer $accessToken"
    }
    
    $validateResponse = Invoke-HttpRequest -Uri "$baseUrl/oauth/validate" -Method "POST" -Headers $headers
    Write-Host "Token Validation:" -ForegroundColor Yellow
    $validateResponse | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "? Failed to validate token: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "?? 6. Testing without token (should fail)..." -ForegroundColor Cyan
try {
    $unauthorizedResponse = Invoke-HttpRequest -Uri "$baseUrl/mcp/tools/list" -Method "POST" -Headers @{}
    Write-Host "?? Unexpected: Request succeeded without token!" -ForegroundColor Yellow
    $unauthorizedResponse | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "? Expected: Request failed without token (this is correct behavior)" -ForegroundColor Green
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
}
Write-Host ""

Write-Host "?? 7. Listing available OAuth clients..." -ForegroundColor Cyan
try {
    $clientsResponse = Invoke-RestMethod -Uri "$baseUrl/oauth/clients" -Method Get
    Write-Host "OAuth Clients:" -ForegroundColor Yellow
    $clientsResponse | ConvertTo-Json -Depth 10 | Write-Host
} catch {
    Write-Host "? Failed to list OAuth clients: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "?? OAuth 2.0 MCP Server testing completed!" -ForegroundColor Green
Write-Host "Server URL was: $baseUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? To test other agents, use their respective client credentials:" -ForegroundColor Cyan
Write-Host "   • Customer: customer-agent-client / customer-secret-2024" -ForegroundColor White
Write-Host "   • Sales: sales-agent-client / sales-secret-2024" -ForegroundColor White
Write-Host "   • Transactions: transactions-agent-client / transactions-secret-2024" -ForegroundColor White
Write-Host ""

Read-Host "Press Enter to exit"