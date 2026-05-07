using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Theragraf.Functions.Agents;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<Kernel>(sp => new Kernel());
        services.AddKeyedSingleton<BaseAgent, SoapAgent>("SoapAgent");
    })
    .Build();

host.Run();
