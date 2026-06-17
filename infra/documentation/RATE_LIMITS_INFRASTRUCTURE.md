// Rate Limiting Infrastructure Documentation

/*
  RATE-LIMITS CONTAINER OVERVIEW
  ==============================

  The rate-limits container stores transient rate limit state for distributed
  rate limiting across multiple Azure Functions instances.

  Location: Cosmos DB → theragraf database → rate-limits container
  Partition Key: /userId
  Time-to-Live (TTL): 60 seconds (automatic cleanup after 60 seconds)

  DEPLOYMENT OPTIONS
  ==================

  Option A: Bicep Infrastructure (IaC) — RECOMMENDED FOR PRODUCTION
  ──────────────────────────────────────────────────────────────────
  The rate-limits container is defined in: infra/modules/cosmos.bicep

  Resource properties:
  • Name: rate-limits
  • Partition Key: /userId (per-user rate limit isolation)
  • TTL: 60 seconds (documents auto-delete when this time elapses)
  • Indexing: minimal (Count, WindowStart excluded)

  When you deploy with Bicep (e.g., `azd up`), the container is automatically
  created with proper TTL configuration for production.

  Option B: Runtime Auto-Provisioning (Fallback)
  ──────────────────────────────────────────────
  If the container does not exist, Program.cs will auto-create it on first
  Functions startup:

	var db = cosmosClient.CreateDatabaseIfNotExistsAsync(dbName).GetAwaiter().GetResult();
	db.Database.CreateContainerIfNotExistsAsync(
		new ContainerProperties
		{
			Id = "rate-limits",
			PartitionKeyPath = "/userId",
			DefaultTimeToLive = 60
		})
		.GetAwaiter().GetResult();

  This ensures the container is available in:
  • Local Cosmos DB Emulator (on first run)
  • Azure (if Bicep deployment is skipped, though not recommended)

  DOCUMENT STRUCTURE
  ==================

  Rate limit documents stored in the container have this structure:

	{
	  "id": "ratelimit#user1#DocumentationStart#DocumentationPipeline",
	  "userId": "user1",
	  "endpointName": "DocumentationStart",
	  "policyName": "DocumentationPipeline",
	  "count": 3,
	  "windowStart": "2025-01-15T14:30:45Z",
	  "timeToLive": 60,
	  "ttl": 60
	}

  Fields:
  • id: Composite key (ratelimit#{userId}#{endpointName}#{policyName})
  • userId: Partition key — used to scale across partitions
  • endpointName: Which endpoint (e.g., DocumentationStart, SpeechTokenGet)
  • policyName: Which policy tier (e.g., DocumentationPipeline, SpeechToken)
  • count: Number of requests in the current time window
  • windowStart: When the current 60-second window started
  • timeToLive: TTL in seconds (set to 60 for auto-cleanup)
  • ttl: System field (set by Cosmos DB)

  AUTOMATIC CLEANUP
  =================

  Cosmos DB's TTL feature automatically deletes documents after the specified
  duration. This means:

  ✓ No manual cleanup needed
  ✓ No unbounded storage growth
  ✓ No stale rate limit state

  When a rate limit document reaches the TTL (60 seconds after creation), it
  is automatically deleted by Cosmos DB.

  DEPLOYMENT CHECKLIST
  ====================

  [✓] Bicep definition: infra/modules/cosmos.bicep (added rateLimitsContainer)
  [✓] Runtime fallback: Theragraf.Functions/Program.cs (auto-provision)
  [✓] Configuration: RateLimitConfiguration.cs (per-tier limits)
  [✓] Middleware: RateLimitMiddleware.cs (enforcement)
  [✓] Service: CosmosRateLimitService.cs (Cosmos DB backend)
  [✓] Tests: MemoryRateLimitServiceTests.cs (unit tests pass)

  VERIFICATION STEPS
  ==================

  Local Testing (Cosmos DB Emulator):
  1. Start Cosmos DB Emulator
  2. Deploy the function app locally
  3. Make a request to any endpoint
  4. Verify container "rate-limits" is created in the "theragraf" database
  5. Check that documents are created with userId partition keys

  Production (Azure):
  1. Run `azd up` (deploys Bicep, creates rate-limits container)
  2. Function app starts and uses the provisioned container
  3. Monitor Cosmos DB throughput (RU/s) — typically <1 RU/s under normal load
  4. Use KQL queries in Application Insights to monitor rate limit violations

  PERFORMANCE CHARACTERISTICS
  ===========================

  RU/s Usage:
  • Typical request: 1-2 RUs (read + write)
  • Under 100 req/min from a single user: ~3-5 RUs total
  • Sustained across therapy platform: <10 RUs/s

  Container Sizing:
  • Serverless (pay-per-request): $0.25 per 1M RUs — ideal for this workload
  • Provisioned (fixed throughput): 400 RU/s minimum = ~$50/month
  • Recommendation: Use serverless for rate-limits container

  TROUBLESHOOTING
  ===============

  Issue: "Container not found" exception
  ──────────────────────────────────────
  Solution: 
  • Verify RateLimitConfiguration.UseDistributedBackend = true
  • Verify CosmosDb:AccountEndpoint is set
  • Check that Bicep deployment completed successfully
  • Function app will auto-create the container if missing

  Issue: Rate limit documents not expiring
  ─────────────────────────────────────────
  Solution:
  • Verify defaultTtl = 60 on the container (check Azure Portal)
  • TTL must be enabled at container level, not just in code
  • If updating manually, set Container Settings → Default TTL to 60

  Issue: High RU/s consumption
  ────────────────────────────
  Solution:
  • Check for runaway rate limit checks (logging will show violations)
  • Verify indexing exclusions (Count, WindowStart) are applied
  • Consider increasing TimeWindowSeconds if legitimate sustained load

  MONITORING
  ==========

  Application Insights Queries:

  // Count rate limit violations by user
  traces
  | where message startswith "Rate limit exceeded"
  | summarize Violations = count() by tostring(customDimensions.userId)
  | top 10 by Violations desc

  // RU/s consumption for rate-limits container
  // (Query Cosmos DB diagnostics logs)
  AzureDiagnostics
  | where resourceProvider == "Microsoft.DocumentDB"
  | where collectionName == "rate-limits"
  | summarize RuConsumed = sum(todouble(requestCharge))

  See RateLimitMonitoring.kql for comprehensive queries.

  NEXT STEPS
  ==========

  1. Deploy with `azd up` to create the container in Azure
  2. Monitor for rate limit violations in Application Insights
  3. Adjust rate limits if needed (local.settings.json for testing)
  4. Set up alert rules for sustained abuse (>20 violations/30min)

*/
