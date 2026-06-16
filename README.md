# TheraGraf

**TheraGraf** is an open-source, agentic clinical documentation engine designed to eliminate the "paperwork tax" for occupational therapists, physical therapists, and mental health practitioners.

Built with a privacy-first philosophy, TheraGraf uses a **Bring-Your-Own-Key (BYOK)** architecture to ensure your patient data never touches shared servers — you maintain full control of your clinical records by deploying entirely within your own Azure subscription.

---

## Why TheraGraf?

Modern clinical documentation is broken. Current solutions are high-cost, closed-source, and create data silos. TheraGraf changes the paradigm:

- **Privacy-First:** PII is redacted before any AI model sees it. The redaction map is encrypted and stored separately; original names are only restored on retrieval.
- **Cost-Efficient:** Pay only for the tokens you consume from your own Azure OpenAI resource. No separate subscription required.
- **Agentic Workflow:** Not just a scribe — an intelligent pipeline that captures diarized audio, redacts PII, generates SOAP notes, validates clinical compliance, suggests CPT billing codes with CMS 8-minute rule unit calculation, and suggests ICD-10 diagnostic codes.
- **Goal-Oriented:** Track SMART treatment goals per client with progress notes after every session. An AI suggestion endpoint generates goal candidates from the latest SOAP note, which the therapist can accept or discard.
- **Clinician-Centric:** Built for professionals who value precision, auditability, and data ownership.

---

## Architecture

### Backend pipeline

```
POST /api/documentation
        │
        ▼
DocumentationOrchestrator (Durable Functions)
        │
        ├── IngestionActivity      — PII redaction via Azure AI Language
        ├── SoapActivity           — SOAP note generation via Azure OpenAI
        ├── ComplianceActivity     — Clinical compliance validation via Azure OpenAI
        ├── FinalizerActivity      — PII restoration for in-flight result
        ├── BillingActivity        — CPT code suggestions + CMS 8-minute unit calculation
        ├── Icd10Activity          — ICD-10 code suggestions
        └── PersistActivity        — Saves redacted note + encrypted redaction map to Cosmos DB

AI agents also power a standalone **Goal Agent** (POST `/api/goals/{clientId}/suggest`) that generates SMART treatment-goal suggestions from an existing SOAP note outside of the pipeline.
```

### HTTP API surface

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/documentation` | Start a new documentation pipeline |
| `GET` | `/api/status/{instanceId}` | Poll orchestration status |
| `GET` | `/api/speech-token` | Exchange server-side Speech key for a short-lived browser token |
| `GET` | `/api/sessions` | Caseload overview for the authenticated therapist |
| `GET` | `/api/sessions/{clientId}` | Pageable session list (with filters and sorting) |
| `GET` | `/api/sessions/{clientId}/{sessionDate}` | Single session detail |
| `PATCH` | `/api/sessions/{clientId}/{sessionDate}` | Edit SOAP note or codes |
| `DELETE` | `/api/sessions/{clientId}/{sessionDate}` | Delete a session |
| `GET` | `/api/goals/{clientId}` | List treatment goals for a client |
| `POST` | `/api/goals/{clientId}` | Create a new treatment goal |
| `PATCH` | `/api/goals/{clientId}/{goalId}` | Update a goal (title, status, progress note) |
| `DELETE` | `/api/goals/{clientId}/{goalId}` | Delete a goal |
| `POST` | `/api/goals/{clientId}/suggest` | AI-generated SMART goal suggestions from a SOAP note |
| `GET` | `/api/stats/therapist/{therapistName}` | Therapist aggregate stats |
| `GET` | `/api/stats/client/{clientId}` | Client aggregate stats |

All routes enforce **JWT ownership checks** — therapists can only read and modify their own records.

### Frontend (React SPA)

```
Theragraf.Web/
  pages/
    Dashboard/         — Therapist stats, legend-labelled charts, searchable/sortable caseload table with overdue-note alerts
    NewSession/        — Diarized audio recording, metadata form, transcript submission
    SessionReview/     — Orchestration status polling, SOAP/CPT/ICD editing
    ClientProfile/     — Per-client stats, SMART goal tracking (with AI suggestions), and session history
    SessionDetail/     — Single session view and edit
