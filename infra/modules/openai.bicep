/*
  openai.bicep — Azure OpenAI account + gpt-4o-mini model deployment
*/

param location string
param suffix string
param deploymentName string
param capacity int

var accountName = 'theragraf-oai-${suffix}'

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  kind: 'OpenAI'
  sku: {
	name: 'S0'
  }
  properties: {
	customSubDomainName: accountName
	publicNetworkAccess: 'Enabled'
  }
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAiAccount
  name: deploymentName
  sku: {
	name: 'Standard'
	capacity: capacity
  }
  properties: {
	model: {
	  format: 'OpenAI'
	  name: 'gpt-4o-mini'
	  version: '2024-07-18'
	}
	versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
}

output accountName string = openAiAccount.name
output endpoint string = openAiAccount.properties.endpoint
