param name string
param location string = resourceGroup().location
param tags object = {}
param applicationInsightsName string = ''

// Create Managed Environment for Azure Container Apps
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-10-02-preview' = {
  name: name
  location: location
  properties: {}
}

output id string = containerAppsEnvironment.id