```

The SPA authenticates via **MSAL** (Microsoft Authentication Library) and acquires an access token scoped to the Function App's Entra ID registration before every API call. It is hosted on **Azure Static Web Apps (Standard)** with the Function App linked as the API backend — no CORS configuration is required.

### Infrastructure

All Azure resources are defined as **Bicep IaC** under `infra/`. A single `az deployment sub create` command provisions everything.

```
infra/
  main.bicep                        — Subscription-level orchestrator
  modules/
    functionApp.bicep               — App Service Plan + Function App + app settings
    cosmos.bicep                    — Cosmos DB account, database, and container
    storage.bicep                   — Azure Storage (required by Durable Functions runtime)
    openai.bicep                    — Azure OpenAI account reference
    language.bicep                  — Azure AI Language account reference
    speech.bicep                    — Azure AI Speech resource
    keyVault.bicep                  — Key Vault for redaction map encryption key
    staticWebApp.bicep              — Azure Static Web Apps (Standard) with linked backend
    monitoring.bicep                — App Insights + Log Analytics workspace
    roleAssignments.bicep           — Managed Identity role assignments
    cognitiveRoleAssignments.bicep
    cosmosRoleAssignment.bicep
    keyVaultRoleAssignment.bicep
  parameters/
    dev.bicepparam
    prod.bicepparam
```

---

## Authentication

TheraGraf uses **two Entra ID app registrations**:

| Registration | Purpose |
|---|---|
| `theragraf-api` | Represents the Function App; defines the `access_as_user` API scope |
| `theragraf-spa` | Represents the React SPA; has delegated permission to call `access_as_user` |

The SPA acquires a token scoped to `api://<api-client-id>/access_as_user`. The Function App's `JwtAuthMiddleware` validates the token on every request and stores the `ClaimsPrincipal` in the function context. `ClaimsHelper` extracts `preferred_username` as the therapist identity for ownership enforcement.

**Current access model:** Only users in your Entra ID tenant can sign in. For a multi-therapist deployment, invite users to your tenant or switch to a multi-tenant app registration.

---

## Prerequisites

