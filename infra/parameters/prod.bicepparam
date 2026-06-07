using '../main.bicep'

param environmentName = 'prod'
param location = 'eastus'
param functionAppName = 'theragraf-functions'
param openAiDeploymentName = 'gpt-4o-mini'
param openAiCapacity = 30
param storageAccountName = 'theragrafstorage'
