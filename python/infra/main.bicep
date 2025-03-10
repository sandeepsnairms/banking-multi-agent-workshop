targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string
param AgentAPIExists bool


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
    databaseName: 'MultiAgentBanking'
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
  name: 'openai'
  params: {
    deployment: {  
      name: 'completions'
      sku: {
        name: 'GlobalStandard'
        capacity: 10
      }
      model: {
        name: 'gpt-4o'
        version: '2024-11-20'
      }
    }    
    location: location
    name: '${abbrs.openAiAccounts}${resourceToken}'
    sku: 'S0'
    tags: tags
  }
  scope: rg
}



module appsEnv './shared/apps-env.bicep' = {
  name: 'apps-env'
  params: {
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    tags: tags
  }
  scope: rg
}


module AgentAPI './app/AgentAPI.bicep' = {
  name: 'AgentAPI'
  params: {
    name: '${abbrs.appContainerApps}chatservicew-${resourceToken}'
    location: location
    tags: tags
    cosmosDbAccountName: cosmos.outputs.name
    identityName: '${abbrs.managedIdentityUserAssignedIdentities}chatservicew-${resourceToken}'
    applicationInsightsName: monitoring.outputs.applicationInsightsName
	openAIName: openAi.outputs.name
    containerAppsEnvironmentId: appsEnv.outputs.id
    containerRegistryName: registry.outputs.name
    exists: AgentAPIExists
    envSettings: [      
      {
        name: 'AZURE_OPENAI_ENDPOINT'
        value: openAi.outputs.endpoint
      }	  
	  {
        name: 'CompletionsDeployment'
        value: openAi.outputs.modelDeploymentName
      }
	  {
        name: 'DATABASE_NAME'
        value: 'MultiAgentBanking'
      }
      {
        name: 'COSMOSDB_ENDPOINT'
        value: cosmos.outputs.endpoint
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
output SERVICE_AgentAPI_ENDPOINT_URL string = AgentAPI.outputs.uri

