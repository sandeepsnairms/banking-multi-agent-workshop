param openAIName string
param identityPrincipalId string

@description('Id of the user principal to assign Azure AI access.')
param userPrincipalId string = ''

@description('Id of the service principal to assign Azure AI access (optional).')
param servicePrincipalId string = ''

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: openAIName
}

resource cognitiveServicesRoleAssignmentUAMI 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(identityPrincipalId, openAi.id, 'cognitive-services-user')
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource cognitiveServicesRoleAssignmentCU 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(userPrincipalId)) {
  name: guid(userPrincipalId, openAi.id, 'cognitive-services-user')
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: userPrincipalId
    principalType: 'User'
  }
}

resource cognitiveServicesRoleAssignmentSP 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(servicePrincipalId)) {
  name: guid(servicePrincipalId, openAi.id, 'cognitive-services-user-sp')
  scope: openAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: servicePrincipalId
    principalType: 'ServicePrincipal'
  }
}
