using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
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

            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: config["AzureOpenAI:DeploymentName"]!,
                endpoint: config["AzureOpenAI:Endpoint"]!,
                apiKey: config["AzureOpenAI:ApiKey"]!
            );

            var kernel = kernelBuilder.Build();

            // Load prompt plugins from the Plugins directory
            var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
            kernel.ImportPluginFromPromptDirectory(pluginsPath, "SoapAgent");
            kernel.ImportPluginFromPromptDirectory(pluginsPath, "ComplianceAgent");

            return kernel;
        });

        services.AddSingleton<ISoapAgent, SoapAgent>();
        services.AddSingleton<IComplianceAgent, ComplianceAgent>();
    })
    .Build();

host.Run();