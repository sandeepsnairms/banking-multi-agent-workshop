
param documentDbClusterName string
param identityName string
param identityPrincipalId string

@description('Id of the user principals to assign database and application roles.')    
param userPrincipalId string = '' 

@description('Id of the service principal to assign database and application roles (optional - leave empty to skip SP role assignments).')
param servicePrincipalId string = '' 


resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: identityName
}

resource documentDb 'Microsoft.DocumentDB/mongoClusters@2025-09-01' existing = {
  name: documentDbClusterName
}


resource documentDbAccessUAMI 'Microsoft.DocumentDB/mongoClusters/users@2025-09-01' = {
  name: identityPrincipalId
  parent: documentDb
  properties: {
    identityProvider: {
      type: 'MicrosoftEntraID'
      properties: {
        principalType: 'servicePrincipal'
      }
    }
    roles: [
      {
        db: 'admin'
        role: 'root'
      }
    ]
  }
}

resource documentDbAccessCU 'Microsoft.DocumentDB/mongoClusters/users@2025-09-01' = if (!empty(userPrincipalId)) {
  name: userPrincipalId
  parent: documentDb
  properties: {
    identityProvider: {
      type: 'MicrosoftEntraID'
      properties: {
        principalType: 'user'
      }
    }
    roles: [
      {
        db: 'admin'
        role: 'root'
      }
    ]
  }
}

resource documentDbAccessSP 'Microsoft.DocumentDB/mongoClusters/users@2025-09-01' = if (!empty(servicePrincipalId)) {
  name: servicePrincipalId
  parent: documentDb
  properties: {
    identityProvider: {
      type: 'MicrosoftEntraID'
      properties: {
        principalType: 'servicePrincipal'
      }
    }
    roles: [
      {
        db: 'MultiAgentBanking'
        role: 'root'
      }
    ]
  }
}

output identityId string = identity.properties.principalId
output identityName string = identity.name
