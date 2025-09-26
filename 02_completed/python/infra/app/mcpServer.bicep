param name string
param location string = resourceGroup().location
param tags object = {}
param containerAppsEnvironmentName string
param containerRegistryName string
param exists bool = false
param envSettings array = []

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-02-02-preview' existing = {
  name: containerAppsEnvironmentName
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = {
  name: containerRegistryName
}

resource app 'Microsoft.App/containerApps@2024-02-02-preview' = {
  name: name
  location: location
  tags: union(tags, {'azd-service-name': 'MCPServer'})
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['DELETE', 'GET', 'POST', 'PUT']
          allowedHeaders: ['*']
        }
      }
      activeRevisionsMode: 'Single'
    }
    template: {
      containers: [
        {
          name: 'mcp-server'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
          env: union([
            {
              name: 'PORT'
              value: '8080'
            }
          ], envSettings)
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 2
      }
    }
  }
}

output name string = app.name
output uri string = 'https://${app.properties.configuration.ingress.fqdn}'
output id string = app.id
output identityPrincipalId string = app.identity.principalId