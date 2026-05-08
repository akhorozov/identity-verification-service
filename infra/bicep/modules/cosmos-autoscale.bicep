// ─────────────────────────────────────────────────────────────────────────────
// modules/cosmos-autoscale.bicep
// Creates a CosmosDB account with autoscale throughput (400–4000 RU/s)
// and two containers: cache (TTL 90d) and audit (TTL 365d)
// T14 subtask #135 | T15 subtasks #135 #136 #137 #138
// ─────────────────────────────────────────────────────────────────────────────

@description('CosmosDB account name')
param accountName string

@description('Azure region')
param location string = resourceGroup().location

@description('Deployment environment (affects backup policy)')
@allowed(['dev', 'staging', 'prod'])
param environment string

// Partition key is /stateAbbreviation — 50 states = even distribution
var cacheContainerName = 'address-cache'
var auditContainerName = 'audit-events'
var databaseName       = 'address-validation'

// TTLs per SRS NFR-024 — T15 #138
var cacheTtlSeconds = 90  * 24 * 60 * 60   // 90 days
var auditTtlSeconds = 365 * 24 * 60 * 60   // 365 days

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-02-15-preview' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName:     location
        failoverPriority: 0
        isZoneRedundant:  environment == 'prod'
      }
    ]
    databaseAccountOfferType: 'Standard'
    enableAutomaticFailover:   environment == 'prod'
    enableFreeTier:            environment == 'dev'
    backupPolicy: environment == 'prod' ? {
      type: 'Continuous'
      continuousModeProperties: { tier: 'Continuous7Days' }
    } : {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
      }
    }
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-02-15-preview' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: { id: databaseName }
  }
}

// ── Cache container — partition key: /stateAbbreviation, TTL: 90 days ─────────
resource cacheContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-02-15-preview' = {
  parent: database
  name: cacheContainerName
  properties: {
    resource: {
      id: cacheContainerName
      partitionKey: {
        paths: [ '/stateAbbreviation' ]
        kind: 'Hash'
        version: 2
      }
      defaultTtl: cacheTtlSeconds
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [ { path: '/*' } ]
        excludedPaths: [ { path: '/"_etag"/?' } ]
      }
    }
    options: {
      autoscaleSettings: {
        maxThroughput: 4000   // 400–4000 RU/s autoscale (#135)
      }
    }
  }
}

// ── Audit container — partition key: /correlationId, TTL: 365 days ───────────
resource auditContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-02-15-preview' = {
  parent: database
  name: auditContainerName
  properties: {
    resource: {
      id: auditContainerName
      partitionKey: {
        paths: [ '/correlationId' ]
        kind: 'Hash'
        version: 2
      }
      defaultTtl: auditTtlSeconds
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [
          { path: '/correlationId/?' }
          { path: '/timestamp/?'     }
          { path: '/eventType/?'     }
        ]
        excludedPaths: [ { path: '/*' } ]
      }
    }
    options: {
      autoscaleSettings: {
        maxThroughput: 4000
      }
    }
  }
}

output endpoint    string = cosmosAccount.properties.documentEndpoint
output accountName string = cosmosAccount.name
output databaseName string = databaseName
