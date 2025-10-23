param(
    [Parameter(Mandatory=$true)]
    [string]$AppId,
   
    [Parameter(Mandatory=$true)]
    [string]$TenantId,
   
    [Parameter(Mandatory=$true)]
    [string]$AppSecret,
   
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionId,
   
    [Parameter(Mandatory=$true)]
    [string]$UserPrincipalName,
   
    [Parameter(Mandatory=$true)]
    [string]$UserObjectId,  # **Mandatory now** for guest MSA accounts
   
    [Parameter(Mandatory=$true)]
    [string]$LabInstanceId,
   
    [Parameter(Mandatory=$true)]
    [string]$Location,
   
    [Parameter()]
    [string]$LocalPath = 'C:\Sandy\HOL_AFandLangGraph\01_exercises',

    [Parameter()]
    [string]$DeployOpenAI = $true
)

Write-Host "Starting deployment with Service Principal..." -ForegroundColor Green

# Calculate the full path to the parameters file
$parametersFilePath = Join-Path $LocalPath "infra\main.parameters.json"
$infraDir = Join-Path $LocalPath "infra"

Write-Host "Using base directory: $LocalPath" -ForegroundColor Cyan
Write-Host "Parameters file path: $parametersFilePath" -ForegroundColor Cyan
Write-Host "Infrastructure directory: $infraDir" -ForegroundColor Cyan

