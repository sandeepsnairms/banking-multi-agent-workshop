targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string
param ChatAPIExists bool


@description('Id of the user or app to assign application roles')
param principalId string

// Tags that should be applied to all resources.
// 
// Note that 'azd-service-name' tags should be applied separately to service host resources.
// Example usage:
//   tags: union(tags, { 'azd-service-name': <service name in azure.yaml> })
var tags = {
  'azd-env-name': environmentName
}

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module monitoring './shared/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    tags: tags
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
  }
  scope: rg
}

module cosmos './shared/cosmosdb.bicep' = {
  name: 'cosmos'
  params: {    
    databaseName: 'vsai-database'
	chatsContainerName: 'ChatsData'
	accountsContainerName: 'AccountsData'
	offersContainerName:'Offers'
	usersContainerName:'Users'
    location: location
    name: '${abbrs.documentDBDatabaseAccounts}${resourceToken}'
    tags: tags
  }
  scope: rg
}

module registry './shared/registry.bicep' = {
  name: 'registry'
  params: {
    location: location
    tags: tags
    name: '${abbrs.containerRegistryRegistries}${resourceToken}'
  }
  scope: rg
}

module openAi './shared/openai.bicep' = {
  name: 'openai-account'
  params: {
    name: '${abbrs.openAiAccounts}${resourceToken}'
    location: location
    tags: tags
    sku: 'S0'
  }
  scope: rg
}



var deployments = [
  {
    name: 'gpt-4o'
    skuCapacity: 10
	skuName: 'GlobalStandard'
    modelName: 'gpt-4o'
    modelVersion: '2024-11-20'
  }
  {
    name: 'text-3-large'
    skuCapacity: 5
	skuName: 'Standard'
    modelName: 'text-embedding-3-small'
    modelVersion: '1'
  }
]

@batchSize(1)
module openAiModelDeployments './shared/modeldeployment.bicep' = [
  for (deployment, _) in deployments: {
    name: 'openai-model-deployment-${deployment.name}'
    params: {
      name: deployment.name
      parentAccountName: openAi.outputs.name
      skuName: deployment.skuName
      skuCapacity: deployment.skuCapacity
      modelName: deployment.modelName
      modelVersion: deployment.modelVersion
      modelFormat: 'OpenAI'
    }
	scope: rg
  }
]

module appsEnv './shared/apps-env.bicep' = {
  name: 'apps-env'
  params: {
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    tags: tags
  }
  scope: rg
}


module ChatAPI './app/ChatAPI.bicep' = {
  name: 'ChatAPI'
  params: {
    name: '${abbrs.appContainerApps}chatservicew-${resourceToken}'
    location: location
    tags: tags
    cosmosDbAccountName: cosmos.outputs.name
    identityName: '${abbrs.managedIdentityUserAssignedIdentities}chatservicew-${resourceToken}'
    applicationInsightsName: monitoring.outputs.applicationInsightsName
	openAIName: openAi.outputs.name
	userPrincipalId: !empty(principalId) ? principalId : null
    containerAppsEnvironmentId: appsEnv.outputs.id
    containerRegistryName: registry.outputs.name
    exists: ChatAPIExists
    envSettings: [      
      {
        name: 'SemanticKernelServiceSettings__AzureOpenAISettings__Endpoint'
        value: openAi.outputs.endpoint
      }	  
	  {
        name: 'SemanticKernelServiceSettings__AzureOpenAISettings__CompletionsDeployment'
        value: openAiModelDeployments[0].outputs.name
      }
	  {
        name: 'SemanticKernelServiceSettings__AzureOpenAISettings__EmbeddingsDeployment'
        value: openAiModelDeployments[1].outputs.name
      }
      {
        name: 'CosmosDBSettings__CosmosUri'
        value: cosmos.outputs.endpoint
      }
	  {
        name: 'CosmosDBSettings__Database'
        value: 'vsai-database'
      }
	  {
        name: 'CosmosDBSettings__ChatDataContainer'
        value: 'ChatsData'
      }
	  {
        name: 'CosmosDBSettings__UserDataContainer'
        value: 'Users'
      }
      {
        name: 'BankingCosmosDBSettings__CosmosUri'
        value: cosmos.outputs.endpoint
      }	
      {
        name: 'BankingCosmosDBSettings__Database'
        value: 'vsai-database'
      }
	  {
        name: 'BankingCosmosDBSettings__AccountsContainer'
        value: 'AccountsData'
      }
	  {
        name: 'BankingCosmosDBSettings__UserDataContainer'
        value: 'Users'
      }
	  {
        name: 'BankingCosmosDBSettings__RequestDataContainer'
        value: 'AccountsData'
      }
	  {
        name: 'BankingCosmosDBSettings__OfferDataContainer'
        value: 'Offers'
      }
      {
        name: 'ApplicationInsightsConnectionString'
        value: monitoring.outputs.applicationInsightsConnectionString
      }
  
    ]
  }
  scope: rg
  dependsOn: [cosmos, monitoring, openAi]
}


output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer
output AZURE_COSMOS_DB_NAME string = cosmos.outputs.name
output SERVICE_ChatAPI_ENDPOINT_URL string = ChatAPI.outputs.uri

