/*
  cosmos.bicep — Azure Cosmos DB for NoSQL account, database, and sessions container.
  Uses serverless capacity mode (pay-per-request, ideal for Functions workloads).
  Partition key: /clientId
*/

param location string
param accountName string

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
	databaseAccountOfferType: 'Standard'
	consistencyPolicy: {
	  defaultConsistencyLevel: 'Session'
	}
	locations: [
	  {
		locationName: location
		failoverPriority: 0
		isZoneRedundant: false
	  }
	]
	capabilities: [
	  { name: 'EnableServerless' }
	]
	enableAutomaticFailover: false
	enableMultipleWriteLocations: false
	publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'theragraf'
  properties: {
	resource: { id: 'theragraf' }
  }
}

resource sessionsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'sessions'
  properties: {
	resource: {
	  id: 'sessions'
	  partitionKey: {
		paths: [ '/clientId' ]
		kind: 'Hash'
	  }
	  indexingPolicy: {
		indexingMode: 'consistent'
		includedPaths: [ { path: '/*' } ]
		excludedPaths: [
		  { path: '/soapNote/*' }
		  { path: '/redactionMap/*' }
		]
		compositeIndexes: [
		  [
			{ path: '/clientId', order: 'ascending' }
			{ path: '/id',       order: 'descending' }
		  ]
		  [
			{ path: '/clientId',     order: 'ascending' }
			{ path: '/discipline',   order: 'ascending' }
		  ]
		  [
			{ path: '/clientId',     order: 'ascending' }
			{ path: '/therapistName', order: 'ascending' }
		  ]
		  [
			{ path: '/clientId',     order: 'ascending' }
			{ path: '/createdAt',    order: 'descending' }
		  ]
		]
	  }
	}
  }
}

output accountName string = cosmosAccount.name
output endpoint string = cosmosAccount.properties.documentEndpoint
