# Theragraf Postman Collection

## Setup

1. Import `theragraf.postman_collection.json` into Postman
2. Set the following collection variables:

| Variable | Description | Example |
|---|---|---|
| `baseUrl` | Local Functions host | `http://localhost:7071` |
| `aoaiEndpoint` | Azure OpenAI base URL | `https://your-resource.services.ai.azure.com` |
| `aoaiDeployment` | Deployment name | `gpt-4o-mini` |
| `aoaiApiVersion` | API version | `2024-12-01-preview` |
| `aoaiApiKey` | Azure OpenAI API key | from Azure portal |
| `durableCode` | Durable webhook code | from step 2 response |

> ⚠️ Never commit real API keys. Use Postman environments to store secrets locally.

## Running the Collection

1. Start the local Functions host (F5 in Visual Studio)
2. Run **Step 1** — generates a random synthetic OT transcript via Azure OpenAI
3. Run **Step 2** — submits the transcript to the pipeline; saves `instanceId`
4. Copy the `code=` value from the `statusQueryGetUri` in the step 2 response into the `durableCode` variable
5. Wait ~5-10 seconds, then run **Step 3** — polls for completion and validates the SOAP note

## Notes

- Step 1 randomly selects from 4 OT scenarios: stroke ADL retraining, pediatric fine motor, TBI cognitive rehab, hip replacement recovery
- All patient names in generated transcripts are fictional
- The `durableCode` stays the same while the Functions host is running; reset it after each F5
