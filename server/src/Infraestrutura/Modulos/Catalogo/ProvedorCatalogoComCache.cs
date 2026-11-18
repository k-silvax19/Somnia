using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Infraestrutura.Modulos.Catalogo;

/// <summary>
/// Reuses recent Spotify catalog responses across users and coalesces concurrent
/// requests for the same catalog key. Only successful catalog results are cached.
/// </summary>
public sealed class ProvedorCatalogoComCache : IMusicCatalogProvider, IDisposable
{
    private const int MaxEntries = 128;
    private static readonly TimeSpan SearchLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ItemLifetime = TimeSpan.FromMinutes(30);

    private readonly IMusicCatalogProvider inner;
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> inFlight = new(StringComparer.Ordinal);

    public ProvedorCatalogoComCache(IMusicCatalogProvider inner) => this.inner = inner;

    public Task<PaginaCatalogoDto> PesquisarAsync(
        string texto, TipoItemCatalogo tipo, int limit, int offset, CancellationToken ct)
    {
        var chave = $"search:{tipo}:{limit}:{offset}:{texto}";
        return ConsultarAsync(chave, SearchLifetime,
            token => inner.PesquisarAsync(texto, tipo, limit, offset, token), ct);
    }

    public Task<AlbumCatalogoDto?> ObterAlbumAsync(string externalId, CancellationToken ct) =>
        ConsultarAsync($"album:{externalId}", ItemLifetime,
            token => inner.ObterAlbumAsync(externalId, token), ct);

    public Task<FaixaCatalogoDto?> ObterFaixaAsync(string externalId, CancellationToken ct) =>
        ConsultarAsync($"track:{externalId}", ItemLifetime,
            token => inner.ObterFaixaAsync(externalId, token), ct);

    private async Task<T?> ConsultarAsync<T>(
        string chave, TimeSpan duracao, Func<CancellationToken, Task<T?>> carregar, CancellationToken ct)
        where T : class
    {
        ct.ThrowIfCancellationRequested();
        if (cache.TryGetValue(chave, out T? valor)) return valor;

        var tarefa = inFlight.GetOrAdd(chave, _ => CriarTarefa(chave, duracao, carregar)).Value;
        _ = tarefa.ContinueWith(
            concluida => { _ = concluida.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return (T?)await tarefa.WaitAsync(ct);
    }

    private Lazy<Task<object?>> CriarTarefa<T>(
        string chave, TimeSpan duracao, Func<CancellationToken, Task<T?>> carregar)
        where T : class
    {
        Lazy<Task<object?>>? entrada = null;
        entrada = new Lazy<Task<object?>>(async () =>
        {
            try
            {
                var valor = await carregar(CancellationToken.None);
                if (valor is not null)
                    cache.Set(chave, valor, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = duracao,
                        Size = 1
                    });
                return valor;
            }
            finally
            {
                ((ICollection<KeyValuePair<string, Lazy<Task<object?>>>>)inFlight)
                    .Remove(new KeyValuePair<string, Lazy<Task<object?>>>(chave, entrada!));
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        return entrada;
    }

    public void Dispose() => cache.Dispose();
}
