targetScope = 'subscription'

@minLength(1)
@description('Primary location')
param location string = 'swedencentral'

@minLength(1)
param environmentName string

@description('App login username')
param authUsername string

@secure()
@description('App login password')
param authPassword string

@description('Voice Live mode: model or agent')
param voiceLiveMode string = 'agent'

@description('Voice Live API version')
param apiVersion string = '2025-10-01'

@description('Linux runtime; empty for self-contained deploy')
param linuxFxVersion string = 'DOTNETCORE|10.0'
param resourceGroupName string = 'rg-${environmentName}'

var token = uniqueString(subscription().id, environmentName, location)
var tags = { 'azd-env-name': environmentName }

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    token: token
    tags: tags
    authUsername: authUsername
    authPassword: authPassword
    voiceLiveMode: voiceLiveMode
    apiVersion: apiVersion
    linuxFxVersion: linuxFxVersion
    environmentName: environmentName
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output SERVICE_WEB_NAME string = resources.outputs.webAppName
output SERVICE_WEB_URI string = resources.outputs.webAppUri
output AZURE_AI_SERVICES_NAME string = resources.outputs.aiServicesName
output AZURE_AI_PROJECT_NAME string = resources.outputs.projectName
output AZURE_AI_PROJECT_ENDPOINT string = resources.outputs.projectEndpoint
