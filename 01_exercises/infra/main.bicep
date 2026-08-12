targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Existing or new resource group name. Defaults to the workshop naming convention.')
param resourceGroupName string = 'rg-${environmentName}'

@description('Id of the user or app to assign application roles')
param principalId string

@description('Id of the service principal to assign application roles (optional - if not provided, SP roles will be skipped)')
param servicePrincipalId string = ''

@description('Owner tag for resource tagging')
param owner string = 'defaultuser@example.com'

@allowed([
  'GlobalStandard'
  'Standard'
])
@description('Azure OpenAI SKU for the chat model deployment.')
param chatDeploymentSkuName string = 'GlobalStandard'

@description('Existing Microsoft Foundry account name. Leave empty to provision a new Azure OpenAI account.')
param existingOpenAIAccountName string = ''

@description('Resource group containing the existing Microsoft Foundry account.')
param existingOpenAIResourceGroupName string = ''

@description('Endpoint of the existing Microsoft Foundry account.')
param existingOpenAIEndpoint string = ''

@description('Start IPv4 address allowed through the Azure DocumentDB firewall. The workshop default allows access from anywhere.')
param documentDbFirewallStartIpAddress string = '0.0.0.0'

@description('End IPv4 address allowed through the Azure DocumentDB firewall. The workshop default allows access from anywhere.')
param documentDbFirewallEndIpAddress string = '255.255.255.255'

var tags = {
  'azd-env-name': environmentName
  owner: owner
}

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var useExistingOpenAI = !empty(existingOpenAIAccountName)

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: resourceGroupName
  location: location
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

// Deploy Azure DocumentDB
module documentDb './shared/documentdb.bicep' = {
  name: 'document-db'
  params: {
    name: '${abbrs.documentDBMongoClusters}${resourceToken}'
    location: location
    firewallStartIpAddress: documentDbFirewallStartIpAddress
    firewallEndIpAddress: documentDbFirewallEndIpAddress
    tags: tags
  }
  scope: rg
}

// Deploy OpenAI
module openAi './shared/openai.bicep' = if (!useExistingOpenAI) {
  name: 'openai-account'
  params: {
    name: '${abbrs.openAiAccounts}${resourceToken}'
    location: location
    tags: tags
    sku: 'S0'
  }
  scope: rg
}

var openAIName = useExistingOpenAI ? existingOpenAIAccountName : openAi!.outputs.name
var openAIResourceGroupName = useExistingOpenAI ? existingOpenAIResourceGroupName : rg.name
var openAIEndpoint = useExistingOpenAI ? existingOpenAIEndpoint : openAi!.outputs.endpoint

//Deploy OpenAI Deployments
var deployments = [
  {
    name: 'gpt-5-mini'
    skuCapacity: 150
	skuName: chatDeploymentSkuName
    modelName: 'gpt-5-mini'
    modelVersion: '2025-08-07'
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
      parentAccountName: openAIName
      skuName: deployment.skuName
      skuCapacity: deployment.skuCapacity
      modelName: deployment.modelName
      modelVersion: deployment.modelVersion
      modelFormat: 'OpenAI'
    }
  scope: resourceGroup(openAIResourceGroupName)
  }
]

//Assign Roles to Managed Identities
module AssignRoles './shared/assignroles.bicep' = {
  name: 'AssignRoles'
  params: {
    documentDbClusterName: documentDb.outputs.name
    identityName: managedIdentity.outputs.name
    identityPrincipalId: managedIdentity.outputs.principalId
    userPrincipalId: principalId
    servicePrincipalId: !empty(servicePrincipalId) ? servicePrincipalId : ''
  }
  scope: rg
}

module assignOpenAIRoles './shared/assignopenairoles.bicep' = {
  name: 'AssignOpenAIRoles'
  params: {
    openAIName: openAIName
    identityPrincipalId: managedIdentity.outputs.principalId
    userPrincipalId: principalId
    servicePrincipalId: !empty(servicePrincipalId) ? servicePrincipalId : ''
  }
  scope: resourceGroup(openAIResourceGroupName)
}


// Outputs
output RG_NAME string = rg.name
output DOCUMENTDB_CLUSTER_NAME string = documentDb.outputs.name
output AZURE_OPENAI_ENDPOINT string = openAIEndpoint
output AZURE_OPENAI_COMPLETIONSDEPLOYMENTID string = openAiModelDeployments[0].outputs.name
output AZURE_OPENAI_EMBEDDINGDEPLOYMENTID string = openAiModelDeployments[1].outputs.name
