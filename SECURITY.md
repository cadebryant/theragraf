# Security & HITECH Compliance

Theragraf processes electronic Protected Health Information (ePHI) and is designed
to operate under HIPAA/HITECH requirements. This document records the controls that
are implemented in code and the operational steps that must be completed before any
production deployment.

---

## 1. Controls Implemented in Code

| HIPAA §164 Rule | Control | Status |
|---|---|---|
| §164.312(a)(1) Access Control | Entra ID JWT validated by `JwtAuthMiddleware` on every HTTP endpoint | ✅ |
| §164.312(a)(1) Access Control | `ClientIdHelper.Namespace()` scopes every record to its owner; namespace checked on every request | ✅ |
| §164.312(a)(1) Access Control | Production startup guard — host refuses to start if `Auth:Disabled=true` outside Development | ✅ |
| §164.312(b) Audit Controls | `ApplicationInsightsAuditLogger` writes structured audit events (actor, action, resource, outcome, correlationId) to App Insights; excluded from adaptive sampling in `host.json` | ✅ |
| §164.312(b) Audit Controls | All PHI-touching endpoints emit `AuditEvent.Success` / `AuditEvent.Failure` | ✅ |
| §164.312(c)(1) Integrity | Re-redaction pass on every SOAP-note edit in `SessionsUpdate`; ownership verified before any write | ✅ |
| §164.312(d) Person Authentication | Token validated against Entra ID OIDC metadata; issuer, audience, and signing keys all verified | ✅ |
| §164.312(e)(1) Transmission Security | Azure Functions enforce HTTPS; all Azure service traffic uses TLS | ✅ |
| §164.312(e)(2)(ii) Encryption | AES-256-GCM encryption for redaction maps via Azure Key Vault (`AesGcmRedactionMapEncryption`) | ✅ |
| §164.312(e)(2)(ii) Encryption | Production startup guard — host refuses to start if `KeyVault:VaultUri` is blank outside Development | ✅ |
| §164.308(a)(1) Risk Management | PII redacted from transcripts before LLM processing and before persistence (`PiiRedactionService`) | ✅ |
| §164.308(a)(1) Risk Management | PHI never written to standard log channels (`LogSanitizer`); error responses sanitized (`SafeErrorHelper`) | ✅ |
| §164.308(a)(4) Minimum Necessary | Only age (not DOB) forwarded to AI agents; raw DOB encrypted before storage in client demographics | ✅ |
| §164.308(a)(7) Contingency Plan | Soft-delete + restore on all session records; configurable 6-year retention policy (`RetentionPolicy`) | ✅ |
| §164.308(a)(8) Evaluation | Rate limiting on all endpoints via `RateLimitMiddleware` (distributed Cosmos backend in prod) | ✅ |
| §164.308(a)(3) Workforce Security | Demo/seed endpoints (`SeedData`, `DeleteSeedData`, `MarkAllSynthetic`) require authentication; all three emit audit events | ✅ |
| §164.524 Right of Access | `GET /api/clients/{clientId}/export` returns a complete ePHI bundle (demographics, sessions, goals) for a given client; auth + ownership enforced; audit logged | ✅ |
| HITECH §13402 Breach Notification | Audit trail in Application Insights enables breach scope determination — see §3 below | ⚠️ Partial |

---

## 2. Operational Steps Required Before Production

The following items **cannot be enforced in code** and must be completed manually.

### 2.1  Sign a Business Associate Agreement (BAA) with Microsoft

All Azure services used (Azure OpenAI, Azure AI Language, Azure Cosmos DB, Azure
Functions, Application Insights, Azure Key Vault, Azure Speech) touch or store ePHI.
Microsoft offers a BAA at no additional cost.

1. Log in to the [Azure portal](https://portal.azure.com).
2. Navigate to **Cost Management + Billing → Billing account → Agreements**.
3. Accept the **Microsoft HIPAA BAA**.

> ⚠️ Do not store real ePHI in any Azure service until the BAA is signed.

### 2.2  Rotate API Keys

`local.settings.json` is excluded from git (see `.gitignore`), but the keys it
contains should be rotated if there is any doubt about whether they have been
exposed (e.g. via Dropbox sync, shared drives, or accidental commits in earlier
history):

- Azure OpenAI key → Azure portal › Cognitive Services › Keys and Endpoint
- Azure AI Language key → Azure portal › Language resource › Keys and Endpoint
- Azure Speech key → Azure portal › Speech resource › Keys and Endpoint

After rotation, update your local `local.settings.json` and any Azure Function App
configuration entries.

### 2.3  Configure Key Vault in Production

The production Function App **must** have `KeyVault:VaultUri` set, otherwise the
startup guard (added in `Program.cs`) will refuse to start the host. Steps:

1. Create an Azure Key Vault.
2. Grant the Function App's Managed Identity the **Key Vault Secrets User** role.
3. Create a secret named `redaction-map-key` containing 32 bytes of base64-encoded
   key material (see `AesGcmRedactionMapEncryption` for format requirements).
4. Set `KeyVault__VaultUri` in the Function App configuration.

### 2.4  Set `Auth:Disabled=false` in Production

The production Function App configuration must **not** contain `Auth__Disabled=true`.
The startup guard will throw if it does. Verify:

- Azure portal › Function App › Configuration › Application settings

Also ensure `AzureAd__TenantId` and `AzureAd__ClientId` are set correctly so the
JWT middleware can validate tokens.

### 2.5  Enable Application Insights and Verify Audit Logs

Audit events are written as `TraceTelemetry` with `customDimensions["audit"] == "true"`.
Run this Kusto query in Log Analytics to confirm events are flowing:

```kql
traces
| where customDimensions["audit"] == "true"
| project timestamp,
		  actor         = customDimensions["actor"],
		  action        = customDimensions["action"],
		  resourceType  = customDimensions["resourceType"],
		  resourceId    = customDimensions["resourceId"],
		  outcome       = customDimensions["outcome"],
		  correlationId = customDimensions["correlationId"],
		  detail        = customDimensions["detail"]
| order by timestamp desc
```

### 2.6  Configure Data Retention

The default `RetentionPolicy` retains records for 6 years (federal HIPAA minimum).
State regulations may require longer retention (e.g. 10 years in some states for
minor patients). Adjust `RetentionPolicy__RetentionYears` in Function App settings
before go-live. Set `RetentionPolicy__AutoPurgeEnabled=true` to enable automatic
Cosmos DB TTL-based purge.

---

## 3. Breach Notification (HITECH §13402)

HITECH requires notifying affected individuals within **60 days** of discovering a
breach of unsecured ePHI. The audit trail in Application Insights is the primary
tool for determining breach scope.

To investigate a suspected breach:

1. Open Log Analytics for the Function App's Application Insights workspace.
2. Run the Kusto query in §2.5 to enumerate all access events.
3. Filter by `actor`, `resourceId`, or time range to scope the incident.
4. Preserve the query results as evidence.

No automated breach-detection pipeline is implemented yet. Consider adding an
Application Insights alert rule on anomalous `AccessDenied` event volume as a
first detection layer.

---

## 4. Out of Scope

- **Patient self-service right-of-access portal** — `GET /api/clients/{clientId}/export`
  provides a complete ePHI bundle that therapists can use to respond to patient
  access requests. A dedicated patient-facing UI button is not yet implemented but
  is not required for initial HIPAA compliance (the obligation falls on the covered
  entity, not the business associate).
- **Business Associate sub-agreements** — if you share ePHI with downstream
  processors (e.g. billing clearinghouses), a BAA is required with each.
- **Physical safeguards (§164.310)** — workstation and device policies are outside
  the scope of this application.
