param name string
param location string
param tags object
param logAnalyticsCustomerId string
param logAnalyticsResourceId string

resource law 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logAnalyticsResourceId, '/'))
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: law.listKeys().primarySharedKey
      }
    }
  }
}

output environmentId string = environment.id
