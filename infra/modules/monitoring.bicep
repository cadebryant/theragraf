/*
  monitoring.bicep — Log Analytics workspace + Application Insights
*/

param location string
param suffix string

var logAnalyticsName = 'theragraf-logs-${suffix}'
var appInsightsName  = 'theragraf-ai-${suffix}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
	sku: {
	  name: 'PerGB2018'
	}
	retentionInDays: 730
	// totalRetentionInDays sets the archive tier (cheaper, still retained) up to 2190 days (6 years).
	// BCP037 is a type-registry gap — the property is valid in ARM at api-version 2023-09-01.
	#disable-next-line BCP037
	totalRetentionInDays: 2190
	publicNetworkAccessForIngestion: 'Enabled'
	publicNetworkAccessForQuery: 'Enabled'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
	Application_Type: 'web'
	WorkspaceResourceId: logAnalytics.id
	RetentionInDays: 730
	publicNetworkAccessForIngestion: 'Enabled'
	publicNetworkAccessForQuery: 'Enabled'
  }
}

output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output logAnalyticsWorkspaceName string = logAnalytics.name
