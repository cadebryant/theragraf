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
        services.AddSingleton<IPiiRedactionService, PiiRedactionService>();

        // Semantic Kernel (placeholder — LLM wired up next)
        services.AddSingleton<Kernel>(_ => new Kernel());
        services.AddKeyedSingleton<BaseAgent, SoapAgent>("SoapAgent");
    })
    .Build();

host.Run();