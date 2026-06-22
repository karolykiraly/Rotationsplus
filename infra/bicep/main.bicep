targetScope = 'resourceGroup'

// =====================================================================================
// Rotations Plus — environment infrastructure (lean P1 footprint).
// Deployed per environment into the pre-created rg-rplus-<env>. Clone of SkyLimit patterns,
// scaled to the 2-app topology. Service Bus / Redis / Blob are added when a module needs them.
//
// RBAC note: the pipeline service principal is Contributor (cannot create role assignments),
// so this template uses Key Vault ACCESS POLICIES (not RBAC) and a PRE-CREATED user-assigned
// managed identity that the owner has already granted AcrPull on the shared ACR.
// See Docs/Azure_Foundation.md §"Bootstrap".
// =====================================================================================

@allowed(['dev', 'preprod', 'prod'])
param environment string

param location string = resourceGroup().location

param namePrefix string = 'rplus'

@description('Shared ACR login server, e.g. rotationsplusacr-xxxx.azurecr.io')
param acrLoginServer string

@description('Name of the pre-created user-assigned managed identity in THIS resource group (owner-bootstrapped with AcrPull on the ACR).')
param appIdentityName string = 'id-${namePrefix}-${environment}'

@description('Container image tag to deploy (the pipeline build id).')
param imageTag string

@secure()
@description('PostgreSQL administrator password (from the DevOps variable group / Key Vault-backed secret).')
param postgresAdminPassword string

@description('Allowed CORS origin for the SPA (the SWA host). Optional on first deploy; set once the SWA host is known.')
param spaAllowedOrigin string = ''

// ---- Environment-tiered SKUs (single source of truth; Plan_Architecture.md §3.9) ----
var skuMap = {
  dev: {
    postgresTier: 'Burstable'
    postgresSku: 'Standard_B1ms'
    postgresStorageGb: 32
    logRetentionDays: 30
    containerCpu: '0.25'
    containerMemory: '0.5Gi'
    apiMinReplicas: 0
    apiMaxReplicas: 2
    swaSku: 'Free'
  }
  preprod: {
    postgresTier: 'Burstable'
    postgresSku: 'Standard_B1ms'
    postgresStorageGb: 32
    logRetentionDays: 30
    containerCpu: '0.5'
    containerMemory: '1.0Gi'
    apiMinReplicas: 1
    apiMaxReplicas: 3
    swaSku: 'Free'
  }
  prod: {
    postgresTier: 'GeneralPurpose'
    postgresSku: 'Standard_D2ds_v4'
    postgresStorageGb: 64
    logRetentionDays: 90
    containerCpu: '1.0'
    containerMemory: '2.0Gi'
    apiMinReplicas: 1
    apiMaxReplicas: 3
    swaSku: 'Standard'
  }
}
var sku = skuMap[environment]
var tags = {
  project: 'rotationsplus'
  environment: environment
  managedBy: 'bicep'
}
var suffix = uniqueString(resourceGroup().id)

// Pre-created identity used by both container apps for ACR pull + Key Vault access.
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: appIdentityName
}

module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    name: 'log-${namePrefix}-${environment}'
    location: location
    tags: tags
    retentionDays: sku.logRetentionDays
  }
}

module appInsights 'modules/applicationInsights.bicep' = {
  name: 'appInsights'
  params: {
    name: 'appi-${namePrefix}-${environment}'
    location: location
    tags: tags
    workspaceResourceId: logAnalytics.outputs.resourceId
  }
}

module postgres 'modules/postgresql.bicep' = {
  name: 'postgres'
  params: {
    name: 'psql-${namePrefix}-${environment}-${suffix}'
    location: location
    tags: tags
    tier: sku.postgresTier
    skuName: sku.postgresSku
    storageGb: sku.postgresStorageGb
    administratorLogin: 'rplusadmin'
    administratorPassword: postgresAdminPassword
    databaseName: 'rotationsplus'
  }
}

var dbConnectionString = 'Host=${postgres.outputs.fqdn};Port=5432;Database=rotationsplus;Username=rplusadmin;Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true'

