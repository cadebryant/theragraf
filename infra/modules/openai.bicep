/*
  openai.bicep — Read existing Azure OpenAI account (lives in Default-Web-EastUS).
  This module never creates or modifies the resource; it only reads its name
  and endpoint so they can be passed to the Function App and role assignments.
*/

param accountName string

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: accountName
}

output accountName string = openAiAccount.name
output endpoint string = openAiAccount.properties.endpoint
