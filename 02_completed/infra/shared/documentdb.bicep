param location string = resourceGroup().location
param name string
param tags object = {}

@description('Start IPv4 address allowed through the cluster firewall.')
param firewallStartIpAddress string

@description('End IPv4 address allowed through the cluster firewall.')
param firewallEndIpAddress string

resource documentDb 'Microsoft.DocumentDB/mongoClusters@2025-09-01' = {
  name: name
  location: location
  properties: {
    authConfig: {
      allowedModes: [
        'MicrosoftEntraID'
      ]
    }
    compute: {
      tier: 'M30'
    }
    dataApi: {
      mode: 'Disabled'
    }
    highAvailability: {
      targetMode: 'Disabled'
    }
    publicNetworkAccess: 'Enabled'
    sharding: {
      shardCount: 1
    }
    storage: {
      sizeGb: 32
      type: 'PremiumSSD'
    }
    serverVersion: '7.0'
  }
  tags: tags
}

resource workshopFirewallRule 'Microsoft.DocumentDB/mongoClusters/firewallRules@2025-09-01' = {
  parent: documentDb
  name: 'AllowAllWorkshopTesting'
  properties: {
    startIpAddress: firewallStartIpAddress
    endIpAddress: firewallEndIpAddress
  }
}

output name string = documentDb.name