Param(
    [parameter(Mandatory=$false)][string]$apiUrl=$env:SERVICE_ChatAPI_ENDPOINT_URL
)

# Check if API URL is provided
if (-not $apiUrl) {
    Write-Host "Error: API URL is required. Usage: .\postdeploy.ps1 -apiUrl 'https://your-api.com/resource'"
    exit 1
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
		Start-Sleep -Milliseconds 500  # Sleeps for 0.5 seconds
    }
}

# Ask user if they want to add dummy data
$dummyDataResponse = Read-Host "Do you want to add some dummy data for testing? (yes/no)"
if ($dummyDataResponse -match "^(yes|y)$") {
    Write-Host "Adding dummy data..."
    # Load dummy data
	Send-Data "./data/UserData.json" "userdata"
	Send-Data "./data/AccountsData.json" "accountdata"
	Send-Data "./data/OffersData.json" "offerdata"
	
	Write-Host "Data load completed."
} else {
    Write-Host "Skipping dummy data addition."
}

Start-Sleep -Milliseconds 2000  # Sleeps for 2 seconds