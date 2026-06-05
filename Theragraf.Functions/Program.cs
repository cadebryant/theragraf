using Azure;
using Azure.AI.TextAnalytics;
using Azure.AI.OpenAI;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using OpenAI;
using System.ClientModel;
using Theragraf.Core.Services;
using Theragraf.Functions.Agents;
using Theragraf.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
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
            var kernelBuilder = Kernel.CreateBuilder();

            var deploymentName = config["AzureOpenAI:DeploymentName"]!;
            var aoaiEndpoint = config["AzureOpenAI:Endpoint"]!;
            var aoaiApiKey = config["AzureOpenAI:ApiKey"];
            Console.WriteLine($"[Theragraf] DeploymentName='{deploymentName}' Endpoint='{aoaiEndpoint}'");

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

            Console.WriteLine($"[Theragraf] Loading plugins from: {pluginsPath}");
            Console.WriteLine($"[Theragraf] Plugin dirs: {string.Join(", ", Directory.GetDirectories(pluginsPath))}");

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

        // Persistence
        // Uses connection string locally (Azurite); uses Managed Identity in Azure
        // when AzureStorage:AccountName is set instead.
        services.AddSingleton(sp =>
        {
            var accountName = config["AzureStorage:AccountName"];
            return string.IsNullOrWhiteSpace(accountName)
                ? new TableServiceClient(config["AzureWebJobsStorage"]!)
                : new TableServiceClient(
                    new Uri($"https://{accountName}.table.core.windows.net"),
                    new DefaultAzureCredential());
        });
        services.AddSingleton<ISessionRepository, TableStorageSessionRepository>();
    })
    .Build();

host.Run();
