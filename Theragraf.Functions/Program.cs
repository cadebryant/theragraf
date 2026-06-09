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
using Theragraf.Core.Services;
using Theragraf.Functions.Agents;
using Theragraf.Functions.Middleware;
using Theragraf.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(workerApp =>
    {
        workerApp.UseMiddleware<JwtAuthMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var config = context.Configuration;

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

            return kernel;
        });

        services.AddSingleton<ISoapAgent, SoapAgent>();
        services.AddSingleton<IComplianceAgent, ComplianceAgent>();
        services.AddSingleton<ICmsUnitCalculator, CmsUnitCalculator>();
        services.AddSingleton<IBillingAgent, BillingAgent>();
        services.AddSingleton<IIcd10Agent, Icd10Agent>();

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
            return new CosmosSessionRepository(cosmosClient, dbName, container, encryption);
        });
    })
    .Build();

host.Run();
