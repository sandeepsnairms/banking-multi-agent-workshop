param containerAppName string
param location string = resourceGroup().location
param environmentId string
param imageName string
param registryServer string
param uamiId string
param containerPort int = 80

resource containerApp 'Microsoft.App/containerApps@2022-01-01-preview' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uamiId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      registries: [
        {
          server: registryServer
          identity: uamiId
        }
      ]
      ingress: {
        external: true
        targetPort: containerPort
        transport: 'auto'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: imageName
          resources: {
            cpu: json('1.0')
            memory: '2.0Gi'
          }  
        }
      ]
    }
  }
}

output uri string = 'https://${containerApp.properties.configuration.ingress.fqdn}'