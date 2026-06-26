# TheraGraf

**TheraGraf** is an open-source, agentic clinical documentation engine designed to eliminate the "paperwork tax" for occupational therapists, physical therapists, and mental health practitioners.

Built with a privacy-first philosophy, TheraGraf is architected to deploy entirely within your own Azure subscription using your own AI resources � ensuring patient data never touches shared servers and you maintain full control of your clinical records.

> **?? Important Note on Deployment:** The infrastructure-as-code files in this repository (`infra/parameters/*.bicepparam`) contain references to the author's specific Azure resources (OpenAI account, Language service, tenant ID, etc.). These are provided as a working reference implementation. **To deploy TheraGraf to your own Azure subscription, you must customize these parameter files** to point to your own pre-created Azure resources. See the [Azure Deployment](#azure-deployment) section for details.

---

## Why TheraGraf?

Modern clinical documentation is broken. Current solutions are high-cost, closed-source, and create data silos. TheraGraf changes the paradigm:

- **Privacy-First:** PII is redacted before any AI model sees it. The redaction map is encrypted and stored separately; original names are only restored on retrieval.
- **Bring-Your-Own-Resources:** TheraGraf deploys into your Azure subscription and uses your Azure OpenAI account, Language service, and Speech service. Pay only for the tokens and API calls you consume � no separate subscription or per-user licensing fees.
- **Agentic Workflow:** Not just a scribe � an intelligent pipeline that captures diarized audio, redacts PII, generates **SOAP or DAP notes** (auto-selected by discipline, manually overrideable), validates clinical compliance, suggests CPT billing codes with CMS 8-minute rule unit calculation, and suggests ICD-10 diagnostic codes.
- **Therapist-in-the-Loop:** AI-generated documentation is clearly labeled as a draft. Sessions require explicit therapist attestation and approval before export. Approval is automatically cleared if clinical content is edited, ensuring accountability.
- **Goal-Oriented:** Track SMART treatment goals per client with progress notes after every session. An AI suggestion endpoint generates goal candidates from the latest SOAP note, which the therapist can accept or discard.
- **Client Profiles:** Maintain demographic and intake data per client � age range, biological sex, prior diagnoses, and functional limitations. This context informs smarter ICD-10 suggestions without forwarding any PII to the AI pipeline.
- **Clinician-Centric:** Built for professionals who value precision, auditability, and data ownership.

---

## Architecture

### Backend pipeline

```
POST /api/documentation
        �
        ?
DocumentationOrchestrator (Durable Functions)
        �
        +-- IngestionActivity      � PII redaction via Azure AI Language
        +-- SoapActivity           � SOAP or DAP note generation via Azure OpenAI (branches on selected note format)
        +-- ComplianceActivity     � Clinical compliance validation via Azure OpenAI (validates correct fields per format)
        +-- FinalizerActivity      � PII restoration for in-flight result
        +-- BillingActivity        � CPT code suggestions + CMS 8-minute unit calculation
        +-- Icd10Activity          � ICD-10 code suggestions (uses client demographics context when available)
        +-- PersistActivity        � Saves redacted note + encrypted redaction map to Cosmos DB

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
| `PATCH` | `/api/sessions/{clientId}/{sessionDate}` | Edit SOAP note, codes, or approve session (with `approval: { verifyAndApprove: true }`) |
| `DELETE` | `/api/sessions/{clientId}/{sessionDate}` | Delete a session |
| `GET` | `/api/goals/{clientId}` | List treatment goals for a client |
| `POST` | `/api/goals/{clientId}` | Create a new treatment goal |
| `PATCH` | `/api/goals/{clientId}/{goalId}` | Update a goal (title, status, progress note) |
| `DELETE` | `/api/goals/{clientId}/{goalId}` | Delete a goal |
| `POST` | `/api/goals/{clientId}/suggest` | AI-generated SMART goal suggestions from a SOAP note |
| `GET` | `/api/clients/{clientId}/demographics` | Retrieve client intake record |
| `PUT` | `/api/clients/{clientId}/demographics` | Create or update client intake record |
| `GET` | `/api/stats/therapist/{therapistName}` | Therapist aggregate stats |
| `GET` | `/api/stats/client/{clientId}` | Client aggregate stats |

All routes enforce **JWT ownership checks** � therapists can only read and modify their own records.

### Frontend (React SPA)

```
Theragraf.Web/
  pages/
    Dashboard/         – Therapist stats, legend-labeled charts, searchable/sortable caseload table with overdue-note alerts
    NewSession/        – Diarized audio recording with speaker diarization, explicit role assignment (Therapist/Client), metadata form (with SOAP/DAP note format selector), transcript submission
    SessionReview/     – Orchestration status polling, SOAP/DAP note editing with format-appropriate field labels, CPT/ICD editing, AI draft banner, attestation workflow, Verify & Approve button
    ClientProfile/     – Per-client stats, demographics/intake panel, SMART goal tracking (with AI suggestions), and session history
    SessionDetail/     – Single session view and edit, approval status badge, conditional export access
    Settings/          – User preferences (display, documentation defaults, notifications, accessibility, privacy), retention policy remains admin-only configuration
