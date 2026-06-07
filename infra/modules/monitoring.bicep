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
	retentionInDays: 30
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
	RetentionInDays: 30
	publicNetworkAccessForIngestion: 'Enabled'
	publicNetworkAccessForQuery: 'Enabled'
  }
}

output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
