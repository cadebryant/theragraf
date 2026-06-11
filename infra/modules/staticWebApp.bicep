/*
  staticWebApp.bicep — Azure Static Web Apps (Standard) for the Theragraf React SPA.

  Standard SKU is required for the linked backend feature, which proxies /api/* calls
  to the Function App without any CORS configuration. The browser communicates only with
  the SWA hostname; the Function App is never exposed directly to the browser.
*/

param location string
param suffix string

@description('Name for the Static Web App (must be globally unique).')
param staticWebAppName string = 'theragraf-web-${suffix}'

@description('Resource ID of the Function App to link as the API backend.')
param functionAppResourceId string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
	name: 'Standard'
	tier: 'Standard'
  }
  properties: {
	buildProperties: {
	  skipGithubActionWorkflowGeneration: true
	}
  }
}

// Link the Function App as the /api backend.
// SWA Standard proxies /api/* to the linked backend — no CORS required.
resource linkedBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  parent: staticWebApp
  name: 'theragraf-functions-backend'
  properties: {
	backendResourceId: functionAppResourceId
	region: location
  }
}

output staticWebAppName string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname
#disable-next-line outputs-should-not-contain-secrets
output deploymentToken string = staticWebApp.listSecrets().properties.apiKey
