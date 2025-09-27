param name string
param location string = resourceGroup().location
param tags object = {}
param environmentId string
param containerRegistryName string
param identityName string
param exists bool = false
param envSettings array = []

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = {
  name: containerRegistryName
}

// Get the principal ID of the user assigned managed identity user
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: identityName
}

resource app 'Microsoft.App/containerApps@2024-02-02-preview' = {
  name: name
  location: location
  tags: union(tags, {'azd-service-name': 'MCPServer'})
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    managedEnvironmentId: environmentId
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
      registries: [
        {
          server: '${containerRegistry.name}.azurecr.io'
          identity: identity.id
        }
      ]
      activeRevisionsMode: 'Single'
    }
    template: {
      containers: [
        {
          name: 'mcp-server'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: union(
            [
              {
                name: 'PORT'
                value: '8080'
              }
            ],
            envSettings
          )
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

output name string = app.name
output uri string = 'https://${app.properties.configuration.ingress.fqdn}'
output id string = app.id