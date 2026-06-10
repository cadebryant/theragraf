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
param openAiDeploymentName string = 'gpt-4o'

@description('Name of the existing Azure OpenAI account.')
param openAiAccountName string = 'theragraf-oai'

@description('Name of the existing Azure AI Language account.')
param languageAccountName string = 'theragraf-language'

@description('Storage account name. Must match the existing account in appResourceGroup.')
param storageAccountName string = 'theragrafstorage'

@description('Entra ID tenant ID for JWT authentication.')
param tenantId string = '9525f140-7768-4f65-8ebb-54bd5151f7cb'

@description('Client ID of the theragraf-api Entra ID app registration.')
param apiClientId string = 'd84a7ccd-aaa1-4adf-8211-7c03fa3d319a'

@description('Name for the Cosmos DB account (must be globally unique).')
param cosmosAccountName string = 'theragraf-cosmos'

@description('Name for the Azure Key Vault (must be globally unique, 3-24 chars).')
param keyVaultName string = 'theragraf-kv-${toLower(environmentName)}'

@description('Name of the existing App Service Plan hosting the Function App.')
param appServicePlanName string = '${functionAppName}-plan'

@description('Name for the Azure AI Speech account (must be globally unique).')
param speechAccountName string = 'theragraf-speech-${toLower(environmentName)}'

@description('Optional Entra ID object ID of a developer to grant Cosmos Data Explorer access.')
param developerPrincipalId string = ''

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

// -- Azure AI Speech (app resource group) ------------------------------------

module speech 'modules/speech.bicep' = {
  name: 'speech'
  scope: appRg
  params: {
    location: location
    suffix: suffix
    speechAccountName: speechAccountName
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
    tenantId: tenantId
    apiClientId: apiClientId
    cosmosEndpoint: cosmos.outputs.endpoint
    appServicePlanName: appServicePlanName
    keyVaultUri: keyVault.outputs.vaultUri
    speechRegion: speech.outputs.region
    speechApiKey: speech.outputs.apiKey
  }
}

// -- Cosmos DB (app resource group) -------------------------------------------

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  scope: appRg
  params: {
    location: location
    accountName: cosmosAccountName
  }
}

// -- Key Vault (app resource group) -------------------------------------------

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  scope: appRg
  params: {
    location: location
    suffix: suffix
    keyVaultName: keyVaultName
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

// -- Role assignments — Cosmos DB (app resource group) ------------------------

module cosmosRoleAssignment 'modules/cosmosRoleAssignment.bicep' = {
  name: 'cosmosRoleAssignment'
  scope: appRg
  params: {
    cosmosAccountName: cosmos.outputs.accountName
    functionAppPrincipalId: functionApp.outputs.principalId
    developerPrincipalId: developerPrincipalId
  }
}

// -- Role assignments — Key Vault (app resource group) -------------------------

module keyVaultRoleAssignment 'modules/keyVaultRoleAssignment.bicep' = {
  name: 'keyVaultRoleAssignment'
  scope: appRg
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    functionAppPrincipalId: functionApp.outputs.principalId
    developerPrincipalId: developerPrincipalId
  }
}

// -- Outputs -------------------------------------------------------------------

output functionAppName string = functionAppName
output functionAppHostname string = functionApp.outputs.defaultHostname
output storageAccountName string = storage.outputs.storageAccountName
output cosmosAccountName string = cosmos.outputs.accountName
output cosmosEndpoint string = cosmos.outputs.endpoint
output openAiEndpoint string = openai.outputs.endpoint
output languageEndpoint string = language.outputs.endpoint
output appInsightsName string = monitoring.outputs.appInsightsName
output keyVaultName string = keyVault.outputs.keyVaultName
output keyVaultUri string = keyVault.outputs.vaultUri
output speechAccountName string = speech.outputs.accountName
output speechRegion string = speech.outputs.region
@description('Copy this value into AzureSpeech__Region in local.settings.json')
output localSettingsSpeechRegion string = speech.outputs.region
@description('Copy this value into AzureSpeech__ApiKey in local.settings.json')
#disable-next-line outputs-should-not-contain-secrets
output localSettingsSpeechApiKey string = speech.outputs.apiKey
