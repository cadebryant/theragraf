/*
  keyVaultSecrets.bicep — Stores runtime secrets in Key Vault during deployment.

  Secrets managed here:
	speech-api-key   — Azure Speech Service key1, referenced by the Function App
					   via a Key Vault reference app setting so it never appears
					   in plaintext in the portal or deployment outputs.

  The Function App reads this secret at runtime via the app setting:
	AzureSpeech__ApiKey = @Microsoft.KeyVault(SecretUri=<vaultUri>secrets/speech-api-key/)

  The Function App MSI already has Key Vault Secrets User (granted in
  keyVaultRoleAssignment.bicep), so no extra role assignment is needed here.
*/

param keyVaultName string

@description('Azure Speech Service key1 — stored as a Key Vault secret.')
@secure()
param speechApiKey string

@description('Secret key required in the X-Migration-Key header to invoke the partition-key migration endpoint. Leave blank to keep the endpoint disabled.')
@secure()
param migrationKey string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource speechApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'speech-api-key'
  properties: {
	value: speechApiKey
	attributes: {
	  enabled: true
	}
  }
}

resource migrationKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(migrationKey)) {
  parent: keyVault
  name: 'migration-key'
  properties: {
	value: migrationKey
	attributes: {
	  enabled: true
	}
  }
}
