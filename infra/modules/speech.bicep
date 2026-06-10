/*
  speech.bicep — Azure AI Speech Service account
  Creates a new S0-tier Speech resource and outputs its region and key.
  The key is only used by the Function App's speech-token endpoint;
  the browser never receives it directly.
*/

param location string
param suffix string

@description('Name for the Speech resource (must be globally unique).')
param speechAccountName string = 'theragraf-speech-${suffix}'

resource speechAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: speechAccountName
  location: location
  kind: 'SpeechServices'
  sku: {
	name: 'S0'
  }
  properties: {
	publicNetworkAccess: 'Enabled'
	customSubDomainName: speechAccountName
  }
}

output accountName string = speechAccount.name
output region string = speechAccount.location
output endpoint string = speechAccount.properties.endpoint
#disable-next-line outputs-should-not-contain-secrets
output apiKey string = speechAccount.listKeys().key1
