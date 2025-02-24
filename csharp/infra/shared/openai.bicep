param deployment object
param location string = resourceGroup().location
param name string
param sku string = 'S0'
param tags object = {}

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  sku: {
    name: sku
  }
  kind: 'OpenAI'
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
  }
  tags: tags
}


resource openAiDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
    parent: openAi
    name: name
    sku: {
      capacity: deployment.sku.capacity
      name: deployment.sku.name
    }
    properties: {
      model: {
        format: 'OpenAI'
        name: deployment.model.name
        version: deployment.model.version
      }
    }
}



output endpoint string = 'https://${openAi.name}.openai.azure.com/'
output name string = openAi.name
