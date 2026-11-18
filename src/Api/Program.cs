using Api;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Logs em JSON: uma linha por evento, fácil de ler com `kubectl logs`
// (para quem está de plantão e para o agente).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

// Falha rápida: sem a configuração obrigatória, a aplicação nem sobe
// e o motivo aparece claramente no log.
builder.Services.AddOptions<PagamentosOptions>()
    .BindConfiguration(PagamentosOptions.Secao)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CatalogoOptions>()
    .BindConfiguration(CatalogoOptions.Secao)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Tempo para terminar requisições em andamento após o SIGTERM do Kubernetes.
// Precisa ser menor que o terminationGracePeriodSeconds do pod (30s).
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(20));

builder.Services.AddSingleton<Catalogo>();
builder.Services.AddHostedService<CarregadorDoCatalogo>();

// Liveness responde só se o processo está vivo; readiness, se pode receber tráfego.
builder.Services.AddHealthChecks()
    .AddCheck("processo", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<CatalogoHealthCheck>("catalogo", tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/live", new() { Predicate = c => c.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });

app.MapGet("/", (Catalogo catalogo) => Results.Ok(new
{
    servico = "demo-api",
    versao = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev",
    catalogoPronto = catalogo.Pronto
}));

app.MapGet("/produtos/{id:int}", (int id, Catalogo catalogo) =>
    catalogo.Buscar(id) is { } produto ? Results.Ok(produto) : Results.NotFound());

// O .NET lê o limite de memória do container (cgroup). Registrar isso no início
// ajuda a entender um OutOfMemoryException ou um OOMKilled depois.
var memoriaGcMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024;
app.Logger.LogInformation(
    "Iniciando demo-api {Versao}. Memória disponível para o GC: {MemoriaMb} MB",
    Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev", memoriaGcMb);

try
{
    await app.RunAsync();
    return Environment.ExitCode;
}
catch (Exception ex)
{
    // Inclui falhas do ValidateOnStart: o motivo vai para o log em JSON
    // e o processo sai com código diferente de zero.
    app.Logger.LogCritical(ex, "Falha fatal: {Motivo}", ex.Message);
    await app.DisposeAsync(); // esvazia a fila do logger antes de o processo sair
    return 1;
}
