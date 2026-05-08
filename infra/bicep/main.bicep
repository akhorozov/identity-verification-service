// ─────────────────────────────────────────────────────────────────────────────
// main.bicep — Address Validation Service deployment entry point
// Deploys: ACR, Log Analytics, ACA Environment, Container App
// SRS Ref: Appendix B | T14 subtasks #127-#131
// ─────────────────────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

@description('Deployment environment (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name used to derive all resource names')
param appName string = 'addr-validation'

@description('Container image tag to deploy (defaults to latest)')
param imageTag string = 'latest'

@description('SmartyStreets Auth ID (secret — pass via --parameters or Key Vault ref)')
@secure()
param smartyAuthId string

@description('SmartyStreets Auth Token (secret)')
@secure()
param smartyAuthToken string

@description('Comma-separated list of valid API keys for X-Api-Key auth')
@secure()
param apiKeys string

// ── Derived names ─────────────────────────────────────────────────────────────
var suffix      = '${appName}-${environment}'
var acrName     = replace('acr${appName}${environment}', '-', '')
var logName     = 'log-${suffix}'
var acaEnvName  = 'cae-${suffix}'
var acaAppName  = 'ca-${suffix}'
var cosmosName  = 'cosmos-${suffix}'

// ── Modules ───────────────────────────────────────────────────────────────────
module acr 'modules/container-registry.bicep' = {
  name: 'acr'
  params: {
    name:     acrName
    location: location
  }
}

module cosmosAutoscale 'modules/cosmos-autoscale.bicep' = {
  name: 'cosmosAutoscale'
  params: {
    accountName: cosmosName
    location:    location
    environment: environment
  }
}

module containerApp 'modules/container-app.bicep' = {
  name: 'containerApp'
  params: {
    location:       location
    acaEnvName:     acaEnvName
    acaAppName:     acaAppName
    logName:        logName
    acrLoginServer: acr.outputs.loginServer
    acrName:        acrName
    imageTag:       imageTag
    appName:        appName
    environment:    environment
    smartyAuthId:   smartyAuthId
    smartyAuthToken:smartyAuthToken
    apiKeys:        apiKeys
    cosmosEndpoint: cosmosAutoscale.outputs.endpoint
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output acrLoginServer  string = acr.outputs.loginServer
output containerAppUrl string = containerApp.outputs.fqdn
output cosmosEndpoint  string = cosmosAutoscale.outputs.endpoint
