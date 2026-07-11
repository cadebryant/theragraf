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

@description('URI of the Azure Key Vault used for redaction-map encryption key storage.')
param keyVaultUri string

@description('Azure Speech Service region, e.g. eastus.')
param speechRegion string

@description('Name of the existing Consumption App Service Plan. Defaults to the plan created alongside the Function App.')
param appServicePlanName string = '${functionAppName}-plan'

@description('Therapist name used for demo/seed records. Leave blank to disable demo mode.')
param demoTherapistName string = ''

@description('Key Vault URI used to build the migration-key secret reference. When empty the endpoint stays disabled.')
param keyVaultUri_migrationKey string = ''

@description('JWT claim type that carries the tenant identifier. Use "tid" for standard Entra ID; override for Entra External ID custom attributes (e.g. "extension_tenantId").')
param tenantIdClaimType string = 'tid'

@description('Display name for the synthetic self-hosted tenant shown in logs. Defaults to "Self-Hosted".')
param syntheticTenantName string = 'Self-Hosted'

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
	  minTlsVersion: '1.2'
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
		// ── Key Vault (redaction-map encryption key) ──────────────────────────
		{
		  name: 'KeyVault__VaultUri'
		  value: keyVaultUri
		}
		// ── Azure Speech (speech-token endpoint) ─────────────────────────────
		{
		  name: 'AzureSpeech__Region'
		  value: speechRegion
		}
		{
		  name: 'AzureSpeech__ApiKey'
		  value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/speech-api-key/)'
		}
		// ── Demo mode ────────────────────────────────────────────────────────
		{
		  name: 'Demo__TherapistName'
		  value: demoTherapistName
		}
		// ── Cosmos DB — additional container names ───────────────────────────
		{
		  name: 'CosmosDb__GoalsContainerName'
		  value: 'goals'
		}
		{
		  name: 'CosmosDb__ClientsContainerName'
		  value: 'clients'
		}
		{
		  name: 'CosmosDb__TenantsContainerName'
		  value: 'tenants'
		}
		{
		  name: 'CosmosDb__TherapistProfilesContainerName'
		  value: 'therapist-profiles'
		}
		{
		  name: 'CosmosDb__ProvidersContainerName'
		  value: 'providers'
		}
		// ── Multi-tenancy ────────────────────────────────────────────────────
		{
		  name: 'MultiTenant__TenantIdClaimType'
		  value: tenantIdClaimType
		}
		{
		  name: 'MultiTenant__SyntheticTenantName'
		  value: syntheticTenantName
		}
		// ── Admin / migration ────────────────────────────────────────────────
		{
		  name: 'Admin__MigrationKey'
		  value: empty(keyVaultUri_migrationKey) ? '' : '@Microsoft.KeyVault(SecretUri=${keyVaultUri_migrationKey}secrets/migration-key/)'
		}
	  ]
	}
  }
}

output principalId string = functionApp.identity.principalId
output defaultHostname string = functionApp.properties.defaultHostName
output resourceId string = functionApp.id