```

**Onboarding:** New users are greeted with a Getting Started modal explaining the privacy-first philosophy and documentation workflow. After dismissal, an interactive product tour (built with react-joyride) guides users through the four key workflow areas: New Session, recording controls, Dashboard, and Settings. The tour can be restarted at any time from the Settings page.

The SPA authenticates via **MSAL** (Microsoft Authentication Library) and acquires an access token scoped to the Function App's Entra ID registration before every API call. It is hosted on **Azure Static Web Apps (Standard)** with the Function App linked as the API backend – no CORS configuration is required.

**Accessibility:** TheraGraf meets WCAG 2.1 Level AA standards with:
- Full keyboard navigation (Tab, Enter, Escape, arrow keys)
- Skip navigation link for efficient keyboard access
- Screen reader support with ARIA landmarks and live regions
- Semantic HTML with proper heading hierarchy (h1, h2, h3)
- Focus management on route changes
- Accessible data tables with sortable column headers
- High contrast color schemes and sufficient text contrast ratios
- Dynamic content announcements (loading states, errors, status updates)

See [docs/accessibility-testing-results.md](docs/accessibility-testing-results.md) for complete WCAG compliance details.

### Infrastructure

All Azure resources are defined as **Bicep IaC** under `infra/`. A single `az deployment sub create` command provisions everything.

```
infra/
  main.bicep                        � Subscription-level orchestrator
  modules/
    functionApp.bicep               � App Service Plan + Function App + app settings
    cosmos.bicep                    � Cosmos DB account, database, and container
    storage.bicep                   � Azure Storage (required by Durable Functions runtime)
    openai.bicep                    � Azure OpenAI account reference
    language.bicep                  � Azure AI Language account reference
    speech.bicep                    � Azure AI Speech resource
    keyVault.bicep                  � Key Vault for redaction map encryption key
    staticWebApp.bicep              � Azure Static Web Apps (Standard) with linked backend
    monitoring.bicep                � App Insights + Log Analytics workspace
    roleAssignments.bicep           � Managed Identity role assignments
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
| `Auth__Disabled` | `true` � bypasses JWT validation so API calls work without an Entra token |
| `KeyVault__VaultUri` | Leave blank � falls back to no-op redaction map encryption |
| `AzureSpeech__*` | Required only if you want to test audio capture locally |

### 3. Start the Cosmos DB Emulator

Launch the **Azure Cosmos DB Emulator** from the Start menu, or:

```powershell
& "$env:ProgramFiles\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"
```

The emulator auto-creates the `theragraf` database and the `sessions`, `goals`, and `clients` containers on first use.

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

TheraGraf is designed to deploy entirely within your own Azure subscription, giving you full control over your data and AI resources. **The Bicep templates reference existing Azure resources** � they do not create new Cognitive Services accounts for you.

### Prerequisites for deployment

Before deploying, you must create the following Azure resources in your subscription:

1. **Azure OpenAI account** with a chat model deployment (e.g., `gpt-4o-mini` or `gpt-4o`)
2. **Azure AI Language account** (for PII detection/redaction)
3. **Azure AI Speech resource** (for browser-side audio transcription)
4. **Two Entra ID app registrations:**
   - **API registration:** Defines the `access_as_user` scope (represents the Function App)
   - **SPA registration:** Has delegated permission to call the API scope (represents the React frontend)

### Customize the infrastructure parameters

The parameter files in `infra/parameters/` currently reference the author's Azure resources. You must edit these files to match your environment:

**Edit `infra/parameters/dev.bicepparam` (or `prod.bicepparam`):**

