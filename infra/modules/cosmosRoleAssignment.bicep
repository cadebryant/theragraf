/*
  cosmosRoleAssignment.bicep — Cosmos DB data-plane RBAC for the Function App Managed Identity
							   and optionally a developer identity for Portal Data Explorer access.
  Uses the built-in "Cosmos DB Built-in Data Contributor" role (id: 00000000-0000-0000-0000-000000000002).
  This grants read/write access to items and queries — no keys required.
*/

param cosmosAccountName string
param functionAppPrincipalId string

@description('Optional Entra ID object ID of a developer to grant Data Explorer access. Leave empty to skip.')
param developerPrincipalId string = ''

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

// Cosmos DB Built-in Data Contributor
var cosmosDataContributorRoleId = '00000000-0000-0000-0000-000000000002'

resource functionAppRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, functionAppPrincipalId, cosmosDataContributorRoleId)
  properties: {
	roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
	principalId: functionAppPrincipalId
	scope: cosmosAccount.id
  }
}

resource developerRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = if (!empty(developerPrincipalId)) {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, developerPrincipalId, cosmosDataContributorRoleId)
  properties: {
	roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
	principalId: developerPrincipalId
	scope: cosmosAccount.id
  }
}
