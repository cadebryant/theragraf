/*
  cognitiveRoleAssignments.bicep — Cognitive Services RBAC for the Function App
  Managed Identity. Must be deployed at subscription scope and target
  Default-Web-EastUS (where the OpenAI and Language accounts live).

  Called from main.bicep with:
	scope: az.resourceGroup('Default-Web-EastUS')

	Cognitive Services OpenAI User — Azure OpenAI (Semantic Kernel)
	Cognitive Services User        — Azure AI Language (PII redaction)
*/

param functionAppPrincipalId string
param openAiAccountName string
param languageAccountName string

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: openAiAccountName
}

resource languageAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: languageAccountName
}

var cognitiveServicesOpenAiUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
var cognitiveServicesUser       = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')

resource openAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAiAccount.id, functionAppPrincipalId, cognitiveServicesOpenAiUser)
  scope: openAiAccount
  properties: {
	roleDefinitionId: cognitiveServicesOpenAiUser
	principalId: functionAppPrincipalId
	principalType: 'ServicePrincipal'
  }
}

resource languageUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(languageAccount.id, functionAppPrincipalId, cognitiveServicesUser)
  scope: languageAccount
  properties: {
	roleDefinitionId: cognitiveServicesUser
	principalId: functionAppPrincipalId
	principalType: 'ServicePrincipal'
  }
}
