/*
  storage.bicep — Storage Account for Azure Functions runtime (blobs, queues)
				  and Table Storage for session records.

  Name is deterministic per resource group using uniqueString so re-deployments
  are idempotent and the name never changes for a given environment.
*/

param location string
param suffix string

@description('Storage account name. Must match the existing account if one already exists in the resource group.')
param storageAccountName string = 'theragraf${take(uniqueString(resourceGroup().id, suffix), 6)}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
	name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
	accessTier: 'Hot'
	allowBlobPublicAccess: false
	allowSharedKeyAccess: true   // required by Durable Functions runtime internally
	minimumTlsVersion: 'TLS1_2'
	supportsHttpsTrafficOnly: true
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource sessionTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'SessionRecords'
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
