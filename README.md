# TheraGraf

**TheraGraf** is an open-source, agentic clinical documentation engine designed to eliminate the "paperwork tax" for occupational therapists, physical therapists, and mental health practitioners.

Built with a privacy-first philosophy, TheraGraf uses a **Bring-Your-Own-Key (BYOK)** architecture to ensure your patient data never touches our servers — you maintain full control of your clinical records.

---

## Why TheraGraf?

Modern clinical documentation is broken. Current solutions are high-cost, closed-source, and create data silos. TheraGraf changes the paradigm:

- **Privacy-First:** PII is redacted before any AI model sees it, and the redaction map is stored separately so original names are only restored on retrieval.
- **Cost-Efficient:** Pay only for the tokens you consume from your own Azure OpenAI resource. No separate subscription required.
- **Agentic Workflow:** Not just a scribe — an intelligent pipeline that redacts PII, generates SOAP notes, validates compliance, suggests CPT billing codes with CMS 8-minute rule unit calculation, and suggests ICD-10 diagnostic codes.
- **Clinician-Centric:** Built for professionals who value precision, auditability, and data ownership.

---

## Architecture

```
POST /api/DocumentationStart
        │
        ▼
DocumentationOrchestrator (Durable Functions)
        │
        ├── IngestionActivity      — PII redaction via Azure AI Language
        ├── SoapActivity           — SOAP note generation via Azure OpenAI
        ├── ComplianceActivity     — Clinical compliance validation via Azure OpenAI
        ├── FinalizerActivity      — PII restoration
        ├── BillingActivity        — CPT code suggestions + CMS 8-minute unit calculation
        ├── Icd10Activity          — ICD-10 code suggestions
        └── PersistActivity        — Save redacted note + redaction map to Azure Table Storage

GET /api/sessions/{clientId}
GET /api/sessions/{clientId}/{sessionDate}
        │
        ▼
TableStorageSessionRepository  — Reads record and restores PII on the fly
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for local Table Storage emulation
- An Azure subscription with:
  - Azure OpenAI resource with a chat model deployment (e.g. `gpt-4o-mini`)
  - Azure AI Language resource (for PII detection)
  - Azure Storage account (for Table Storage persistence)

---

## Local development setup

1. **Clone the repo**
   ```powershell
   git clone https://github.com/cadebryant/theragraf.git
   cd theragraf
   ```

2. **Create your local settings file**
   ```powershell
   Copy-Item Theragraf.Functions\local.settings.template.json Theragraf.Functions\local.settings.json
   ```
   Open `local.settings.json` and fill in your Azure endpoint URLs and API keys.

3. **Start Azurite** (local Table Storage emulator)
   ```powershell
   azurite --silent
   ```

4. **Run the Function App**
   ```powershell
   cd Theragraf.Functions
   func start
   ```
   Or press **F5** in Visual Studio.

5. **Run the tests**
   ```powershell
   dotnet test
   ```

---

## Azure deployment

The app uses **Managed Identity** for all service-to-service authentication in Azure — no API keys are needed in the deployed environment.

### One-time Azure setup

```powershell
$REGION  = "eastus"
$RG      = "theragraf-rg"
$STORAGE = "theragrafstorage"
$FUNCAPP = "theragraf-functions"

# Create resources
az group create --name $RG --location $REGION
az storage account create --name $STORAGE --resource-group $RG --location $REGION --sku Standard_LRS --kind StorageV2
az functionapp create --name $FUNCAPP --resource-group $RG --storage-account $STORAGE `
  --consumption-plan-location $REGION --runtime dotnet-isolated --runtime-version 10 `
  --functions-version 4 --os-type Windows

# Enable Managed Identity and grant permissions
az functionapp identity assign --resource-group $RG --name $FUNCAPP
$PRINCIPAL_ID = az functionapp identity show --resource-group $RG --name $FUNCAPP --query principalId --output tsv

az role assignment create --assignee $PRINCIPAL_ID --role "Storage Table Data Contributor" `
  --scope $(az storage account show --name $STORAGE --resource-group $RG --query id --output tsv)

az role assignment create --assignee $PRINCIPAL_ID --role "Cognitive Services OpenAI User" `
  --scope $(az cognitiveservices account show --name <your-openai-resource> --resource-group <rg> --query id --output tsv)

az role assignment create --assignee $PRINCIPAL_ID --role "Cognitive Services User" `
  --scope $(az cognitiveservices account show --name <your-language-resource> --resource-group <rg> --query id --output tsv)

# Push app settings (no secrets — Managed Identity handles auth)
az functionapp config appsettings set --resource-group $RG --name $FUNCAPP --settings `
  "AzureStorage__AccountName=$STORAGE" `
  "AzureOpenAI__Endpoint=https://<your-openai-resource>.openai.azure.com/" `
  "AzureOpenAI__DeploymentName=gpt-4o-mini" `
  "AzureLanguage__Endpoint=https://<your-language-resource>.cognitiveservices.azure.com/"
```

### Deploy

#### Automated (GitHub Actions — recommended)

Every push to `main` automatically builds, tests, and deploys to Azure via the workflow in `.github/workflows/deploy.yml`.

**One-time setup — add the publish profile as a GitHub secret:**

1. In the Azure Portal, open **theragraf-functions → Overview → Get publish profile** and copy the XML content.
2. In GitHub, go to **Settings → Secrets and variables → Actions → New repository secret**.
3. Name it `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` and paste the XML as the value.

After this, every push to `main` will trigger an automated deployment. You can also trigger it manually from **Actions → Build, Test & Deploy to Azure → Run workflow**.

#### Manual (fallback)

```powershell
cd Theragraf.Functions
func azure functionapp publish theragraf-functions
```

---

## Security notes

- `local.settings.json` is excluded from git via `.gitignore` — **never commit it**
- Use `local.settings.template.json` as the shareable reference for required config values
- In Azure, all service-to-service authentication uses Managed Identity — no API keys are stored in app settings
- PII is redacted before the SOAP note is stored; the redaction map is stored separately and used to restore PII on retrieval
- If you have previously committed `local.settings.json`, rotate any exposed keys in the Azure portal immediately

---

## Project structure

```
Theragraf.Core/          — Shared models, interfaces, and services
  Models/                — CptCode, IcdCode, SoapNote, TranscriptInput, SessionRecord, etc.
  Services/              — IPiiRedactionService, ISessionRepository, ICmsUnitCalculator, etc.

Theragraf.Functions/     — Azure Functions host
  Activities/            — Durable activity functions
  Agents/                — Semantic Kernel agents (SOAP, Compliance, Billing, ICD-10)
  EntryPoint/            — HTTP triggers (DocumentationStart, SessionsGet)
  Orchestration/         — DocumentationOrchestrator
  Plugins/               — Semantic Kernel prompt templates
  Services/              — PiiRedactionService, TableStorageSessionRepository

Theragraf.Tests/         — xUnit test suite (105 tests)
```
