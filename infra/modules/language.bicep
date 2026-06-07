/*
  language.bicep — Azure AI Language account (used for PII redaction)
*/

param location string
param suffix string

var accountName = 'theragraf-lang-${suffix}'

resource languageAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  kind: 'TextAnalytics'
  sku: {
	name: 'S'
  }
  properties: {
	customSubDomainName: accountName
	publicNetworkAccess: 'Enabled'
  }
}

output accountName string = languageAccount.name
output endpoint string = languageAccount.properties.endpoint
