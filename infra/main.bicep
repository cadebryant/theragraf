/*
  main.bicep — Theragraf root orchestrator
  Deploys all modules in dependency order and wires outputs between them.

  Deploy:
	az deployment group create \
	  --resource-group theragraf-rg \
	  --template-file infra/main.bicep \
	  --parameters infra/parameters/dev.bicepparam
*/

targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────────────────────

@description('Short environment tag used in resource names (dev | prod).')
@allowed(['dev', 'prod'])
param environmentName string = 'dev'

@description('Primary Azure region for all resources.')
param location string = resourceGroup().location

@description('Name of the Azure Functions app.')
param functionAppName string = 'theragraf-functions'

@description('Name of the Azure OpenAI deployment (model deployment, not the account).')
param openAiDeploymentName string = 'gpt-4o-mini'

@description('Azure OpenAI model capacity (thousands of tokens per minute).')
param openAiCapacity int = 10

@description('Storage account name. Set this to your existing account name to avoid creating a duplicate.')
param storageAccountName string = 'theragrafstorage'

// ── Shared naming suffix ──────────────────────────────────────────────────────

var suffix = toLower(environmentName)

// ── Modules ───────────────────────────────────────────────────────────────────

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
	location: location
	suffix: suffix
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
	location: location
	suffix: suffix
	storageAccountName: storageAccountName
  }
}
}

module openai 'modules/openai.bicep' = {
  name: 'openai'
  params: {
	location: location
	suffix: suffix
	deploymentName: openAiDeploymentName
	capacity: openAiCapacity
  }
}

module language 'modules/language.bicep' = {
  name: 'language'
  params: {
	location: location
	suffix: suffix
  }
}

module functionApp 'modules/functionApp.bicep' = {
  name: 'functionApp'
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

module roleAssignments 'modules/roleAssignments.bicep' = {
  name: 'roleAssignments'
  params: {
	functionAppPrincipalId: functionApp.outputs.principalId
	storageAccountName: storage.outputs.storageAccountName
	openAiAccountName: openai.outputs.accountName
	languageAccountName: language.outputs.accountName
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output functionAppName string = functionAppName
output functionAppHostname string = functionApp.outputs.defaultHostname
output storageAccountName string = storage.outputs.storageAccountName
output openAiEndpoint string = openai.outputs.endpoint
output languageEndpoint string = language.outputs.endpoint
output appInsightsName string = monitoring.outputs.appInsightsName
