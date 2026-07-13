using Azure;
using Azure.AI.TextAnalytics;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.SemanticKernel;
using OpenAI;
using System.ClientModel;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Agents;
using Theragraf.Functions.Configuration;
using Theragraf.Functions.Logging;
using Theragraf.Functions.Middleware;
using Theragraf.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(workerApp =>
    {
        workerApp.UseMiddleware<JwtAuthMiddleware>();
        workerApp.UseMiddleware<TenantResolutionMiddleware>();
        workerApp.UseMiddleware<RateLimitMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<IAuditLogger, ApplicationInsightsAuditLogger>();

        var config = context.Configuration;

        // Rate limiting configuration
        var rateLimitConfig = new Theragraf.Functions.Configuration.RateLimitConfiguration();
        config.GetSection(Theragraf.Functions.Configuration.RateLimitConfiguration.Section).Bind(rateLimitConfig);
        services.AddSingleton(rateLimitConfig);

        // Retention policy configuration for HIPAA compliance
        var retentionPolicy = new Theragraf.Functions.Configuration.RetentionPolicyConfiguration();
        config.GetSection(Theragraf.Functions.Configuration.RetentionPolicyConfiguration.Section).Bind(retentionPolicy);
        services.AddSingleton<RetentionPolicy>(retentionPolicy);

        // Register rate limit service based on environment.
        // In Azure (production), use Cosmos DB for distributed rate limiting.
        // Locally/in tests, use in-memory for speed (no external dependencies).
        if (rateLimitConfig.UseDistributedBackend && !string.IsNullOrWhiteSpace(config["CosmosDb:AccountEndpoint"]))
        {
            services.AddSingleton<IRateLimitService>(sp =>
            {
                var cosmosClient = sp.GetRequiredService<CosmosClient>();
                var dbName = config["CosmosDb:DatabaseName"] ?? "theragraf";
                var logger = sp.GetRequiredService<ILogger<CosmosRateLimitService>>();

                // Auto-provision rate-limit container on all deployments (local emulator and Azure).
                // This ensures the container exists with proper TTL configuration for automatic cleanup.
                var db = cosmosClient.CreateDatabaseIfNotExistsAsync(dbName).GetAwaiter().GetResult();
                db.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = CosmosRateLimitService.ContainerName,
                        PartitionKeyPath = "/userId",
                        DefaultTimeToLive = 60  // Automatically delete rate limit documents after 60 seconds
                    })
                    .GetAwaiter().GetResult();

                return new CosmosRateLimitService(cosmosClient, dbName, logger);
            });
        }
        else
        {
            // Use in-memory service (testing, or when Cosmos isn't configured).
            services.AddSingleton<IRateLimitService, MemoryRateLimitService>();
        }

        // Azure AI Language (PII Redaction)
        // Uses API key locally; falls back to Managed Identity in Azure when no key is configured.
        services.AddSingleton(_ =>
        {
            var endpoint = new Uri(config["AzureLanguage:Endpoint"]!);
            var apiKey = config["AzureLanguage:ApiKey"];
            return string.IsNullOrWhiteSpace(apiKey)
                ? new TextAnalyticsClient(endpoint, new DefaultAzureCredential())
                : new TextAnalyticsClient(endpoint, new AzureKeyCredential(apiKey));
        });
        services.AddSingleton<ITextAnalyticsClientAdapter, TextAnalyticsClientAdapter>();
        services.AddSingleton<IPiiRedactionService, PiiRedactionService>();
        services.AddSingleton<IPromptInputHardeningService, PromptInputHardeningService>();

        // Semantic Kernel — Azure OpenAI
        services.AddSingleton<Kernel>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Theragraf.Startup");
            var kernelBuilder = Kernel.CreateBuilder();

            var deploymentName = config["AzureOpenAI:DeploymentName"]!;
            var aoaiEndpoint = config["AzureOpenAI:Endpoint"]!;
            var aoaiApiKey = config["AzureOpenAI:ApiKey"];
            logger.LogInformation("Configuring Semantic Kernel: deployment={DeploymentName} endpoint={Endpoint}",
                deploymentName, aoaiEndpoint);

            // Uses API key locally; uses Managed Identity in Azure when no key is configured.
            var azureClient = string.IsNullOrWhiteSpace(aoaiApiKey)
                ? new AzureOpenAIClient(
                    new Uri(aoaiEndpoint),
                    new DefaultAzureCredential(),
                    new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview))
                : new AzureOpenAIClient(
                    new Uri(aoaiEndpoint),
                    new AzureKeyCredential(aoaiApiKey),
                    new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview));

            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                azureOpenAIClient: azureClient
            );

            var kernel = kernelBuilder.Build();

            // Load prompt plugins from the Plugins directory
            var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");

            if (!Directory.Exists(pluginsPath))
                throw new DirectoryNotFoundException($"Plugins directory not found at: {pluginsPath}");

            logger.LogInformation("Loading SK plugins from {PluginsPath}", pluginsPath);

            kernel.ImportPluginFromPromptDirectory(Path.Combine(pluginsPath, "SoapAgent"), "SoapAgent");
            kernel.ImportPluginFromPromptDirectory(Path.Combine(pluginsPath, "ComplianceAgent"), "ComplianceAgent");
            kernel.ImportPluginFromPromptDirectory(Path.Combine(pluginsPath, "BillingAgent"), "BillingAgent");
            kernel.ImportPluginFromPromptDirectory(Path.Combine(pluginsPath, "Icd10Agent"), "Icd10Agent");
            kernel.ImportPluginFromPromptDirectory(Path.Combine(pluginsPath, "GoalAgent"), "GoalAgent");

            return kernel;
        });

        services.AddSingleton<ISoapAgent, SoapAgent>();
        services.AddSingleton<IComplianceAgent, ComplianceAgent>();
        services.AddSingleton<ICmsUnitCalculator, CmsUnitCalculator>();
        services.AddSingleton<IBillingAgent, BillingAgent>();
        services.AddSingleton<IIcd10Agent, Icd10Agent>();
        services.AddSingleton<IGoalAgent, GoalAgent>();

        // Persistence — Azure Cosmos DB for NoSQL
        // Local: connection string from CosmosDb:ConnectionString (Cosmos Emulator)
        // Azure: endpoint + Managed Identity when CosmosDb:AccountEndpoint is set
        // Redaction-map encryption — AES-256-GCM via Key Vault in Azure; pass-through locally
        services.AddSingleton<IRedactionMapEncryption>(sp =>
        {
            var vaultUriStr = config["KeyVault:VaultUri"];
            if (string.IsNullOrWhiteSpace(vaultUriStr))
                return new NullRedactionMapEncryption();
            return new AesGcmRedactionMapEncryption(new Uri(vaultUriStr), new DefaultAzureCredential());
        });

        services.AddSingleton(sp =>
        {
            var options = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };
            var endpoint = config["CosmosDb:AccountEndpoint"];
            return string.IsNullOrWhiteSpace(endpoint)
                ? new CosmosClient(config["CosmosDb:ConnectionString"]!, options)
                : new CosmosClient(endpoint, new DefaultAzureCredential(), options);
        });
        services.AddSingleton<ISessionRepository>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var encryption   = sp.GetRequiredService<IRedactionMapEncryption>();
            var dbName       = config["CosmosDb:DatabaseName"] ?? "theragraf";
            var container    = config["CosmosDb:ContainerName"] ?? "sessions";

            // When running against the local emulator (no AccountEndpoint configured),
            // ensure the database and container exist. In Azure, Bicep owns provisioning.
            var endpoint = config["CosmosDb:AccountEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                var db = cosmosClient.CreateDatabaseIfNotExistsAsync(dbName).GetAwaiter().GetResult();
                db.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = container,
                        PartitionKeyPath = "/clientId"
                    })
                    .GetAwaiter().GetResult();
            }

            var retentionPolicy = sp.GetRequiredService<RetentionPolicy>();
            return new CosmosSessionRepository(cosmosClient, dbName, container, encryption, retentionPolicy);
        });

        services.AddSingleton<IGoalRepository>(sp =>
        {
            var cosmosClient    = sp.GetRequiredService<CosmosClient>();
            var dbName          = config["CosmosDb:DatabaseName"] ?? "theragraf";
            var goalsContainer  = config["CosmosDb:GoalsContainerName"] ?? "goals";

            // Auto-provision goals container locally. In Azure, add the container in Bicep.
            var endpoint = config["CosmosDb:AccountEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                var db = cosmosClient.CreateDatabaseIfNotExistsAsync(dbName).GetAwaiter().GetResult();
                db.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = goalsContainer,
                        PartitionKeyPath = "/clientId"
                    })
                    .GetAwaiter().GetResult();
            }

            var retentionPolicy = sp.GetRequiredService<RetentionPolicy>();
            return new CosmosGoalRepository(cosmosClient, dbName, goalsContainer, retentionPolicy);
        });

        services.AddSingleton<IClientRepository>(sp =>
        {
            var cosmosClient      = sp.GetRequiredService<CosmosClient>();
            var encryption        = sp.GetRequiredService<IRedactionMapEncryption>();
            var dbName            = config["CosmosDb:DatabaseName"] ?? "theragraf";
            var clientsContainer  = config["CosmosDb:ClientsContainerName"] ?? "clients";

            // Auto-provision clients container locally. In Azure, Bicep owns provisioning.
            var endpoint = config["CosmosDb:AccountEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                var db = cosmosClient.CreateDatabaseIfNotExistsAsync(dbName).GetAwaiter().GetResult();
                db.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = clientsContainer,
                        PartitionKeyPath = "/clientId"
                    })
                    .GetAwaiter().GetResult();
            }

            return new CosmosClientRepository(cosmosClient, dbName, clientsContainer, encryption);
        });

        services.AddSingleton<ITenantRepository>(sp =>
        {
            var cosmosClient        = sp.GetRequiredService<CosmosClient>();
            var dbName              = config["CosmosDb:DatabaseName"] ?? "theragraf";
            var tenantsContainer    = config["CosmosDb:TenantsContainerName"] ?? "tenants";
            var profilesContainer   = config["CosmosDb:TherapistProfilesContainerName"] ?? "therapist-profiles";
            var providersContainer  = config["CosmosDb:ProvidersContainerName"] ?? "providers";
            var logger              = sp.GetRequiredService<ILogger<CosmosTenantRepository>>();

            // Auto-provision tenant-related containers locally. In Azure, Bicep owns provisioning.
            var endpoint = config["CosmosDb:AccountEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                var db = cosmosClient.CreateDatabaseIfNotExistsAsync(dbName).GetAwaiter().GetResult();

                // tenants — single-level partition key (tenantId IS the top-level entity)
                db.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties { Id = tenantsContainer, PartitionKeyPath = "/tenantId" })
                    .GetAwaiter().GetResult();

                // therapist-profiles — hierarchical: /tenantId + /therapistId
                db.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = profilesContainer,
                        PartitionKeyPaths = ["/tenantId", "/therapistId"]
                    })
                    .GetAwaiter().GetResult();

                // providers — hierarchical: /tenantId + /providerId
                db.Database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties
                    {
                        Id = providersContainer,
                        PartitionKeyPaths = ["/tenantId", "/providerId"]
                    })
                    .GetAwaiter().GetResult();
            }

            return new CosmosTenantRepository(cosmosClient, dbName, tenantsContainer, logger);
        });

        services.AddSingleton<ITherapistProfileRepository>(sp =>
        {
            var cosmosClient       = sp.GetRequiredService<CosmosClient>();
            var dbName             = config["CosmosDb:DatabaseName"] ?? "theragraf";
            var profilesContainer  = config["CosmosDb:TherapistProfilesContainerName"] ?? "therapist-profiles";
            var logger             = sp.GetRequiredService<ILogger<CosmosTherapistProfileRepository>>();
            return new CosmosTherapistProfileRepository(cosmosClient, dbName, profilesContainer, logger);
        });

        services.AddSingleton<IProviderRepository>(sp =>
        {
            var cosmosClient       = sp.GetRequiredService<CosmosClient>();
            var dbName             = config["CosmosDb:DatabaseName"] ?? "theragraf";
            var providersContainer = config["CosmosDb:ProvidersContainerName"] ?? "providers";
            var logger             = sp.GetRequiredService<ILogger<CosmosProviderRepository>>();
            return new CosmosProviderRepository(cosmosClient, dbName, providersContainer, logger);
        });
    })
    .Build();

// ── HITECH Production Guards ──────────────────────────────────────────────────
// These checks run once at startup and crash the host immediately if a
// security-critical misconfiguration is detected. Failing fast in the deployment
// pipeline is far safer than silently running without authentication or encryption.
var env    = host.Services.GetRequiredService<IHostEnvironment>();
var config = host.Services.GetRequiredService<IConfiguration>();

if (!env.IsDevelopment())
{
    // Guard 1: Authentication must never be disabled outside of local dev.
    if (config.GetValue<bool>("Auth:Disabled"))
        throw new InvalidOperationException(
            "HITECH guard: Auth:Disabled=true is not permitted outside the Development environment. " +
            "Remove or set Auth:Disabled=false in your production configuration.");

    // Guard 2: Redaction-map encryption must be active outside of local dev.
    var encryption = host.Services.GetRequiredService<IRedactionMapEncryption>();
    if (!encryption.IsEnabled)
        throw new InvalidOperationException(
            "HITECH guard: Redaction-map encryption is disabled (KeyVault:VaultUri is not configured). " +
            "Set KeyVault:VaultUri to your Azure Key Vault URI in production configuration.");
}

host.Run();
