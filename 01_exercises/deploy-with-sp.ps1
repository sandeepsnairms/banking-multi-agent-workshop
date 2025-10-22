# Enhanced PowerShell script for Service Principal deployment with azd

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
    [string]$LabInstanceId,
    
    [Parameter(Mandatory=$true)]
    [string]$Location,
    
    [Parameter()]
    [string]$LocalPath = 'C:\Lab\01_exercises\infra\main.parameters.json'
)

Write-Host "Starting deployment with Service Principal..." -ForegroundColor Green

try {
    # Create secure credential for Service Principal
    $secureSecret = ConvertTo-SecureString $AppSecret -AsPlainText -Force
    $spCredential = New-Object System.Management.Automation.PSCredential($AppId, $secureSecret)

    # Connect to Azure with Service Principal
    Write-Host "Connecting to Azure with Service Principal..." -ForegroundColor Yellow
    Connect-AzAccount -ServicePrincipal -Credential $spCredential -TenantId $TenantId -Subscription $SubscriptionId -SkipContextPopulation

    # Get the user's Azure AD Object ID (this is what we need for role assignments)
    Write-Host "Getting Azure AD User Object ID..." -ForegroundColor Yellow
    $azureUser = Get-AzADUser -UserPrincipalName $UserPrincipalName
    if (-not $azureUser) {
        throw "Could not find user with UPN: $UserPrincipalName"
    }
    $azureUserId = $azureUser.Id
    Write-Host "Found user ID: $azureUserId" -ForegroundColor Green

    # Set Azure context
    Set-AzContext -Subscription $SubscriptionId -Tenant $TenantId

    # Set environment variables for azd
    $env:AZURE_ENV_NAME = "agenthol-$LabInstanceId"
    $env:AZURE_LOCATION = $Location
    $env:AZURE_PRINCIPAL_ID = $azureUserId
    $env:AZURE_PRINCIPAL_TYPE = "User"  # This is for the user who will use the application
    $env:OWNER_EMAIL = $UserPrincipalName

    Write-Host "Environment variables set:" -ForegroundColor Green
    Write-Host "  AZURE_ENV_NAME: $env:AZURE_ENV_NAME"
    Write-Host "  AZURE_LOCATION: $env:AZURE_LOCATION"
    Write-Host "  AZURE_PRINCIPAL_ID: $env:AZURE_PRINCIPAL_ID"
    Write-Host "  AZURE_PRINCIPAL_TYPE: $env:AZURE_PRINCIPAL_TYPE"
    Write-Host "  OWNER_EMAIL: $env:OWNER_EMAIL"

    # Modify Bicep parameters file to ensure proper principal ID is used
    if (Test-Path $LocalPath) {
        Write-Host "Updating parameters file..." -ForegroundColor Yellow
        $FindPrincipalId = '"value": "${AZURE_PRINCIPAL_ID}"'
        $ReplacePrincipalId = '"value": "' + $azureUserId + '"'
        (Get-Content -Path $LocalPath -Raw).Replace($FindPrincipalId, $ReplacePrincipalId) | Set-Content -Path $LocalPath
        Write-Host "Parameters file updated successfully" -ForegroundColor Green
    } else {
        Write-Warning "Parameters file not found at: $LocalPath"
    }

    # Change to infrastructure directory
    $infraDir = 'C:\Lab\01_exercises'
    Set-Location $infraDir
    Write-Host "Changed to directory: $infraDir" -ForegroundColor Yellow

    # Authenticate azd with Service Principal
    Write-Host "Authenticating azd with Service Principal..." -ForegroundColor Yellow
    & azd auth login --client-id $AppId --client-secret $AppSecret --tenant-id $TenantId
    if ($LASTEXITCODE -ne 0) {
        throw "azd auth login failed with exit code: $LASTEXITCODE"
    }
    Write-Host "azd authentication successful" -ForegroundColor Green

    # Check if environment already exists
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

    # Set the required environment variables in azd
    Write-Host "Setting azd environment variables..." -ForegroundColor Yellow
    & azd env set AZURE_PRINCIPAL_ID $azureUserId
    & azd env set AZURE_PRINCIPAL_TYPE "User"
    & azd env set OWNER_EMAIL $UserPrincipalName

    # Deploy the application
    Write-Host "Starting deployment..." -ForegroundColor Yellow
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