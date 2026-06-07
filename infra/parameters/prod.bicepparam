using '../main.bicep'

param environmentName = 'prod'
param location = 'eastus'
param functionAppName = 'theragraf-functions'
param openAiDeploymentName = 'gpt-4o-mini'
param openAiCapacity = 30
param storageAccountName = '' // TODO: set to your existing storage account name, e.g. 'theragrafabc123'