// Blob storage for program/hospital images. Private account; the API mints read SAS from the
// account-key connection string (held in Key Vault). Storage names: 3-24 chars, lowercase alphanumeric.
var programImagesContainer = 'program-images'
// Student-uploaded rotation documents (PDF/JPEG/PNG). Same private account as images; the API mints
// short-lived read SAS from the same account-key connection string (held in Key Vault as blob-connection).
var documentsContainer = 'documents'
module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    // Storage account names are max 24 chars, lowercase alphanumeric. 'st' + prefix + env + 8 of the
    // suffix stays well under 24 for every env (preprod is the longest at 22), leaving headroom.
    name: 'st${namePrefix}${environment}${take(suffix, 8)}'
    location: location
    tags: tags
    containers: [programImagesContainer, documentsContainer]
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    // Key Vault names are max 24 chars, alphanumeric + hyphens, no consecutive hyphens.
    // 'kv' + prefix + env + 8 of the suffix keeps us under 24 for all environments.
    name: 'kv${namePrefix}${environment}${take(suffix, 8)}'
    location: location
    tags: tags
    readerObjectIds: [appIdentity.properties.principalId]
    secrets: [
      {
        name: 'db-connection'
        value: dbConnectionString
      }
      {
        name: 'blob-connection'
        value: storage.outputs.connectionString
      }
    ]
  }
}

module containerEnv 'modules/containerAppsEnvironment.bicep' = {
  name: 'containerEnv'
  params: {
    name: 'cae-${namePrefix}-${environment}'
    location: location
    tags: tags
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsResourceId: logAnalytics.outputs.resourceId
  }
}

var dbConnectionSecretUri = '${keyVault.outputs.vaultUri}secrets/db-connection'
var blobConnectionSecretUri = '${keyVault.outputs.vaultUri}secrets/blob-connection'

module api 'modules/containerApp.bicep' = {
  name: 'api'
  params: {
    name: 'ca-${namePrefix}-api-${environment}'
    location: location
    tags: tags
    environmentId: containerEnv.outputs.environmentId
    image: '${acrLoginServer}/rplus-api:${imageTag}'
    acrLoginServer: acrLoginServer
    userAssignedIdentityId: appIdentity.id
    targetPort: 8080
    externalIngress: true
    minReplicas: sku.apiMinReplicas
    maxReplicas: sku.apiMaxReplicas
    cpu: sku.containerCpu
    memory: sku.containerMemory
    keyVaultSecrets: [
      {
        name: 'db-connection'
        keyVaultUrl: dbConnectionSecretUri
      }
      {
        name: 'blob-connection'
        keyVaultUrl: blobConnectionSecretUri
      }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: environment == 'dev' ? 'Development' : 'Production' }
      { name: 'ConnectionStrings__rotationsdb', secretRef: 'db-connection' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
      { name: 'Cors__AllowedOrigins__0', value: spaAllowedOrigin }
      { name: 'Storage__Images__ConnectionString', secretRef: 'blob-connection' }
      { name: 'Storage__Images__ContainerName', value: programImagesContainer }
      { name: 'Storage__Documents__ConnectionString', secretRef: 'blob-connection' }
      { name: 'Storage__Documents__ContainerName', value: documentsContainer }
    ]
  }
}

module worker 'modules/containerApp.bicep' = {
  name: 'worker'
  params: {
    name: 'ca-${namePrefix}-worker-${environment}'
    location: location
    tags: tags
    environmentId: containerEnv.outputs.environmentId
    image: '${acrLoginServer}/rplus-worker:${imageTag}'
    acrLoginServer: acrLoginServer
    userAssignedIdentityId: appIdentity.id
    targetPort: 8080
    externalIngress: true
    minReplicas: 1
    maxReplicas: 1
    cpu: sku.containerCpu
    memory: sku.containerMemory
    keyVaultSecrets: [
      {
        name: 'db-connection'
        keyVaultUrl: dbConnectionSecretUri
      }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: environment == 'dev' ? 'Development' : 'Production' }
      { name: 'ConnectionStrings__rotationsdb', secretRef: 'db-connection' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
}

module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    name: 'swa-${namePrefix}-${environment}'
    // SWA is not available in every region; pin to a supported one.
    location: 'eastus2'
    tags: tags
    sku: sku.swaSku
  }
}

output apiFqdn string = api.outputs.fqdn
output workerFqdn string = worker.outputs.fqdn
output swaName string = staticWebApp.outputs.name
output swaDefaultHostname string = staticWebApp.outputs.defaultHostname
output keyVaultName string = keyVault.outputs.name
output postgresFqdn string = postgres.outputs.fqdn
