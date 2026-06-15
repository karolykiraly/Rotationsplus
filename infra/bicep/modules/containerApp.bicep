param name string
param location string
param tags object
param environmentId string
param image string
param acrLoginServer string

@description('Resource ID of the user-assigned managed identity (ACR pull + Key Vault reference).')
param userAssignedIdentityId string

param targetPort int
param externalIngress bool
param minReplicas int
param maxReplicas int

@description('CPU cores as a string (Bicep has no float param type); converted via json().')
param cpu string
param memory string

@description('Key Vault-sourced secrets: [{ name, keyVaultUrl }]. Resolved with the user-assigned identity.')
param keyVaultSecrets array = []

@description('Container environment variables (value or secretRef).')
param env array = []

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: externalIngress
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acrLoginServer
          identity: userAssignedIdentityId
        }
      ]
      secrets: [
        for s in keyVaultSecrets: {
          name: s.name
          keyVaultUrl: s.keyVaultUrl
          identity: userAssignedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: name
          image: image
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: env
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/alive', port: targetPort }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: targetPort }
              initialDelaySeconds: 10
              periodSeconds: 30
              failureThreshold: 6
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = app.properties.configuration.ingress.fqdn
output name string = app.name