```bicep
param openAiAccountName      = 'your-openai-account-name'      // Your Azure OpenAI resource name
param openAiDeploymentName   = 'your-deployment-name'          // Your model deployment name (e.g., gpt-4o)
param languageAccountName    = 'your-language-service-name'    // Your Azure AI Language resource name
param cognitiveResourceGroup = 'your-cognitive-rg'             // Resource group containing your Cognitive Services
param tenantId               = 'your-entra-tenant-id'          // Your Entra ID tenant GUID
param apiClientId            = 'your-api-app-registration-id'  // Client ID of your API app registration
param spaClientId            = 'your-spa-app-registration-id'  // Client ID of your SPA app registration
param storageAccountName     = 'yourstorageaccount'            // Unique storage account name (3-24 chars, lowercase)
param cosmosAccountName      = 'your-cosmos-account'           // Unique Cosmos DB account name
param keyVaultName           = 'your-keyvault-name'            // Unique Key Vault name
param staticWebAppName       = 'your-swa-name'                 // Unique Static Web App name
// ... (see file for full list of parameters)
```

### One-time infrastructure provisioning

In Azure, the Function App uses **Managed Identity** for all service-to-service authentication � no API keys are stored in app settings.

After customizing the parameters, deploy:

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

Trigger manually from **Actions ? Build, Test & Deploy to Azure ? Run workflow**.

### Register the SWA redirect URI

After the first deployment, add the SWA hostname to the SPA app registration so Entra will accept the login redirect:

```powershell
$swaHostname = az staticwebapp show --name <swa-name> --resource-group theragraf-rg --query "defaultHostname" -o tsv
az ad app update --id <spa-client-id> --set spa.redirectUris="[\"http://localhost:5173\",\"https://$swaHostname\"]"
```

---

## Project structure

```
Theragraf.Core/             � Shared models, interfaces, and domain logic
  Models/                   � CptCode, IcdCode, SoapNote, NoteFormat, SessionResponse, TranscriptInput, GoalModels, ClientModels, stats records, etc.
  Services/                 � IPiiRedactionService, ISessionRepository, IGoalRepository, IClientRepository, ICmsUnitCalculator, etc.

Theragraf.Functions/        � Azure Functions host (isolated worker, .NET 10)
  Activities/               � Durable activity functions
  Agents/                   � Semantic Kernel agents (SOAP, Compliance, Billing, ICD-10)
  EntryPoint/               � HTTP triggers
  Helpers/                  � ClaimsHelper (JWT identity extraction)
  Middleware/               � JwtAuthMiddleware (Entra ID token validation)
  Orchestration/            � DocumentationOrchestrator
  Plugins/                  � Semantic Kernel prompt templates
  Services/                 � PiiRedactionService, CosmosSessionRepository, CosmosGoalRepository, CosmosClientRepository

Theragraf.Web/              � React + TypeScript + Vite SPA
  src/
    api/                    � Typed fetch wrappers (sessions, stats, speech token, goals, clients/demographics)
    auth/                   � MSAL configuration and singleton instance
    components/             � AppLayout, ProtectedRoute, GettingStartedModal
    hooks/                  � useSettings (localStorage-based user preferences with cross-tab sync)
    pages/                  � Dashboard, NewSession, SessionReview, ClientProfile (with GoalsPanel), SessionDetail, Settings
    types.ts                � TypeScript mirrors of all backend models plus user settings types

Theragraf.Tests/            � xUnit unit test suite (endpoints, helpers, agents, orchestration)
Theragraf.IntegrationTests/ � xUnit integration tests against Cosmos DB Emulator (sessions, goals, client demographics)
Theragraf.Web/tests/e2e/    � Playwright E2E tests covering full-stack user workflows

infra/                      � Bicep IaC for all Azure resources
  main.bicep
  modules/
  parameters/

postman/                    � Postman collection for manual API testing
```

---

## Testing

TheraGraf includes a comprehensive test suite at three levels:

### Unit Tests (.NET)

Located in `Theragraf.Tests/`, xUnit tests cover:
- HTTP endpoint logic
- Orchestration workflows
- AI agent prompts and parsing
- Helper utilities
- Domain models

Run unit tests:
```powershell
dotnet test Theragraf.Tests
```

### Integration Tests (.NET)

Located in `Theragraf.IntegrationTests/`, tests against Cosmos DB Emulator:
- Session repository CRUD operations
- Goal repository operations
- Client demographics repository
- Query builders and filtering
- Soft-delete and restore workflows

**Prerequisites:** Cosmos DB Emulator must be running (auto-starts via MSBuild target)

Run integration tests:
```powershell
dotnet test Theragraf.IntegrationTests
```

### End-to-End Tests (Playwright)

Located in `Theragraf.Web/tests/e2e/`, Playwright tests cover full-stack user workflows:
- **Authentication:** Real Azure AD authentication flow
- **Session Creation:** Complete workflow from recording to AI processing to approval
- **Dashboard:** Statistics, charts, caseload table, search, navigation
- **Client Profiles:** Demographics, goals CRUD, AI goal suggestions, session history
- **Session Review:** SOAP/DAP note editing, CPT/ICD code management, approval workflow

