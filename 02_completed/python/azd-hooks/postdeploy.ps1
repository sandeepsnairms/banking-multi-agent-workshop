Param(
    [parameter(Mandatory=$false)][string]$webAppName=$env:WEB_APP_NAME,
    [parameter(Mandatory=$false)][string]$resourceGroup=$env:RG_NAME,
	[parameter(Mandatory=$false)][string]$webAppUrl=$env:FRONTENDPOINT_URL,
    [parameter(Mandatory=$false)][string]$apiUrl=$env:SERVICE_ChatAPI_ENDPOINT_URL,
    [parameter(Mandatory=$false)][string]$mcpServerUrl=$env:SERVICE_MCPSERVER_ENDPOINT_URL
)

# Check if API URL is provided
if (-not $apiUrl) {
    Write-Host "Error: API URL is required."
    exit 1
}

# Function to update .env files with complete Azure configuration
function Update-EnvFile {
    param (
        [string]$mcpServerUrl,
        [string]$targetPath = ".",
        [bool]$includeMcpClient = $true
    )
    
    $envFilePath = "$targetPath\.env"
    Write-Host "Updating .env file at: $envFilePath"
    
    # Read and preserve non-Azure environment variables
    $preservedContent = @()
    if (Test-Path $envFilePath) {
        $rawContent = Get-Content $envFilePath -Raw
        if ($rawContent) {
            # Simple approach: split on quotes followed by uppercase letters (common .env pattern)
            # This handles both normal files and malformed single-line files
            $potentialVars = $rawContent -split '(?="[A-Z_]+=)|(?<="[^"]*")(?=[A-Z_]+=)'
            
            foreach ($var in $potentialVars) {
                $cleaned = $var.Trim().TrimStart('"')
                if ($cleaned -match '^[A-Z_]+=' -and $cleaned -match '=.*') {
                    # Check if this is NOT an Azure/MCP variable we want to replace
                    $varName = ($cleaned -split '=')[0]
                    if ($varName -notin @('COSMOSDB_ENDPOINT', 'AZURE_OPENAI_ENDPOINT', 'AZURE_OPENAI_EMBEDDINGDEPLOYMENTID', 'AZURE_OPENAI_COMPLETIONSDEPLOYMENTID', 'APPLICATIONINSIGHTS_CONNECTION_STRING', 'MCP_SERVER_ENDPOINT', 'USE_REMOTE_MCP_SERVER', 'AZURE_OPENAI_API_VERSION', 'MCP_AUTH_SECRET_KEY')) {
                        $preservedContent += $cleaned
                    }
                }
            }
        }
    }
    
    # Start with preserved content (like LANGCHAIN variables)
    $envContent = $preservedContent
    
    # Add Azure service configuration (available to all components)
    $envContent += "COSMOSDB_ENDPOINT=`"$env:COSMOSDB_ENDPOINT`""
    $envContent += "AZURE_OPENAI_ENDPOINT=`"$env:AZURE_OPENAI_ENDPOINT`""
    $envContent += "AZURE_OPENAI_EMBEDDINGDEPLOYMENTID=`"$env:AZURE_OPENAI_EMBEDDINGDEPLOYMENTID`""
    $envContent += "AZURE_OPENAI_COMPLETIONSDEPLOYMENTID=`"$env:AZURE_OPENAI_COMPLETIONSDEPLOYMENTID`""
    $envContent += "APPLICATIONINSIGHTS_CONNECTION_STRING=`"$env:APPLICATIONINSIGHTS_CONNECTION_STRING`""
    $envContent += "AZURE_OPENAI_API_VERSION=`"2024-02-15-preview`""
    
    # Add MCP client configuration (only for Python API)
    if ($includeMcpClient -and $mcpServerUrl) {
        $envContent += "MCP_SERVER_ENDPOINT=`"$mcpServerUrl`""
        $envContent += "USE_REMOTE_MCP_SERVER=`"true`""
    }
    
    # Add MCP server-specific configuration (only for MCP server)
    if (-not $includeMcpClient) {
        $envContent += "MCP_AUTH_SECRET_KEY=`"banking-mcp-server-jwt-secret-for-local-development`""
    }
    
    # Write back to .env file with explicit newlines
    $envString = $envContent -join "`n"
    $envString | Out-File -FilePath $envFilePath -Encoding utf8 -Force -NoNewline
    # Add a final newline
    "`n" | Out-File -FilePath $envFilePath -Encoding utf8 -Force -Append -NoNewline
    
    Write-Host "Updated .env file at $envFilePath"
    if ($includeMcpClient -and $mcpServerUrl) {
        Write-Host "  - Added MCP client configuration: $mcpServerUrl"
    }
    Write-Host "  - Added Azure service endpoints"
}

# Function to upload frontend app
function Build-And-Deploy-Frontend {
    param (
        [string]$frontendPath = "..\..\frontend",
        [string]$webAppName,
        [string]$resourceGroup,
        [string]$buildOutput = "dist\multi-agent-app"  # Change if the output folder is different
    )

    if (-not $webAppName -or -not $resourceGroup) {
        Write-Host "Error: Web App name and Resource Group are required."
        return
    }
		
    # Switching to frontend directory
    Set-Location -Path $frontendPath
	
	# Setting ChatServiceWebApi URL in frontend app
	$envContent = "export const environment = { apiUrl: '$apiUrl/' };"
	$envFilePath = "src\environments\environment.prod.ts"  # Relative to frontend directory

	$envContent | Out-File -FilePath $envFilePath -Encoding utf8 -Force

	
    Write-Host "Installing dependencies..."
    npm install
	
    Write-Host "Building frontend..."
	ng build --configuration=production

    # Use cross-platform path joining
    $zipFile = Join-Path "dist" "app.zip"
    $buildOutputPath = Join-Path "dist" "multi-agent-app"
    
    # Check if the standard build output exists, fallback to different structure
    if (-not (Test-Path $buildOutputPath)) {
        $buildOutputPath = "dist"
        Write-Host "Using alternate build output path: $buildOutputPath"
    }
    
    # Ensure dist directory exists
    if (-not (Test-Path "dist")) {
        New-Item -ItemType Directory -Path "dist" -Force | Out-Null
    }
	
	#Delete the existing zip file if it exists
	 if (Test-Path $zipFile) {
		Remove-Item $zipFile -Force
	 }
	
	# Find the actual browser output directory (Angular 17+ uses different structure)
	$browserPath = $null
	if (Test-Path (Join-Path $buildOutputPath "browser")) {
	    $browserPath = Join-Path $buildOutputPath "browser"
	} elseif (Test-Path $buildOutputPath) {
	    # Check if build output is directly in dist folder
	    $browserPath = $buildOutputPath
	}
	
	if ($browserPath -and (Test-Path $browserPath)) {
	    Write-Host "Compressing build output from: $browserPath"
	    # Use cross-platform path with Get-ChildItem for better compatibility
	    $filesToCompress = Get-ChildItem -Path $browserPath -Recurse -File
	    if ($filesToCompress.Count -gt 0) {
	        Compress-Archive -Path (Join-Path $browserPath "*") -DestinationPath $zipFile -Force
	        Write-Host "Created zip file: $zipFile"
	    } else {
	        Write-Error "No files found in build output directory: $browserPath"
	        return
	    }
	} else {
	    Write-Error "Build output directory not found. Expected at: $buildOutputPath"
	    Write-Host "Available directories in dist:"
	    if (Test-Path "dist") {
	        Get-ChildItem "dist" -Directory | ForEach-Object { Write-Host "  - $($_.Name)" }
	    }
	    return
	}

	Write-Host "Deploying to Azure Web App..."
	az webapp deploy --resource-group $resourceGroup --name $webAppName --src-path $zipFile --type zip --only-show-errors
}

# Function to send data to API
function Send-Data($jsonFilePath, $endpoint) {
    if (-Not (Test-Path $jsonFilePath)) {
        Write-Host "Error: JSON file $jsonFilePath not found. Skipping."
        return
    }
    
    $apiEndpointUrl = "$apiUrl/$endpoint"
    
    # Read the JSON file as UTF-8 and parse it
    $jsonContent = Get-Content -Path $jsonFilePath -Raw -Encoding UTF8
    $jsonArray = $jsonContent | ConvertFrom-Json
    
    foreach ($item in $jsonArray) {
        try {
            $response = Invoke-RestMethod -Uri $apiEndpointUrl -Method Put -Body ($item | ConvertTo-Json -Depth 10) -ContentType "application/json"
            Write-Output "PUT $endpoint Request Successful: $response"
        } catch {
            Write-Output "Error: $_"
        }
        Start-Sleep -Milliseconds 500  # Sleep for 0.5 seconds
    }
}

# Ask user if they want to add dummy data
$dummyDataResponse = Read-Host "Do you want to add some dummy data for testing? (yes/no)"
if ($dummyDataResponse -match "^(yes|y)$") {
    Write-Host "Adding dummy data..."
    # Load dummy data
    Send-Data "../data/UserData.json" "userdata"
	Send-Data "../data/AccountsData.json" "accountdata"
	Send-Data "../data/OffersData.json" "offerdata"
}
# Update .env files in both python and mcpserver folders
Write-Host ""
Write-Host "Updating .env files with Azure and MCP configuration..."

# Update Python API .env file (with MCP client configuration)
Update-EnvFile -mcpServerUrl $mcpServerUrl -targetPath "." -includeMcpClient $true

# Update MCP Server .env file (Azure services only, no MCP client config)
# Only update root location - src/.env is redundant
Update-EnvFile -mcpServerUrl $mcpServerUrl -targetPath "..\..\mcpserver" -includeMcpClient $false

Write-Host "Both .env files updated successfully"

Write-Host ""
# Ask user if they want to deploy frontend
$dummyDataResponse = Read-Host "Do you want to deploy the frontend app? (yes/no)"
if ($dummyDataResponse -match "^(yes|y)$") {
	Write-Host ""
    Write-Host "***FRONTEND APP deployment started!***"
	# Deploy frontend
	Build-And-Deploy-Frontend -webAppName $webAppName -resourceGroup $resourceGroup
	
	Write-Host ""
	Write-Host "Deployment complete. You can visit your app at : $webAppUrl"
}

Write-Host ""
Write-Host "Post-deployment configuration complete."
if ($mcpServerUrl) {
    Write-Host "MCP Server is available at: $mcpServerUrl"
}