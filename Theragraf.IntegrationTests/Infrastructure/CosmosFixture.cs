namespace Theragraf.IntegrationTests.Infrastructure;

using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using System.Net.NetworkInformation;

/// <summary>
/// xUnit collection fixture that owns a single CosmosClient pointed at the local
/// Cosmos DB Emulator (https://localhost:8081). All tests in the "Cosmos" collection
/// share this fixture so container creation only happens once per test run.
///
/// On <see cref="InitializeAsync"/> the fixture will attempt to launch the emulator
/// executable if the port is not yet listening, then wait up to 120 seconds for it
/// to become ready before giving up.
///
/// If the emulator cannot be started or found, <see cref="IsAvailable"/> is set to
/// false and each test skips via <see cref="SkipIfUnavailable"/>.
/// </summary>
public sealed class CosmosFixture : IAsyncLifetime
{
    // The canonical well-known master key for the Azure Cosmos DB Emulator (88 chars, ends with KA==).
    // See: https://learn.microsoft.com/en-us/azure/cosmos-db/emulator
    private const string EmulatorEndpoint = "https://localhost:8081/";
    private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const int EmulatorPort = 8081;
    private const int StartupTimeoutSecs = 120;  // max for the HTTP readiness probe
    private const int SdkTimeoutSecs = 20;   // per-request Cosmos SDK timeout
    private const int TotalTimeoutSecs = StartupTimeoutSecs + SdkTimeoutSecs + 10; // hard deadline

    private static readonly string[] _candidatePaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),         @"Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),      @"Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"),
    ];

    public const string DatabaseName = "theragraf-integration-tests";
    public const string ContainerName = "sessions";

    public bool IsAvailable { get; private set; }
    public CosmosClient Client { get; private set; } = null!;
    public Container Container { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Hard outer deadline — prevents the test runner from freezing entirely
        // if the emulator port is open but the data plane is in a bad state.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TotalTimeoutSecs));
        var ct = cts.Token;

        if (!IsPortListening(EmulatorPort))
        {
            if (!TryLaunchEmulator())
            {
                IsAvailable = false;
                return;
            }
        }

        // Always do an HTTP readiness probe — the port can be listening while
        // the Cosmos data plane API is still initialising, which causes the SDK
        // to receive a malformed response and throw a Base-64 parse error.
        if (!await WaitForReadyAsync(EmulatorPort, TimeSpan.FromSeconds(StartupTimeoutSecs), ct))
        {
            Console.WriteLine($"[CosmosFixture] Emulator did not become ready within {StartupTimeoutSecs}s.");
            IsAvailable = false;
            return;
        }

        try
        {
            Client = new CosmosClient(
                EmulatorEndpoint,
                EmulatorKey,
                new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    },
                    // Emulator uses a self-signed cert — allow it.
                    HttpClientFactory = () => new HttpClient(new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    }),
                    ConnectionMode = ConnectionMode.Gateway,
                    // Prevent the SDK from attempting regional endpoint discovery,
                    // which causes a Base-64 parse failure against the local emulator.
                    LimitToEndpoint = true,
                    // Belt-and-suspenders: cap each individual SDK request so a stalled
                    // emulator data plane cannot block the test runner indefinitely.
                    RequestTimeout = TimeSpan.FromSeconds(SdkTimeoutSecs)
                });

            var db = await Client.CreateDatabaseIfNotExistsAsync(
                DatabaseName,
                cancellationToken: ct);

            var containerResponse = await db.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties
                {
                    Id = ContainerName,
                    PartitionKeyPath = "/clientId"
                },
                cancellationToken: ct);

            Container = containerResponse.Container;
            IsAvailable = true;
        }
        catch (OperationCanceledException)
        {
            IsAvailable = false;
            Console.WriteLine($"[CosmosFixture] Initialization timed out after {TotalTimeoutSecs}s. " +
                              "The emulator port was open but the data plane did not respond. " +
                              "Try restarting the Cosmos DB Emulator.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            Console.WriteLine($"[CosmosFixture] Could not connect to emulator ({ex.GetType().Name}): {ex.Message}");
            Console.WriteLine($"[CosmosFixture] Stack: {ex.StackTrace}");
            if (ex.InnerException is not null)
                Console.WriteLine($"[CosmosFixture] Inner ({ex.InnerException.GetType().Name}): {ex.InnerException.Message}");
        }
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable) return;

        // Delete the test database entirely on teardown so each run starts clean.
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(SdkTimeoutSecs));
            await Client.GetDatabase(DatabaseName).DeleteAsync(cancellationToken: cts.Token);
        }
        catch { /* best effort */ }

        Client.Dispose();
    }

    /// <summary>
    /// Call at the top of every integration test. Uses <see cref="Skip.If"/> so the
    /// test is reported as Skipped (not Failed) when the emulator is unavailable.
    /// Requires the <c>Xunit.SkippableFact</c> package and <c>[SkippableFact]</c> attribute.
    /// </summary>
    public void SkipIfUnavailable() =>
        Skip.If(!IsAvailable, "Azure Cosmos DB Emulator is not running. Start it and re-run the tests.");

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool TryLaunchEmulator()
    {
        var exe = _candidatePaths.FirstOrDefault(File.Exists);
        if (exe is null)
        {
            Console.WriteLine("[CosmosFixture] Cosmos DB Emulator not found in any known location. Download from https://aka.ms/cosmosdb-emulator");
            return false;
        }

        Console.WriteLine($"[CosmosFixture] Launching emulator: {exe}");
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
        return true;
    }

    private static async Task<bool> WaitForReadyAsync(int port, TimeSpan timeout, CancellationToken ct = default)
    {
        Console.WriteLine($"[CosmosFixture] Waiting up to {timeout.TotalSeconds}s for emulator on port {port}...");

        // Build a handler that trusts the emulator's self-signed cert.
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
        {
            try
            {
                // The emulator returns 200 or 401 on the root path once it's ready.
                // Any proper HTTP response means the API is up.
                var response = await http.GetAsync($"https://localhost:{port}/", ct);
                Console.WriteLine($"[CosmosFixture] Emulator ready after {sw.Elapsed.TotalSeconds:F1}s (HTTP {(int)response.StatusCode}).");
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Not ready yet — keep polling.
            }

            try
            {
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Console.WriteLine($"[CosmosFixture] Emulator did not become ready within {timeout.TotalSeconds}s.");
        return false;
    }

    private static bool IsPortListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(ep => ep.Port == port);
        }
        catch { return false; }
    }
}