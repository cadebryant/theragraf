using '../main.bicep'

param environmentName        = 'prod'
param location               = 'eastus'
param appResourceGroup       = 'theragraf-rg'
param cognitiveResourceGroup = 'Default-Web-EastUS'
param functionAppName        = 'theragraf-functions'
param openAiDeploymentName   = 'gpt-4o'
param openAiAccountName      = 'theragraf-oai'
param languageAccountName    = 'theragraf-language'
param storageAccountName     = 'theragrafstorage'
param tenantId               = '9525f140-7768-4f65-8ebb-54bd5151f7cb'
param apiClientId            = 'd84a7ccd-aaa1-4adf-8211-7c03fa3d319a'
param cosmosAccountName      = 'theragraf-cosmos'
param keyVaultName           = 'theragraf-kv-prod'
param spaClientId            = 'ba58ec08-f9c8-4232-8a01-8e90c5e4de2a'

param staticWebAppName        = 'theragraf-web-prod'
param staticWebAppLocation    = 'eastus2'

// Set to the Entra ID object ID of the admin who should be able to query the audit log.
// Run: az ad signed-in-user show --query id -o tsv
param adminPrincipalId        = '420e4c50-7bdc-4ad9-818d-a69fe25629d3'
