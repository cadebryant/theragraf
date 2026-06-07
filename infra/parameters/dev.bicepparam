using '../main.bicep'

param environmentName = 'dev'
param location = 'eastus'
param functionAppName = 'theragraf-functions'
param openAiDeploymentName = 'gpt-4o-mini'
param openAiCapacity = 10
param storageAccountName = '' // TODO: set to your existing storage account name, e.g. 'theragrafabc123'
