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
	disableLocalAuth: true  // key-based access disabled; Managed Identity only
	// Continuous backup (tier 7 = 7-day point-in-time restore).
	// Supports restore to any second within the retention window — satisfies
	// HIPAA contingency plan requirements for RTO/RPO on PHI data stores.
	// Upgrade to Continuous30Days for 30-day restore window when budget allows.
	backupPolicy: {
	  type: 'Continuous'
	  continuousModeProperties: {
		tier: 'Continuous7Days'
	  }
	}
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
			{ path: '/clientId',    order: 'ascending' }
			{ path: '/discipline',  order: 'ascending' }
		  ]
		  [
			{ path: '/clientId',      order: 'ascending' }
			{ path: '/therapistName', order: 'ascending' }
		  ]
		  [
			{ path: '/clientId',  order: 'ascending' }
			{ path: '/createdAt', order: 'descending' }
		  ]
		]
	  }
	}
  }
}

resource goalsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'goals'
  properties: {
	resource: {
	  id: 'goals'
	  partitionKey: {
		paths: [ '/clientId' ]
		kind: 'Hash'
	  }
	  indexingPolicy: {
		indexingMode: 'consistent'
		includedPaths: [ { path: '/*' } ]
		excludedPaths: [
		  { path: '/description/*' }
		  { path: '/progressNotes/*' }
		]
		compositeIndexes: [
		  [
			{ path: '/clientId',  order: 'ascending' }
			{ path: '/createdAt', order: 'descending' }
		  ]
		  [
			{ path: '/clientId', order: 'ascending' }
			{ path: '/status',   order: 'ascending' }
		  ]
		]
	  }
	}
  }
}

resource clientsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'clients'
  properties: {
	resource: {
	  id: 'clients'
	  partitionKey: {
		paths: [ '/clientId' ]
		kind: 'Hash'
	  }
	  indexingPolicy: {
		indexingMode: 'consistent'
		includedPaths: [ { path: '/*' } ]
		excludedPaths: [
		  // Never index the encrypted DOB blob — it is opaque binary data.
		  { path: '/encryptedDateOfBirth/*' }
		]
	  }
	}
  }
}

resource rateLimitsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'rate-limits'
  properties: {
	resource: {
	  id: 'rate-limits'
	  partitionKey: {
		paths: [ '/userId' ]
		kind: 'Hash'
	  }
	  defaultTtl: 60  // Automatically delete rate limit documents after 60 seconds (matches time window)
	  indexingPolicy: {
		indexingMode: 'consistent'
		includedPaths: [ { path: '/*' } ]
		excludedPaths: [
		  { path: '/Count/*' }
		  { path: '/WindowStart/*' }
		]
	  }
	}
  }
}

output accountName string = cosmosAccount.name
output endpoint string = cosmosAccount.properties.documentEndpoint
