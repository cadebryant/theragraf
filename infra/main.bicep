/*
  main.bicep — Theragraf root orchestrator
  Deploys all modules in dependency order and wires outputs between them.

  Resources in theragraf-rg    : storage, monitoring, functionApp, roleAssignments
  Resources in Default-Web-EastUS : openai (existing), language (existing),
                                    cognitiveRoleAssignments

  Deploy:
    az deployment sub create \
      --location eastus \
      --template-file infra/main.bicep \
      --parameters infra/parameters/dev.bicepparam
*/

targetScope = 'subscription'

// -- Parameters ----------------------------------------------------------------

@description('Short environment tag used in resource names (dev | prod).')
@allowed(['dev', 'prod'])
param environmentName string = 'dev'

@description('Primary Azure region for all resources.')
param location string = 'eastus'

@description('Name of the resource group that owns the Function App and Storage.')
param appResourceGroup string = 'theragraf-rg'

@description('Name of the resource group where Cognitive Services accounts live.')
param cognitiveResourceGroup string = 'Default-Web-EastUS'

@description('Name of the Azure Functions app.')
param functionAppName string = 'theragraf-functions'

@description('Name of the Azure OpenAI deployment (model deployment, not the account).')
param openAiDeploymentName string = 'gpt-4o-mini'

@description('Name of the existing Azure OpenAI account.')
param openAiAccountName string = 'theragraf-oai'

@description('Name of the existing Azure AI Language account.')
param languageAccountName string = 'theragraf-language'

@description('Storage account name. Must match the existing account in appResourceGroup.')
param storageAccountName string = 'theragrafstorage'

// -- Shared naming suffix ------------------------------------------------------

var suffix = toLower(environmentName)

// -- Resource group references -------------------------------------------------

resource appRg      'Microsoft.Resources/resourceGroups@2023-07-01' existing = { name: appResourceGroup }
resource cognitiveRg 'Microsoft.Resources/resourceGroups@2023-07-01' existing = { name: cognitiveResourceGroup }

// -- Modules (app resource group) ----------------------------------------------

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: appRg
  params: {
    location: location
    suffix: suffix
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  scope: appRg
  params: {
    location: location
    suffix: suffix
    storageAccountName: storageAccountName
  }
}

// -- Modules (cognitive resource group — read-only existing) -------------------

module openai 'modules/openai.bicep' = {
  name: 'openai'
  scope: cognitiveRg
  params: {
    accountName: openAiAccountName
  }
}

module language 'modules/language.bicep' = {
  name: 'language'
  scope: cognitiveRg
  params: {
    accountName: languageAccountName
  }
}

// -- Function App (app resource group) ----------------------------------------

module functionApp 'modules/functionApp.bicep' = {
  name: 'functionApp'
  scope: appRg
  params: {
    location: location
    functionAppName: functionAppName
    storageAccountName: storage.outputs.storageAccountName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    openAiEndpoint: openai.outputs.endpoint
    openAiDeploymentName: openAiDeploymentName
    languageEndpoint: language.outputs.endpoint
  }
}

// -- Role assignments — storage (app resource group) ---------------------------

module roleAssignments 'modules/roleAssignments.bicep' = {
  name: 'roleAssignments'
  scope: appRg
  params: {
    functionAppPrincipalId: functionApp.outputs.principalId
    storageAccountName: storage.outputs.storageAccountName
  }
}

// -- Role assignments — cognitive services (cognitive resource group) -----------

module cognitiveRoleAssignments 'modules/cognitiveRoleAssignments.bicep' = {
  name: 'cognitiveRoleAssignments'
  scope: cognitiveRg
  params: {
    functionAppPrincipalId: functionApp.outputs.principalId
    openAiAccountName: openai.outputs.accountName
    languageAccountName: language.outputs.accountName
  }
}

// -- Outputs -------------------------------------------------------------------

output functionAppName string = functionAppName
output functionAppHostname string = functionApp.outputs.defaultHostname
output storageAccountName string = storage.outputs.storageAccountName
output openAiEndpoint string = openai.outputs.endpoint
output languageEndpoint string = language.outputs.endpoint
output appInsightsName string = monitoring.outputs.appInsightsName
