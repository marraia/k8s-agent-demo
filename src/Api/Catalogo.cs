using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Api;

public sealed class PagamentosOptions
{
    public const string Secao = "Pagamentos";

    // A mensagem já diz como corrigir: quem lê o log não precisa abrir o código.
    [Required(ErrorMessage =
        "Pagamentos:ApiKey é obrigatória. No Kubernetes, defina a variável de ambiente " +
        "Pagamentos__ApiKey (com dois underlines).")]
    public string ApiKey { get; set; } = "";
}

public sealed class CatalogoOptions
{
    public const string Secao = "Catalogo";

    [Range(1, 4096)]
    public int TamanhoMb { get; set; } = 64;

    [Range(0, 600)]
    public int AquecimentoSegundos { get; set; } = 45;
}

public sealed record Produto(int Id, string Nome, decimal Preco);

/// <summary>
/// Simula um catálogo carregado em memória na inicialização:
/// ocupa <see cref="CatalogoOptions.TamanhoMb"/> MB e leva
/// <see cref="CatalogoOptions.AquecimentoSegundos"/> segundos para ficar pronto.
/// </summary>
public sealed class Catalogo
{
    private readonly List<byte[]> _blocos = [];
    private int _mbCarregados;
    private volatile bool _pronto;

    public bool Pronto => _pronto;
    public int MbCarregados => Volatile.Read(ref _mbCarregados);

    internal void Adicionar(byte[] bloco)
    {
        _blocos.Add(bloco);
        Interlocked.Increment(ref _mbCarregados);
    }

    internal void MarcarPronto() => _pronto = true;

    internal void Limpar()
    {
        _blocos.Clear();
        Volatile.Write(ref _mbCarregados, 0);
    }

    public Produto? Buscar(int id) =>
        _pronto && id is > 0 and <= 1000 ? new Produto(id, $"Produto {id}", 9.90m + id) : null;
}

public sealed class CarregadorDoCatalogo(
    Catalogo catalogo,
    IOptions<CatalogoOptions> opcoes,
    ILogger<CarregadorDoCatalogo> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = opcoes.Value;
        var intervalo = TimeSpan.FromSeconds((double)o.AquecimentoSegundos / o.TamanhoMb);
        logger.LogInformation("Carregando catálogo: {TamanhoMb} MB em {Segundos}s", o.TamanhoMb, o.AquecimentoSegundos);

        try
        {
            for (var mb = 1; mb <= o.TamanhoMb; mb++)
            {
                var bloco = new byte[1024 * 1024];
                Random.Shared.NextBytes(bloco); // escreve em todas as páginas: a memória é usada de verdade
                catalogo.Adicionar(bloco);

                if (mb % 16 == 0)
                    logger.LogInformation("Catálogo: {Carregados}/{Total} MB", mb, o.TamanhoMb);

                await Task.Delay(intervalo, stoppingToken);
            }
        }
        catch (OutOfMemoryException ex)
        {
            var carregados = catalogo.MbCarregados;
            catalogo.Limpar(); // libera memória para conseguir registrar o erro
            logger.LogCritical(ex,
                "Sem memória ao carregar o catálogo: {Carregados} de {Total} MB. Limite do GC: {LimiteMb} MB",
                carregados, o.TamanhoMb, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);
            Environment.ExitCode = 1;
            throw;
        }
        catch (Exception) when (!stoppingToken.IsCancellationRequested)
        {
            // Uma exceção num BackgroundService para o host, mas o código de saída
            // continuaria 0. Com 1, o Kubernetes registra a falha corretamente.
            Environment.ExitCode = 1;
            throw;
        }

        catalogo.MarcarPronto();
        logger.LogInformation("Catálogo pronto: {TamanhoMb} MB", o.TamanhoMb);
    }
}

public sealed class CatalogoHealthCheck(Catalogo catalogo) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(catalogo.Pronto
            ? HealthCheckResult.Healthy("Catálogo carregado")
            : HealthCheckResult.Unhealthy($"Catálogo carregando: {catalogo.MbCarregados} MB"));
}
