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

var planName = '${functionAppName}-plan'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  sku: {
	name: 'Y1'
	tier: 'Dynamic'
  }
  kind: 'linux'
  properties: {
	reserved: true  // required for Linux
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
	type: 'SystemAssigned'
  }
  properties: {
	serverFarmId: appServicePlan.id
	reserved: true
	httpsOnly: true
	siteConfig: {
	  linuxFxVersion: 'dotnet-isolated|10.0'
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
		// ── Table Storage (Managed Identity — account name only) ─────────────
		{
		  name: 'AzureStorage__AccountName'
		  value: storageAccountName
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
	  ]
	}
  }
}

output principalId string = functionApp.identity.principalId
output defaultHostname string = functionApp.properties.defaultHostName
