Param(
    [parameter(Mandatory=$false)][string]$apiUrl=$env:SERVICE_CHATAPI_ENDPOINT_URL
)

# Check if API URL is provided
if (-not $apiUrl) {
    Write-Host "Error: API URL is required. Usage: .\postdeploy.ps1 -apiUrl 'https://your-api.com/resource'"
    exit 1
}


#Populating UserData with dummy documents
$jsonFilePath = ".\data\UserData.json"

# Define the API endpoint
$apiEndpointUrl = "{0}/userdata" -f $apiUrl

# Read the JSON file and parse it
$jsonArray = Get-Content -Path $jsonFilePath | ConvertFrom-Json

# Iterate through each item in the array
foreach ($item in $jsonArray) {
    # Convert the object back to JSON for the request body
    $jsonBody = $item | ConvertTo-Json -Depth 10

    # Convert JSON string to bytes (some APIs require it)
    $jsonBytes = [System.Text.Encoding]::UTF8.GetBytes($jsonBody)

    # Make the PUT request
    try {
        $response = Invoke-RestMethod -Uri $apiEndpointUrl -Method Put -Body $jsonBytes -ContentType "application/json"
        Write-Output "PUT UserData Request Successful: $response"
    } catch {
        Write-Output "Error: $_"	
    
	}
}


#Populating AccountsData with dummy documents
$jsonFilePath = ".\data\AccountsData.json"

# Define the API endpoint
$apiEndpointUrl = "{0}/accountdata" -f $apiUrl

# Read the JSON file and parse it
$jsonArray = Get-Content -Path $jsonFilePath | ConvertFrom-Json

# Iterate through each item in the array
foreach ($item in $jsonArray) {
    # Convert the object back to JSON for the request body
    $jsonBody = $item | ConvertTo-Json -Depth 10

    # Convert JSON string to bytes (some APIs require it)
    $jsonBytes = [System.Text.Encoding]::UTF8.GetBytes($jsonBody)

    # Make the PUT request
    try {
        $response = Invoke-RestMethod -Uri $apiEndpointUrl -Method Put -Body $jsonBytes -ContentType "application/json"
        Write-Output "PUT AccountsData Request Successful: $response"
    } catch {
        Write-Output "Error: $_"
    }
}

#Populating OffersData with dummy documents
$jsonFilePath = ".\data\OffersData.json"

# Define the API endpoint
$apiEndpointUrl = "{0}/offerdata" -f $apiUrl

# Read the JSON file and parse it
$jsonArray = Get-Content -Path $jsonFilePath | ConvertFrom-Json

# Iterate through each item in the array
foreach ($item in $jsonArray) {
    # Convert the object back to JSON for the request body
    $jsonBody = $item | ConvertTo-Json -Depth 10

    # Convert JSON string to bytes (some APIs require it)
    $jsonBytes = [System.Text.Encoding]::UTF8.GetBytes($jsonBody)

    # Make the PUT request
    try {
        $response = Invoke-RestMethod -Uri $apiEndpointUrl -Method Put -Body $jsonBytes -ContentType "application/json"
        Write-Output "PUT OffersData Request Successful: $response"
    } catch {
        Write-Output "Error: $_"
		
	Start-Sleep -Milliseconds 1000  # Sleeps for 0.5 seconds
	
    }
}