**Prerequisites:**
1. All services running (Frontend, Backend, Cosmos DB)
2. Test user credentials configured in `.env.test`
3. Playwright browsers installed

Run E2E tests:
```powershell
cd Theragraf.Web
npm install
npx playwright install
npm run test:e2e
```

For interactive testing and debugging:
```powershell
npm run test:e2e:ui        # Run tests in UI mode
npm run test:e2e:debug     # Run tests with debugger
npm run test:e2e:report    # View test results report
```

**Documentation:** See [Theragraf.Web/tests/e2e/README.md](Theragraf.Web/tests/e2e/README.md) for complete E2E testing guide including:
- Environment setup
- Configuration options
- Writing new tests
- Troubleshooting
- CI/CD integration

**Test Coverage:**
- 3 test projects (unit, integration, E2E)
- Full-stack workflow validation
- Multi-browser testing (Chromium, Firefox, WebKit)
- Real authentication with Azure AD
- Automated test data cleanup

---

## Security notes

- `local.settings.json` is excluded from git via `.gitignore` � **never commit it**
- Use `local.settings.template.json` as the shareable reference for required config values
- In Azure, all service-to-service authentication uses Managed Identity � no API keys are stored in app settings
- **PII Protection:** PII is redacted before any AI model processes the transcript; the redaction map is encrypted with a Key Vault-managed key and stored alongside the session record
- **Prompt Hardening:** All user-supplied text (transcripts, demographics, prompts) is sanitized via `PromptInputHardeningService` to prevent prompt injection attacks
- **Rate Limiting:** Middleware-based HTTP rate limiting with pluggable backends (in-memory for dev, Cosmos for production) protects against abuse
- **Approval Workflow:** AI-generated documentation is clearly labeled as a draft. Sessions require explicit therapist attestation and approval before export. Editing clinical content automatically clears approval status.
- **Encrypted Sensitive Data:** Client date of birth is stored AES-GCM encrypted at rest and is never returned through the API; only a computed age range is forwarded to the AI pipeline
- **Access Control:** All HTTP endpoints enforce JWT ownership � therapists cannot read or modify another therapist's sessions, goals, or client records
- **Client ID Namespacing:** Client IDs are transparently namespaced server-side using a hash of the therapist's email address; the raw client-visible name is stripped from API responses and never stored without the prefix
- The React SPA contains only public, non-sensitive Entra configuration values

---

## Recent Enhancements

### Speech Token Reliability (June 2026)
TheraGraf now implements **proactive speech token refresh** to support long therapy sessions:
- 🔄 Automatic token renewal every 8 minutes during recording
- 🛡️ Graceful error handling - recording continues even if a refresh fails
- ⏱️ Supports the full 45-minute default session duration without interruption
- 📊 Console logging for token refresh events to aid debugging

Azure Speech tokens typically expire after 10 minutes. Without proactive refresh, sessions longer than 10 minutes would experience transcription failures. The 8-minute refresh interval provides a safety margin while ensuring uninterrupted real-time transcription for extended clinical sessions.

### Accessibility (June 2026)
TheraGraf now meets **WCAG 2.1 Level AA** accessibility standards:
- ? Full keyboard navigation with skip links
- ? Screen reader support with ARIA landmarks and live regions
- ? Semantic HTML with proper heading hierarchy
- ? Descriptive labels for all interactive elements
- ? Focus management on navigation
- ? Accessible data tables with sortable columns
- ? Dynamic content announcements for loading states and errors

See [docs/accessibility-testing-results.md](docs/accessibility-testing-results.md) for complete testing guide and WCAG compliance checklist.

### End-to-End Testing (June 2026)
Comprehensive Playwright E2E test suite covering:
- ? Real Azure AD authentication flow
- ? Session creation with mock backend support
- ? SOAP/DAP note editing and approval workflow
- ? CPT/ICD code management
- ? Dashboard navigation and caseload management
- ? Multi-browser testing (Chromium, Firefox, WebKit)

Run tests with `npm run test:e2e` or in UI mode with `npm run test:e2e:ui`. See [Theragraf.Web/tests/e2e/README.md](Theragraf.Web/tests/e2e/README.md) for details.

### Approval Workflow
- Sessions require explicit therapist attestation before approval
- Approval status badge visible throughout the UI
- Editing clinical content automatically clears approval
- Export functions (PDF, 837P) only available for approved sessions

---
