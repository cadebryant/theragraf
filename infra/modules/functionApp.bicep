/*
  functionApp.bicep — Consumption App Service Plan + Azure Functions app
					  (.NET 10 isolated, system-assigned Managed Identity)

  App settings use Managed Identity for storage (AzureWebJobsStorage__accountName)
  and pass endpoints only (no keys) for OpenAI and Language — keys are granted
  via RBAC role assignments in roleAssignments.bicep.
*/

param location string
param functionAppName string
param storageAccountName string
param appInsightsConnectionString string
param openAiEndpoint string
param openAiDeploymentName string
param languageEndpoint string
param tenantId string
param apiClientId string
param cosmosEndpoint string

@description('Name of the existing Consumption App Service Plan. Defaults to the plan created alongside the Function App.')
param appServicePlanName string = '${functionAppName}-plan'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: appServicePlanName
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
	type: 'SystemAssigned'
  }
  properties: {
	serverFarmId: appServicePlan.id
	httpsOnly: true
	siteConfig: {
	  netFrameworkVersion: 'v10.0'
	  functionAppScaleLimit: 200
	  appSettings: [
		// ── Functions runtime ────────────────────────────────────────────────
		{
		  name: 'FUNCTIONS_EXTENSION_VERSION'
		  value: '~4'
		}
		{
		  name: 'FUNCTIONS_WORKER_RUNTIME'
		  value: 'dotnet-isolated'
		}
		{
		  name: 'FUNCTIONS_WORKER_RUNTIME_VERSION'
		  value: '10'
		}
		// ── Storage (Managed Identity — no connection string) ────────────────
		{
		  name: 'AzureWebJobsStorage__accountName'
		  value: storageAccountName
		}
		// ── Application Insights ─────────────────────────────────────────────
		{
		  name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
		  value: appInsightsConnectionString
		}
		// ── Azure OpenAI ─────────────────────────────────────────────────────
		{
		  name: 'AzureOpenAI__Endpoint'
		  value: openAiEndpoint
		}
		{
		  name: 'AzureOpenAI__DeploymentName'
		  value: openAiDeploymentName
		}
		// ── Azure AI Language ────────────────────────────────────────────────
		{
		  name: 'AzureLanguage__Endpoint'
		  value: languageEndpoint
		}
		// ── Entra ID authentication ──────────────────────────────────────────
		{
		  name: 'AzureAd__TenantId'
		  value: tenantId
		}
		{
		  name: 'AzureAd__ClientId'
		  value: apiClientId
		}
		// ── Cosmos DB (Managed Identity — endpoint only) ─────────────────────
		{
		  name: 'CosmosDb__AccountEndpoint'
		  value: cosmosEndpoint
		}
		{
		  name: 'CosmosDb__DatabaseName'
		  value: 'theragraf'
		}
		{
		  name: 'CosmosDb__ContainerName'
		  value: 'sessions'
		}
	  ]
	}
  }
}

output principalId string = functionApp.identity.principalId
output defaultHostname string = functionApp.properties.defaultHostName