### Backend

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azure Cosmos DB Emulator](https://aka.ms/cosmosdb-emulator) for local session persistence
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for local Durable Functions storage

### Frontend

- [Node.js 20+](https://nodejs.org/)

### Azure (for deployment)

- Azure OpenAI resource with a chat model deployment (e.g. `gpt-4o-mini`)
- Azure AI Language resource (for PII detection)
- Azure AI Speech resource (for browser-side diarized transcription)
- Azure Cosmos DB account
- Azure Key Vault
- Azure Static Web Apps (Standard SKU)

---

## Local development setup

### 1. Clone the repo

```powershell
git clone https://github.com/cadebryant/theragraf.git
cd theragraf
```

### 2. Configure the Function App

```powershell
Copy-Item Theragraf.Functions\local.settings.template.json Theragraf.Functions\local.settings.json
```

Open `local.settings.json` and fill in your Azure endpoint URLs and API keys. For local-only development:

| Setting | Local value |
|---|---|
| `CosmosDb__ConnectionString` | Cosmos DB Emulator default connection string (pre-filled in template) |
| `Auth__Disabled` | `true` — bypasses JWT validation so API calls work without an Entra token |
| `KeyVault__VaultUri` | Leave blank — falls back to no-op redaction map encryption |
| `AzureSpeech__*` | Required only if you want to test audio capture locally |

### 3. Start the Cosmos DB Emulator

Launch the **Azure Cosmos DB Emulator** from the Start menu, or:

```powershell
& "$env:ProgramFiles\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"
```

The emulator auto-creates the `theragraf` database and both the `sessions` and `goals` containers on first use.

### 4. Start Azurite

Azurite is required by the Durable Functions runtime for its internal storage.

```powershell
azurite --silent
```

### 5. Run the Function App

```powershell
cd Theragraf.Functions
func start
```

Or press **F5** in Visual Studio.

### 6. Run the React SPA

```powershell
cd Theragraf.Web
npm install
npm run dev
```

The Vite dev server starts on `http://localhost:5173` and proxies `/api/*` requests to the Function App at `http://localhost:7071`.

> With `Auth__Disabled=true` on the backend, MSAL login is still triggered by the frontend. Either log in with your Azure account (the redirect URI `http://localhost:5173` is registered) or temporarily remove the `<ProtectedRoute>` wrapper in `App.tsx` for purely local testing.

### 7. Run the tests

```powershell
dotnet test
```

Integration tests that require the Cosmos DB Emulator are automatically skipped when the emulator is not running.

---

## Azure deployment

In Azure, the Function App uses **Managed Identity** for all service-to-service authentication — no API keys are stored in app settings.

### One-time infrastructure provisioning

```powershell
az deployment sub create --location eastus --template-file infra/main.bicep --parameters infra/parameters/dev.bicepparam
```

After provisioning, copy the `swaDeploymentToken` output value and add it as a GitHub secret:

```powershell
gh secret set SWA_DEPLOYMENT_TOKEN --body "<token-from-deployment-output>"
```

### GitHub Actions secrets required

| Secret | Description |
|---|---|
| `AZURE_CLIENT_ID` | Service principal client ID for CI deployments |
| `AZURE_CLIENT_SECRET` | Service principal secret |
| `AZURE_TENANT_ID` | Entra ID tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `SWA_DEPLOYMENT_TOKEN` | Azure Static Web Apps deployment token |

### Automated deployment (recommended)

Every push to `main` automatically:

1. Restores, builds, and tests the .NET solution
2. Publishes and deploys the Function App via `az functionapp deployment source config-zip`
3. Builds the Vite SPA and deploys it to Azure Static Web Apps

Trigger manually from **Actions → Build, Test & Deploy to Azure → Run workflow**.

### Register the SWA redirect URI

After the first deployment, add the SWA hostname to the SPA app registration so Entra will accept the login redirect:

```powershell
$swaHostname = az staticwebapp show --name <swa-name> --resource-group theragraf-rg --query "defaultHostname" -o tsv
az ad app update --id <spa-client-id> --set spa.redirectUris="[\"http://localhost:5173\",\"https://$swaHostname\"]"
```

---

## Project structure

```
Theragraf.Core/             — Shared models, interfaces, and domain logic
  Models/                   — CptCode, IcdCode, SoapNote, SessionResponse, TranscriptInput, stats records, etc.
  Services/                 — IPiiRedactionService, ISessionRepository, ICmsUnitCalculator, etc.

Theragraf.Functions/        — Azure Functions host (isolated worker, .NET 10)
  Activities/               — Durable activity functions
  Agents/                   — Semantic Kernel agents (SOAP, Compliance, Billing, ICD-10)
  EntryPoint/               — HTTP triggers
  Helpers/                  — ClaimsHelper (JWT identity extraction)
  Middleware/               — JwtAuthMiddleware (Entra ID token validation)
  Orchestration/            — DocumentationOrchestrator
  Plugins/                  — Semantic Kernel prompt templates
  Services/                 — PiiRedactionService, CosmosSessionRepository

Theragraf.Web/              — React + TypeScript + Vite SPA
  src/
    api/                    — Typed fetch wrappers (sessions, stats, speech token, goals)
    auth/                   — MSAL configuration and singleton instance
    components/             — AppLayout, ProtectedRoute, GettingStartedModal
    pages/                  — Dashboard, NewSession, SessionReview, ClientProfile (with GoalsPanel), SessionDetail
    types.ts                — TypeScript mirrors of all backend models

Theragraf.Tests/            — xUnit unit test suite (endpoints, helpers, agents, orchestration)
Theragraf.IntegrationTests/ — xUnit integration tests against Cosmos DB Emulator (sessions + goals)

infra/                      — Bicep IaC for all Azure resources
  main.bicep
  modules/
  parameters/

postman/                    — Postman collection for manual API testing
```

---

## Security notes

- `local.settings.json` is excluded from git via `.gitignore` — **never commit it**
- Use `local.settings.template.json` as the shareable reference for required config values
- In Azure, all service-to-service authentication uses Managed Identity — no API keys are stored in app settings
- PII is redacted before any AI model processes the transcript; the redaction map is encrypted with a Key Vault-managed key and stored alongside the session record
- The React SPA contains only public, non-sensitive Entra configuration values (`tenantId`, `clientId`, `scope`, `speechRegion`) — no secrets are embedded in the frontend bundle
- All HTTP endpoints enforce JWT ownership — therapists cannot read or modify another therapist's sessions or goals
- Client IDs are transparently namespaced server-side using a hash of the therapist's email address; the raw client-visible name is stripped from API responses and never stored without the prefix
