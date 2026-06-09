/*
  keyVault.bicep — Azure Key Vault for Theragraf secrets
  Stores the AES-256 redaction-map encryption key (secret name: redaction-map-key).

  Access model:
	- The Function App's system-assigned Managed Identity is granted Key Vault Secrets User
	  via keyVaultRoleAssignment.bicep — no access policies, pure RBAC.
	- Soft-delete and purge protection are enabled to comply with HIPAA retention requirements.
*/

param location string
param suffix string

@description('Name for the Key Vault. Must be 3-24 chars, globally unique.')
param keyVaultName string = 'theragraf-kv-${suffix}'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
	sku: {
	  family: 'A'
	  name: 'standard'
	}
	tenantId: subscription().tenantId
	enableRbacAuthorization: true   // pure Azure RBAC — no legacy access policies
	enableSoftDelete: true
	softDeleteRetentionInDays: 90
	enablePurgeProtection: true
  }
}

output keyVaultName string = keyVault.name
output vaultUri string = keyVault.properties.vaultUri
