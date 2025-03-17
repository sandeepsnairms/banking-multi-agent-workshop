param name string
param tags object = {}
param containerAppName string
param location string = resourceGroup().location
param environmentId string
param imageName string
param containerImageTag string = 'latest'
param registryServer string
param identityName string
param containerPort int = 8088
param chatAPIUrl string


// Get the principal ID of the user assigned managed identity user
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: identityName
}


module fetchLatestImage '../modules/fetch-container-image.bicep' = {
  name: '${name}-fetch-image'
  params: {
    name: name
	exists:false
  }
}

resource frontend 'Microsoft.App/containerApps@2023-05-01' = {
  name: name
  location: location
  tags: union(tags, {'azd-service-name': 'FrontendApp' })
  identity: {
    type: 'UserAssigned'
     userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress:  {
        external: true
        targetPort: 80
        transport: 'auto'      
        corsPolicy: {
          allowedOrigins: [
				'*'
          ]
        }
	  }
	  registries: [
        {
          server: '${registryServer}.azurecr.io'
          identity: identity.id
        }
      ]
	  activeRevisionsMode: 'Single' // Ensures only one active revision at a time
      
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: fetchLatestImage.outputs.?containers[?0].image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          env: [              
			  {
                name: 'apiUrl'
                value: chatAPIUrl
              }
		  ]
		  resources: {
            cpu: json('1.0')
            memory: '2.0Gi'
          }  
        }
      ]
    }
  }
}

output uri string = 'https://${frontend.properties.configuration.ingress.fqdn}'
