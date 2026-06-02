using Azure;
using Azure.AI.TextAnalytics;
using Azure.AI.OpenAI;
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
        services.AddSingleton(_ => new TextAnalyticsClient(
            new Uri(config["AzureLanguage:Endpoint"]!),
            new AzureKeyCredential(config["AzureLanguage:ApiKey"]!)
        ));
        services.AddSingleton<ITextAnalyticsClientAdapter, TextAnalyticsClientAdapter>();
        services.AddSingleton<IPiiRedactionService, PiiRedactionService>();

        // Semantic Kernel — Azure OpenAI
        services.AddSingleton<Kernel>(sp =>
        {
            var kernelBuilder = Kernel.CreateBuilder();

            var deploymentName = config["AzureOpenAI:DeploymentName"]!;
            var aoaiEndpoint = config["AzureOpenAI:Endpoint"]!;
            var aoaiApiKey = config["AzureOpenAI:ApiKey"]!;
            Console.WriteLine($"[Theragraf] DeploymentName='{deploymentName}' Endpoint='{aoaiEndpoint}'");

            // o4-mini requires a newer API version than SK's default
            var azureClient = new AzureOpenAIClient(
                new Uri(aoaiEndpoint),
                new AzureKeyCredential(aoaiApiKey),
                new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview)
            );

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

            return kernel;
        });

        services.AddSingleton<ISoapAgent, SoapAgent>();
        services.AddSingleton<IComplianceAgent, ComplianceAgent>();
        services.AddSingleton<IBillingAgent, BillingAgent>();
    })
    .Build();

host.Run();