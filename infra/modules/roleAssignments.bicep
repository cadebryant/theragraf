/*
  roleAssignments.bicep — Storage RBAC assignments for the Function App Managed Identity.
  Scoped to theragraf-rg (where the storage account lives).

  Cognitive Services role assignments are handled separately in
  cognitiveRoleAssignments.bicep, scoped to Default-Web-EastUS.

    Storage Blob Data Owner        — Durable Functions runtime (blobs)
    Storage Queue Data Contributor — Durable Functions runtime (queues)
    Storage Table Data Contributor — Session records (TableServiceClient)
*/

param functionAppPrincipalId string
param storageAccountName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var storageBlobDataOwner        = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var storageQueueDataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableDataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')

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
