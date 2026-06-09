/*
  keyVaultRoleAssignment.bicep — Grants Key Vault Secrets User to the Function App
  Managed Identity so it can read the redaction-map encryption key at runtime.

  Built-in role: Key Vault Secrets User
  GUID: 4633458b-17de-408a-b874-0445c86b69e4
*/

param keyVaultName string
param functionAppPrincipalId string

@description('Optional Entra ID object ID of a developer to grant Key Vault Secrets User for local debugging via VS/az login.')
param developerPrincipalId string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

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
  name: guid(keyVault.id, developerPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
	roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
	principalId: developerPrincipalId
	principalType: 'User'
  }
}