try {
    # Create secure credential for Service Principal
    $secureSecret = ConvertTo-SecureString $AppSecret -AsPlainText -Force
    $spCredential = New-Object System.Management.Automation.PSCredential($AppId, $secureSecret)

    # Connect to Azure with Service Principal
    Write-Host "Connecting to Azure with Service Principal..." -ForegroundColor Yellow
    Connect-AzAccount -ServicePrincipal -Credential $spCredential -TenantId $TenantId -Subscription $SubscriptionId -SkipContextPopulation

    # Get the Service Principal Object ID for role assignments
    Write-Host "Getting Service Principal Object ID..." -ForegroundColor Yellow
    Write-Host "  Looking up Service Principal with App ID: $AppId" -ForegroundColor Cyan
    $servicePrincipal = Get-AzADServicePrincipal -ApplicationId $AppId
    if (-not $servicePrincipal) {
        throw "Could not find Service Principal with App ID: $AppId"
    }
    $servicePrincipalObjectId = $servicePrincipal.Id
    Write-Host "Found Service Principal Object ID: $servicePrincipalObjectId" -ForegroundColor Green

    Write-Host "Using provided UserObjectId: $UserObjectId" -ForegroundColor Green
    
    # Debug: Check if IDs are the same
    if ($servicePrincipalObjectId -eq $UserObjectId) {
        Write-Host "WARNING: Service Principal Object ID and User Object ID are the SAME!" -ForegroundColor Red
        Write-Host "  Service Principal Object ID: $servicePrincipalObjectId" -ForegroundColor Red
        Write-Host "  User Object ID: $UserObjectId" -ForegroundColor Red
    } else {
        Write-Host "✅ Service Principal and User Object IDs are different (expected)" -ForegroundColor Green
        Write-Host "  Service Principal Object ID: $servicePrincipalObjectId" -ForegroundColor Cyan
        Write-Host "  User Object ID: $UserObjectId" -ForegroundColor Cyan
    }

    # Set Azure context
    Set-AzContext -Subscription $SubscriptionId -Tenant $TenantId

    # Set environment variables for azd
    $env:AZURE_ENV_NAME = "agenthol-$LabInstanceId"
    $env:AZURE_LOCATION = $Location
    $env:AZURE_PRINCIPAL_ID = $servicePrincipalObjectId  # Service Principal Object ID
    $env:AZURE_CURRENT_USER_ID = $UserObjectId          # Current User Object ID
    $env:AZURE_PRINCIPAL_TYPE = "ServicePrincipal"      # Type for the Service Principal
    $env:DEPLOY_OPENAI = $DeployOpenAI.ToString().ToLower()  # Whether to deploy OpenAI
    $env:OWNER_EMAIL = $UserPrincipalName

    Write-Host "Environment variables set:" -ForegroundColor Green
    Write-Host "  AZURE_ENV_NAME: $env:AZURE_ENV_NAME"
    Write-Host "  AZURE_LOCATION: $env:AZURE_LOCATION"
    Write-Host "  AZURE_PRINCIPAL_ID (SP): $env:AZURE_PRINCIPAL_ID"
    Write-Host "  AZURE_CURRENT_USER_ID: $env:AZURE_CURRENT_USER_ID"
    Write-Host "  AZURE_PRINCIPAL_TYPE: $env:AZURE_PRINCIPAL_TYPE"
    Write-Host "  DEPLOY_OPENAI: $env:DEPLOY_OPENAI"
    Write-Host "  OWNER_EMAIL: $env:OWNER_EMAIL"

    # Update Bicep parameters file
    if (Test-Path $parametersFilePath) {
        Write-Host "Updating parameters file..." -ForegroundColor Yellow
        $FindPrincipalId = '"value": "${AZURE_PRINCIPAL_ID}"'
        $ReplacePrincipalId = '"value": "' + $servicePrincipalObjectId + '"'
        (Get-Content -Path $parametersFilePath -Raw).Replace($FindPrincipalId, $ReplacePrincipalId) | Set-Content -Path $parametersFilePath
        Write-Host "Parameters file updated successfully" -ForegroundColor Green
    } else {
        Write-Warning "Parameters file not found at: $parametersFilePath"
    }

    # Authenticate azd with Service Principal
    Write-Host "Authenticating azd with Service Principal..." -ForegroundColor Yellow
    & azd auth login --client-id $AppId --client-secret $AppSecret --tenant-id $TenantId
    if ($LASTEXITCODE -ne 0) {
        throw "azd auth login failed with exit code: $LASTEXITCODE"
    }
    Write-Host "azd authentication successful" -ForegroundColor Green

    # Check if environment exists
    $envName = "agenthol-$LabInstanceId"
    Write-Host "Checking if environment '$envName' exists..." -ForegroundColor Yellow
    $existingEnv = & azd env list --output json | ConvertFrom-Json | Where-Object { $_.Name -eq $envName }
   
    if ($existingEnv) {
        Write-Host "Environment '$envName' already exists. Using existing environment." -ForegroundColor Yellow
        & azd env select $envName
    } else {
        Write-Host "Creating new environment '$envName'..." -ForegroundColor Yellow
        & azd env new $envName --location $Location --subscription $SubscriptionId
        if ($LASTEXITCODE -ne 0) {
            throw "azd env new failed with exit code: $LASTEXITCODE"
        }
    }

    # Set azd environment variables
    Write-Host "Setting azd environment variables..." -ForegroundColor Yellow
    Write-Host "  🔹 AZURE_PRINCIPAL_ID (SP): $servicePrincipalObjectId" -ForegroundColor Cyan
    Write-Host "  🔹 AZURE_CURRENT_USER_ID: $UserObjectId" -ForegroundColor Cyan
    & azd env set AZURE_PRINCIPAL_ID $servicePrincipalObjectId   # Service Principal for deployment
    & azd env set AZURE_CURRENT_USER_ID $UserObjectId           # Current User for development
    & azd env set AZURE_PRINCIPAL_TYPE "ServicePrincipal"
    & azd env set DEPLOY_OPENAI $DeployOpenAI.ToString().ToLower()
    & azd env set OWNER_EMAIL $UserPrincipalName
    
    Write-Host "✅ All azd environment variables set successfully" -ForegroundColor Green

    # Deploy the application
    Write-Host "Starting deployment from base directory: $LocalPath" -ForegroundColor Yellow
    Set-Location $LocalPath  # Ensure we're in the base directory with azure.yaml
    & azd up -e $envName --no-prompt
    if ($LASTEXITCODE -ne 0) {
        throw "azd up failed with exit code: $LASTEXITCODE"
    }

    Write-Host "Deployment completed successfully!" -ForegroundColor Green

} catch {
    Write-Error "Deployment failed: $($_.Exception.Message)"
    Write-Error "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}