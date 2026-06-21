param name string
param location string
param tags object

@description('Blob containers to create (private access). Names must be lowercase, 3-63 chars.')
param containers array = []

// Program/hospital images live here. The account stays PRIVATE (no anonymous blob access); the API
// mints short-lived read SAS URLs from the account key, which it reads from Key Vault. This key-based
// path is deliberate: the pipeline principal is only Contributor and cannot create the role assignment
// a managed-identity (user-delegation SAS) approach would require. See infra/bicep/main.bicep.
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowSharedKeyAccess: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource blobContainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [
  for c in containers: {
    parent: blobService
    name: c
    properties: {
      publicAccess: 'None'
    }
  }
]

// AccountKey connection string (account key via listKeys — Contributor has the listKeys action).
// Secured so it never lands in deployment outputs/logs; the caller stores it in Key Vault.
@secure()
output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

output name string = storage.name
output blobEndpoint string = storage.properties.primaryEndpoints.blob
