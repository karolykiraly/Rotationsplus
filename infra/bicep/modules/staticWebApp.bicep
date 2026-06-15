param name string
param location string
param tags object
param sku string

resource swa 'Microsoft.Web/staticSites@2024-04-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    // Content is deployed by the pipeline (swa deploy / SWA CLI), not from a connected repo.
    allowConfigFileUpdates: true
  }
}

output name string = swa.name
output defaultHostname string = swa.properties.defaultHostname
