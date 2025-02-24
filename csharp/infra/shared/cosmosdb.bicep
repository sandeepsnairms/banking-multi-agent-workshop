param databaseName string
param accountsContainerName string
param chatsContainerName string
param offersContainerName string
param usersContainerName string
param location string = resourceGroup().location
param name string
param tags object = {}

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: name
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        failoverPriority: 0
        isZoneRedundant: false
        locationName: location
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
  }
  tags: tags
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosDb
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
  tags: tags
}

resource cosmosContainerAccounts 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
	parent: database
	name: accountsContainerName
	properties: {
	  resource: {
		id: accountsContainerName
		partitionKey: {
		  paths: [
			'/tenantId'
			'/accountId'
		  ]
		  kind: 'MultiHash'
		  version: 2
		}
	  }
	}
	tags: tags
}

resource cosmosContainerChats 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
    parent: database
    name: chatsContainerName
    properties: {
      resource: {
        id: chatsContainerName
        partitionKey: {
          paths: [
            '/tenantId'
            '/userId'
            '/sessionId'
          ]
          kind: 'MultiHash'
          version: 2
        }
      }
    }
    tags: tags
  }
  
resource cosmosContainerOffers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
    parent: database
    name: offersContainerName
    properties: {
      resource: {
        id: offersContainerName
        partitionKey: {
          paths: [
            '/tenantId'
          ]
          kind: 'Hash'
          version: 2
        }
      }
    }
    tags: tags
  }
  
resource cosmosContainerUsers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
    parent: database
    name: usersContainerName
    properties: {
      resource: {
        id: usersContainerName
        partitionKey: {
          paths: [
            '/tenantId'
          ]
          kind: 'Hash'
          version: 2
        }
      }
    }
    tags: tags
  }


output endpoint string = cosmosDb.properties.documentEndpoint
output name string = cosmosDb.name
