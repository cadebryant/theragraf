/*
  keyVaultRoleAssignment.bicep — Grants Key Vault roles to the Function App MSI and an optional developer.

  Function App MSI  → Key Vault Secrets User    (read secrets at runtime)
  Developer         → Key Vault Secrets Officer  (read + write secrets for key rotation/setup)
*/

param keyVaultName string
param functionAppPrincipalId string

@description('Optional Entra ID object ID of a developer to grant Key Vault Secrets Officer for secret management.')
param developerPrincipalId string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

var keyVaultSecretsUserRoleId    = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource functionAppRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionAppPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
	roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
	principalId: functionAppPrincipalId
	principalType: 'ServicePrincipal'
  }
}

resource developerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(developerPrincipalId)) {
  name: guid(keyVault.id, developerPrincipalId, keyVaultSecretsOfficerRoleId)
  scope: keyVault
  properties: {
	roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRoleId)
	principalId: developerPrincipalId
	principalType: 'User'
  }
}
