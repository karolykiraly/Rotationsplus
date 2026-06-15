using './main.bicep'

param environment = 'dev'

// Shared ACR (created during Azure foundation; see Docs/Azure_Foundation.md).
param acrLoginServer = 'rotationsplusacr-cvgsceanh0fpbdh3.azurecr.io'

// Pipeline-supplied at deploy time.
param imageTag = readEnvironmentVariable('IMAGE_TAG', 'latest')
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', '')
param spaAllowedOrigin = readEnvironmentVariable('SPA_ALLOWED_ORIGIN', '')

// appIdentityName defaults to id-rplus-dev (owner-bootstrapped UAMI with AcrPull on the ACR).
