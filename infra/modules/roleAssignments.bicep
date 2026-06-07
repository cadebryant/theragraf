/*
  roleAssignments.bicep — RBAC assignments for the Function App's Managed Identity.

  Grants least-privilege access to each resource the app needs at runtime:
	• Storage Blob Data Owner        — Durable Functions runtime (blobs)
	• Storage Queue Data Contributor — Durable Functions runtime (queues)
	• Storage Table Data Contributor — Session records (TableServiceClient)
	• Cognitive Services OpenAI User — Azure OpenAI (Semantic Kernel)
	• Cognitive Services User        — Azure AI Language (PII redaction)
*/

param functionAppPrincipalId string
param storageAccountName string
param openAiAccountName string
param languageAccountName string

// ── Existing resource references ──────────────────────────────────────────────

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: openAiAccountName
}

resource languageAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: languageAccountName
}

// ── Built-in role definition IDs ──────────────────────────────────────────────

var storageBlobDataOwner        = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var storageQueueDataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableDataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var cognitiveServicesOpenAiUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
var cognitiveServicesUser       = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')

// ── Role assignments ──────────────────────────────────────────────────────────

resource blobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageBlobDataOwner)
  scope: storageAccount
  properties: {
	roleDefinitionId: storageBlobDataOwner
	principalId: functionAppPrincipalId
	principalType: 'ServicePrincipal'
  }
}

resource queueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageQueueDataContributor)
  scope: storageAccount
  properties: {
	roleDefinitionId: storageQueueDataContributor
	principalId: functionAppPrincipalId
	principalType: 'ServicePrincipal'
  }
}

resource tableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageTableDataContributor)
  scope: storageAccount
  properties: {
	roleDefinitionId: storageTableDataContributor
	principalId: functionAppPrincipalId
	principalType: 'ServicePrincipal'
  }
}

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
