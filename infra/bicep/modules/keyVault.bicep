param name string
param location string
param tags object

@description('Entra object IDs granted get/list on secrets (e.g. the app managed identity principalId).')
param readerObjectIds array

@description('Secrets to seed: [{ name, value }]. Values originate from @secure() params upstream.')
param secrets array = []

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    // Access-policy mode (not RBAC) so a Contributor pipeline principal can grant the app identity.
    enableRbacAuthorization: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    accessPolicies: [
      for oid in readerObjectIds: {
        tenantId: subscription().tenantId
        objectId: oid
        permissions: {
          secrets: ['get', 'list']
        }
      }
    ]
  }
}

resource vaultSecrets 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = [
  for s in secrets: {
    parent: vault
    name: s.name
    properties: {
      value: s.value
    }
  }
]

output name string = vault.name
output vaultUri string = vault.properties.vaultUri
