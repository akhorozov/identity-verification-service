// ─────────────────────────────────────────────────────────────────────────────
// modules/container-registry.bicep — Azure Container Registry
// SKU: Basic (dev/staging), Standard (prod)
// ─────────────────────────────────────────────────────────────────────────────

@description('ACR name (alphanumeric, globally unique)')
param name string

@description('Azure region')
param location string = resourceGroup().location

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false   // Use managed identity, not admin credentials
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Disabled'
  }
}

output loginServer string = acr.properties.loginServer
output resourceId  string = acr.id
