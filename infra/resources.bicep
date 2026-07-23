param location string
param token string
param tags object
param authUsername string
@secure()
param authPassword string
param voiceLiveMode string
param apiVersion string
param linuxFxVersion string

var aiName = 'ai${token}'
var projectName = 'proj-default'

resource ai 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: aiName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    allowProjectManagement: true
    customSubDomainName: aiName
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: ai
  name: projectName
  location: location
  identity: { type: 'SystemAssigned' }
  properties: { displayName: 'Voice Live Avatar' }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-${token}'
  location: location
  tags: tags
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30 }
}

resource appi 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${token}'
  location: location
  tags: tags
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id }
}

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: 'plan-${token}'
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: 'B1' }
  properties: { reserved: true }
}

resource site 'Microsoft.Web/sites@2024-11-01' = {
  name: 'app-${token}'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      webSocketsEnabled: true
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: '/api/health'
      appSettings: [
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appi.properties.ConnectionString }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConfigDir', value: 'config' }
        { name: 'VoiceLive__ConfigDir', value: 'config' }
        { name: 'VoiceLive__Endpoint', value: ai.properties.endpoint }
        { name: 'VoiceLive__Mode', value: voiceLiveMode }
        { name: 'VoiceLive__ApiVersion', value: apiVersion }
        { name: 'VoiceLive__AllowedOrigins__0', value: 'https://app-${token}.azurewebsites.net' }
        { name: 'Auth__Username', value: authUsername }
        { name: 'Auth__Password', value: authPassword }
      ]
    }
  }
}

var cognitiveServicesUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
var foundryUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '53ca6127-db72-4b80-b1b0-d745d6d5456d')

resource raCog 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: ai
  name: guid(ai.id, site.id, 'cog-user')
  properties: { principalId: site.identity.principalId, principalType: 'ServicePrincipal', roleDefinitionId: cognitiveServicesUser }
}

resource raProj 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: project
  name: guid(project.id, site.id, 'foundry-user')
  properties: { principalId: site.identity.principalId, principalType: 'ServicePrincipal', roleDefinitionId: foundryUser }
}

output webAppName string = site.name
output webAppUri string = 'https://${site.properties.defaultHostName}'
output aiServicesName string = ai.name
output projectName string = project.name
output projectEndpoint string = 'https://${aiName}.services.ai.azure.com/api/projects/${projectName}'
