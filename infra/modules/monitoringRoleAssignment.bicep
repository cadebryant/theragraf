/*
  monitoringRoleAssignment.bicep — Grants Log Analytics Reader to an optional
  admin principal so that query access to the audit log is controlled via Entra
  RBAC rather than being open to anyone with network access.

  Log Analytics Reader allows querying workspace data (including audit traces)
  but does NOT allow changing workspace settings or viewing billing details.

  Admin (developer/operator) → Log Analytics Reader  (query audit log, read dashboards)

  Intentionally NOT granting Log Analytics Contributor (which allows workspace
  config changes) or Monitoring Contributor (which allows creating alerts).
*/

param logAnalyticsWorkspaceName string

@description('Entra ID object ID of the admin principal to grant Log Analytics Reader access.')
param adminPrincipalId string

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

// Built-in role: Log Analytics Reader
// https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/monitor#log-analytics-reader
var logAnalyticsReaderRoleId = '73c42c96-874c-492b-b04d-ab87d138a893'

resource adminRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(logAnalytics.id, adminPrincipalId, logAnalyticsReaderRoleId)
  scope: logAnalytics
  properties: {
	roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', logAnalyticsReaderRoleId)
	principalId: adminPrincipalId
	principalType: 'User'
	description: 'Theragraf audit log query access — managed by Bicep'
  }
}
