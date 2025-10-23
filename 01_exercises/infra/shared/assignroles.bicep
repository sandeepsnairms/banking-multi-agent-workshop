
param cosmosDbAccountName string
param identityName string
param openAIName string = ''

@description('Id of the service principal to assign database and application roles.')    
param servicePrincipalId string = '' 

@description('Id of the current user to assign database and application roles.')    
param currentUserId string = '' 

@description('Type of the service principal - User or ServicePrincipal')
param principalType string = 'User' 

// Variables to handle duplicate principal IDs
var hasSP = !empty(servicePrincipalId)
var hasCurrentUser = !empty(currentUserId)
var isDuplicatePrincipal = servicePrincipalId == currentUserId && hasSP && hasCurrentUser
var shouldCreateSPRole = hasSP && !isDuplicatePrincipal
var shouldCreateCurrentUserRole = hasCurrentUser && !isDuplicatePrincipal
var shouldCreateCombinedRole = isDuplicatePrincipal 


resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: identityName
}

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = if (!empty(openAIName)) {
  name: openAIName
}

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' existing = {
  name: cosmosDbAccountName
}


// Role Assignment for Cognitive Services User to UAMI (conditional)
resource cognitiveServicesRoleAssignmentUAMI 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(openAIName)) {
  name: guid(identity.id, openAi.id, 'cognitive-services-user')  // Unique GUID for role assignment
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')  // Cognitive Services User Role ID
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Role Assignment for Cognitive Services User to Service Principal (conditional)
resource cognitiveServicesRoleAssignmentSP 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (shouldCreateSPRole && !empty(openAIName)) {
  name: guid(servicePrincipalId, openAi.id, 'cognitive-services-user-sp')  // Unique GUID for role assignment
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')  // Cognitive Services User Role ID
    principalId: servicePrincipalId
    principalType: principalType
  }
}

// Role Assignment for Cognitive Services User to Current User (conditional)
resource cognitiveServicesRoleAssignmentCU 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (shouldCreateCurrentUserRole && !empty(openAIName)) {
  name: guid(currentUserId, openAi.id, 'cognitive-services-user-cu')  // Unique GUID for role assignment
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')  // Cognitive Services User Role ID
    principalId: currentUserId
    principalType: 'User'
  }
}

// Role Assignment for Cognitive Services User to Combined Principal (when SP and User are the same)
resource cognitiveServicesRoleAssignmentCombined 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (shouldCreateCombinedRole && !empty(openAIName)) {
  name: guid(servicePrincipalId, openAi.id, 'cognitive-services-user-combined')  // Unique GUID for role assignment
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')  // Cognitive Services User Role ID
    principalId: servicePrincipalId  // Use either servicePrincipalId or currentUserId (they're the same)
    principalType: principalType
  }
}


// Role Assignment for Cosmos DB role to managed identity
resource cosmosAccessRoleUAMI 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-11-15' = {
  name: guid('00000000-0000-0000-0000-000000000002', identity.id, cosmosDb.id)
  parent: cosmosDb
  properties: {
    principalId: identity.properties.principalId
    roleDefinitionId: resourceId('Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions', cosmosDb.name, '00000000-0000-0000-0000-000000000002')
    scope: cosmosDb.id
  }
}


// Role Assignment for Cosmos DB role to service principal
resource cosmosAccessRoleSP 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-11-15' = if (shouldCreateSPRole) {
  name: guid('00000000-0000-0000-0000-000000000002', servicePrincipalId, cosmosDb.id, 'sp')
  parent: cosmosDb
  properties: {
    principalId: servicePrincipalId
    roleDefinitionId: resourceId('Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions', cosmosDb.name, '00000000-0000-0000-0000-000000000002')
    scope: cosmosDb.id
  }
}

// Role Assignment for Cosmos DB role to current user
resource cosmosAccessRoleCU 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-11-15' = if (shouldCreateCurrentUserRole) {
  name: guid('00000000-0000-0000-0000-000000000002', currentUserId, cosmosDb.id, 'cu')
  parent: cosmosDb
  properties: {
    principalId: currentUserId
    roleDefinitionId: resourceId('Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions', cosmosDb.name, '00000000-0000-0000-0000-000000000002')
    scope: cosmosDb.id
  }
}

// Role Assignment for Cosmos DB role to combined principal (when SP and User are the same)
resource cosmosAccessRoleCombined 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-11-15' = if (shouldCreateCombinedRole) {
  name: guid('00000000-0000-0000-0000-000000000002', servicePrincipalId, cosmosDb.id, 'combined')
  parent: cosmosDb
  properties: {
    principalId: servicePrincipalId  // Use either servicePrincipalId or currentUserId (they're the same)
    roleDefinitionId: resourceId('Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions', cosmosDb.name, '00000000-0000-0000-0000-000000000002')
    scope: cosmosDb.id
  }
}

output identityId string = identity.properties.principalId
output identityName string = identity.name
output debugInfo object = {
  servicePrincipalId: servicePrincipalId
  currentUserId: currentUserId
  hasSP: hasSP
  hasCurrentUser: hasCurrentUser
  isDuplicatePrincipal: isDuplicatePrincipal
  shouldCreateSPRole: shouldCreateSPRole
  shouldCreateCurrentUserRole: shouldCreateCurrentUserRole
  shouldCreateCombinedRole: shouldCreateCombinedRole
}
