targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('Type of the principal - User or ServicePrincipal')
param principalType string = 'User'

@description('Id of the current user to assign application roles')
param currentUserId string = ''

@description('Whether to deploy OpenAI resources')
param deployOpenAI bool = true

@description('Owner tag for resource tagging')
param owner string = 'defaultuser@example.com'

// Validation: At least one of principalId or currentUserId must be provided
var hasPrincipalId = !empty(principalId)
var hasCurrentUserId = !empty(currentUserId)
var hasValidPrincipals = hasPrincipalId || hasCurrentUserId

var tags = {
  'azd-env-name': environmentName
  owner: owner
}

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

// Deploy Managed Identity
module managedIdentity './shared/managedidentity.bicep' = {
  name: 'managed-identity'
  params: {
    identityName: '${abbrs.managedIdentityUserAssignedIdentities}${resourceToken}'
    location: location
    tags: tags
  }
  scope: rg
}

// Deploy Azure Cosmos DB
module cosmos './shared/cosmosdb.bicep' = {
  name: 'cosmos'
  params: {    
    name: '${abbrs.documentDBDatabaseAccounts}${resourceToken}'
    location: location
    tags: tags
    databaseName: 'MultiAgentBanking'
	  chatsContainerName: 'ChatsData'
	  accountsContainerName: 'AccountsData'
	  offersContainerName:'OffersData'
	  usersContainerName:'Users'
	  checkpointsContainerName:'Checkpoints'
	  chatHistoryContainerName:'ChatHistory'
	  debugContainerName:'Debug'
  }
  scope: rg
}

// Deploy OpenAI (conditional)
module openAi './shared/openai.bicep' = if (deployOpenAI) {
  name: 'openai-account'
  params: {
    name: '${abbrs.openAiAccounts}${resourceToken}'
    location: location
    tags: tags
    sku: 'S0'
  }
  scope: rg
}

//Deploy OpenAI Deployments (conditional)
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
  for (deployment, _) in deployments: if (deployOpenAI) {
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

//Assign Roles to Managed Identity and Current User/Service Principal
module AssignRoles './shared/assignroles.bicep' = if (hasValidPrincipals) {
  name: 'AssignRoles'
  params: {
    cosmosDbAccountName: cosmos.outputs.name
    openAIName: deployOpenAI ? '${abbrs.openAiAccounts}${resourceToken}' : ''
    identityName: managedIdentity.outputs.name
    servicePrincipalId: principalId  // Service Principal ID
    currentUserId: currentUserId     // Current User ID  
    principalType: principalType
  }
  scope: rg
  dependsOn: deployOpenAI ? [
    openAi
    openAiModelDeployments  // Ensure deployments are complete before assigning roles
  ] : []
}


// Outputs (conditional)
output RG_NAME string = 'rg-${environmentName}'
output COSMOSDB_ENDPOINT string = cosmos.outputs.endpoint
output AZURE_OPENAI_ENDPOINT string = deployOpenAI ? openAi.name : 'Not deployed'
output AZURE_OPENAI_COMPLETIONSDEPLOYMENTID string = deployOpenAI ? deployments[0].name : 'Not deployed'
output AZURE_OPENAI_EMBEDDINGDEPLOYMENTID string = deployOpenAI ? deployments[1].name : 'Not deployed'
output MANAGED_IDENTITY_NAME string = managedIdentity.outputs.name
output OPENAI_DEPLOYED bool = deployOpenAI
