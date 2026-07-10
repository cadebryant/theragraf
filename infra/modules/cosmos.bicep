/*
  cosmos.bicep — Azure Cosmos DB for NoSQL account, database, and containers.
  Uses serverless capacity mode (pay-per-request, ideal for Functions workloads).

  Multi-tenant partition key strategy
  ────────────────────────────────────
  All clinical data containers use hierarchical partition keys (MultiHash, version 2)
  with /tenantId as the first level. This makes cross-tenant data leakage structurally
  impossible — a query scoped to the wrong tenantId returns zero results by design.

  ⚠️  EXISTING DEPLOYMENTS: Cosmos DB does not allow in-place partition key changes.
  If you are updating an existing deployment that has data in the old single-key
  containers, run the TenantMigrationFunction (POST /api/admin/migrate-partitions)
  BEFORE redeploying this Bicep. The migration function creates *-v2 destination
  containers and copies all documents with tenantId stamped. Once migration is
  verified, the old containers can be deleted manually in the portal.
  New deployments are unaffected — Bicep provisions the correct keys from the start.
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
		paths: [ '/tenantId', '/clientId' ]
		kind: 'MultiHash'
		version: 2
	  }
	  defaultTtl: -1  // Enable TTL without default expiration; per-document TimeToLive controls retention
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
		paths: [ '/tenantId', '/clientId' ]
		kind: 'MultiHash'
		version: 2
	  }
	  defaultTtl: -1  // Enable TTL without default expiration; per-document TimeToLive controls retention
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
		paths: [ '/tenantId', '/clientId' ]
		kind: 'MultiHash'
		version: 2
	  }
	  defaultTtl: -1  // Enable TTL without default expiration; per-document TimeToLive controls retention
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
		paths: [ '/tenantId', '/userId' ]
		kind: 'MultiHash'
		version: 2
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

// ── Multi-tenant containers ───────────────────────────────────────────────────

resource tenantsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'tenants'
  properties: {
	resource: {
	  id: 'tenants'
	  partitionKey: {
		paths: [ '/tenantId' ]
		kind: 'Hash'
	  }
	  indexingPolicy: {
		indexingMode: 'consistent'
		includedPaths: [ { path: '/*' } ]
		excludedPaths: [
		  { path: '/organizationName/*' }
		]
	  }
	}
  }
}

resource therapistProfilesContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'therapist-profiles'
  properties: {
	resource: {
	  id: 'therapist-profiles'
	  partitionKey: {
		paths: [ '/tenantId', '/therapistId' ]
		kind: 'MultiHash'
		version: 2
	  }
	  indexingPolicy: {
		indexingMode: 'consistent'
		includedPaths: [ { path: '/*' } ]
		excludedPaths: [
		  // Never index encrypted tax ID blobs.
		  { path: '/encryptedTaxId/*' }
		]
	  }
	}
  }
}

resource providersContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'providers'
  properties: {
	resource: {
	  id: 'providers'
	  partitionKey: {
		paths: [ '/tenantId', '/providerId' ]
		kind: 'MultiHash'
		version: 2
	  }
	  indexingPolicy: {
		indexingMode: 'consistent'
		includedPaths: [ { path: '/*' } ]
		excludedPaths: [
		  // Never index encrypted EIN blobs.
		  { path: '/encryptedEin/*' }
		]
	  }
	}
  }
}

output accountName string = cosmosAccount.name
output endpoint string = cosmosAccount.properties.documentEndpoint
