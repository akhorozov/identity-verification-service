// ─────────────────────────────────────────────────────────────────────────────
// modules/container-app.bicep
// Creates: Log Analytics, ACA Environment, Container App (2-10 replicas, HTTPS)
// T14 subtasks: #128 (replicas/scaling), #129 (HTTPS ingress), #130 (ACR), #131 (Dapr)
// ─────────────────────────────────────────────────────────────────────────────

@description('Azure region')
param location string = resourceGroup().location

@description('Log Analytics workspace name')
param logName string

@description('ACA Environment name')
param acaEnvName string

@description('Container App name')
param acaAppName string

@description('ACR login server (e.g. myacr.azurecr.io)')
param acrLoginServer string

@description('ACR resource name (for role assignment)')
param acrName string

@description('Container image tag')
param imageTag string = 'latest'

@description('Application name (used for image name)')
param appName string

@description('Deployment environment')
@allowed(['dev', 'staging', 'prod'])
param environment string

@secure()
param smartyAuthId string

@secure()
param smartyAuthToken string

@secure()
param apiKeys string

@description('CosmosDB endpoint URL')
param cosmosEndpoint string

// ── Log Analytics ─────────────────────────────────────────────────────────────
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── ACA Environment ───────────────────────────────────────────────────────────
resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: acaEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logWorkspace.properties.customerId
        sharedKey:  logWorkspace.listKeys().primarySharedKey
      }
    }
    daprAIConnectionString: ''
  }
}

// ── ACR pull role for the Container App managed identity ──────────────────────
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'  // AcrPull built-in

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

resource acaIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${acaAppName}'
  location: location
}

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, acaIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: acaIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Container App ─────────────────────────────────────────────────────────────
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: acaAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${acaIdentity.id}': {} }
  }
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      // External HTTPS ingress (#129)
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      // ACR via managed identity (#130)
      registries: [
        {
          server:   acrLoginServer
          identity: acaIdentity.id
        }
      ]
      // Dapr sidecar (#131)
      dapr: {
        enabled:     true
        appId:       'address-validation-api'
        appPort:     8080
        appProtocol: 'http'
        logLevel:    environment == 'prod' ? 'warn' : 'info'
      }
      secrets: [
        { name: 'smarty-auth-id',    value: smartyAuthId    }
        { name: 'smarty-auth-token', value: smartyAuthToken }
        { name: 'api-keys',          value: apiKeys         }
      ]
    }
    template: {
      containers: [
        {
          name:  'api'
          image: '${acrLoginServer}/${appName}:${imageTag}'
          resources: {
            cpu:    '0.5'
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT',              value: environment == 'prod' ? 'Production' : 'Staging' }
            { name: 'ASPNETCORE_URLS',                     value: 'http://+:8080' }
            { name: 'AddressValidation__CosmosDb__Endpoint', value: cosmosEndpoint }
            { name: 'Smarty__AuthId',    secretRef: 'smarty-auth-id'    }
            { name: 'Smarty__AuthToken', secretRef: 'smarty-auth-token' }
            { name: 'ApiKeys',           secretRef: 'api-keys'          }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      // 2-10 replicas with HTTP-based autoscaling (#128)
      scale: {
        minReplicas: 2
        maxReplicas: 10
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [ acrPullRole ]
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
