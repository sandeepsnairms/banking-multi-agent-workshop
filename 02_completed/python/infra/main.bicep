targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string

@description('Owner tag for resource tagging')
param owner string = 'defaultuser@example.com'

var tags = {
  'azd-env-name': environmentName
  'owner': owner
}

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

// Deploy AppInsights
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

// Deploy Azure Cosmos DB
module cosmos './shared/cosmosdb.bicep' = {
  name: 'cosmos'
  params: {    
    databaseName: 'MultiAgentBanking'
	chatsContainerName: 'Chat'
	accountsContainerName: 'AccountsData'
	offersContainerName:'OffersData'
	usersContainerName:'Users'
	checkpointsContainerName:'Checkpoints'
	chatHistoryContainerName:'ChatHistory'
	debugContainerName:'Debug'
    location: location
    name: '${abbrs.documentDBDatabaseAccounts}${resourceToken}'
    tags: tags
  }
  scope: rg
}


// Deploy Azure Container Registry (ACR)
module registry './shared/registry.bicep' = {
  name: 'registry'
  params: {
    location: location
    tags: tags
    name: '${abbrs.containerRegistryRegistries}${resourceToken}'
  }
  scope: rg
}

// Deploy OpenAI
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

//Deploy OpenAI Deployments
var deployments = [
  {
    name: 'gpt-4.1-mini'
    skuCapacity: 30
	skuName: 'GlobalStandard'
    modelName: 'gpt-4.1-mini'
    modelVersion: '2025-04-14'
  }
  {
    name: 'text-embedding-3-small'
    skuCapacity: 5
	skuName: 'GlobalStandard'
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

// Deploy Container Apps Environment
module appsEnv './shared/apps-env.bicep' = {
  name: 'apps-env'
  params: {
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
  }
  scope: rg
}


//Assign Roles to Managed Identities
module AssignRoles './shared/assignroles.bicep' = {
  name: 'AssignRoles'
  params: {
    name: 'assignroles-${resourceToken}'
    location: location
    tags: tags
    cosmosDbAccountName: cosmos.outputs.name
    identityName: '${abbrs.managedIdentityUserAssignedIdentities}chatservicew-${resourceToken}'
	openAIName: openAi.outputs.name
	userPrincipalId: !empty(principalId) ? principalId : null
    containerRegistryName: registry.outputs.name
  }
  scope: rg
  dependsOn: [cosmos, monitoring, openAi]
}



// STEP 1: Deploy MCP Server Container App FIRST
module mcpServer './app/mcpServer.bicep' = {
  name: 'mcpServer'
  params: {
    name: '${abbrs.appContainerApps}mcpserver-${resourceToken}'
    location: location
    tags: tags
    environmentId: appsEnv.outputs.id
    containerRegistryName: registry.outputs.name
    identityName: AssignRoles.outputs.identityName
    registryServer: registry.outputs.loginServer
    envSettings: [
      {
        name: 'PORT'
        value: '8080'
      }
      {
        name: 'AZURE_OPENAI_ENDPOINT'
        value: openAi.outputs.endpoint
      }
      {
        name: 'AZURE_OPENAI_COMPLETIONSDEPLOYMENTID'
        value: openAiModelDeployments[0].outputs.name
      }
      {
        name: 'AZURE_OPENAI_EMBEDDINGDEPLOYMENTID'
        value: openAiModelDeployments[1].outputs.name
      }
      {
        name: 'COSMOSDB_ENDPOINT'
        value: cosmos.outputs.endpoint
      }
      {
        name: 'ApplicationInsightsConnectionString'
        value: monitoring.outputs.applicationInsightsConnectionString
      }
      {
        name: 'AZURE_OPENAI_API_VERSION'
        value: '2024-02-15-preview'
      }
      {
        name: 'AZURE_CLIENT_ID'
        value: AssignRoles.outputs.identityClientId
      }
      {
        name: 'MCP_AUTH_TOKEN'
        value: 'banking-server-prod-token-2025'
      }
      {
        name: 'GITHUB_CLIENT_ID'
        value: ''
      }
      {
        name: 'GITHUB_CLIENT_SECRET'
        value: ''
      }
    ]
  }
  scope: rg
  dependsOn: [cosmos, monitoring, openAi, AssignRoles, appsEnv, registry]
}

// STEP 2: Deploy ChatAPI Container App AFTER MCP Server is fully deployed
module ChatAPI './app/ChatAPI.bicep' = {
  name: 'ChatAPI'
  params: {
    name: '${abbrs.appContainerApps}webapi-${resourceToken}'
	containerAppName: 'chatservicewebapi'
    location: location
    tags: tags
    containerImageTag: 'latest'  // Change this if versioning is required
    containerPort: 8080
    identityName: AssignRoles.outputs.identityName
	environmentId: appsEnv.outputs.id
	imageName: 'chatservicewebapi'  // ACR repository name
	registryServer:registry.outputs.name
	applicationInsightsName: monitoring.outputs.applicationInsightsName
    envSettings: [     
	  {
        name: 'AZURE_OPENAI_ENDPOINT'
        value: openAi.outputs.endpoint
      }	  
	  {
        name: 'AZURE_OPENAI_COMPLETIONSDEPLOYMENTID'
        value: openAiModelDeployments[0].outputs.name
      }
	  {
        name: 'AZURE_OPENAI_EMBEDDINGDEPLOYMENTID'
        value: openAiModelDeployments[1].outputs.name
      }
      {
        name: 'COSMOSDB_ENDPOINT'
        value: cosmos.outputs.endpoint
      }
	  {
        name: 'CosmosDBSettings__Database'
        value: 'MultiAgentBanking'
      }
	  {
        name: 'CosmosDBSettings__ChatDataContainer'
        value: 'Chat'
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
        value: 'MultiAgentBanking'
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
        value: 'OffersData'
      }
      {
        name: 'ApplicationInsightsConnectionString'
        value: monitoring.outputs.applicationInsightsConnectionString
      }
      {
        name: 'AZURE_OPENAI_API_VERSION'
        value: '2024-02-15-preview'
      }
      {
        name: 'MCP_SERVER_BASE_URL'
        value: mcpServer.outputs.uri
      }
      {
        name: 'USE_REMOTE_MCP_SERVER'
        value: 'true'
      }
      {
        name: 'MCP_AUTH_TOKEN'
        value: 'banking-server-prod-token-2025'
      }
  
    ]
  }
  scope: rg
  // Explicitly depend on MCP Server being fully deployed first
  dependsOn: [mcpServer]
}

module webApp './app/webApp.bicep' = {
  name: 'webApp'  
  params: {
    name: '${abbrs.webSitesAppService}${resourceToken}'
    appServicePlanName: '${abbrs.webServerFarms}${resourceToken}'
  }
  scope: rg
  dependsOn: [ChatAPI]
}


// Outputs
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer
output SERVICE_ChatAPI_ENDPOINT_URL string = ChatAPI.outputs.uri
output SERVICE_MCPSERVER_ENDPOINT_URL string = mcpServer.outputs.uri
output FRONTENDPOINT_URL string = webApp.outputs.url
output AZURE_OPENAI_ENDPOINT string = openAi.outputs.endpoint
output WEB_APP_NAME string = '${abbrs.webSitesAppService}${resourceToken}'
output RG_NAME string = 'rg-${environmentName}'
output AZURE_OPENAI_COMPLETIONSDEPLOYMENTID string = openAiModelDeployments[0].outputs.name
output AZURE_OPENAI_EMBEDDINGDEPLOYMENTID string = openAiModelDeployments[1].outputs.name
output COSMOSDB_ENDPOINT string = cosmos.outputs.endpoint
output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString
