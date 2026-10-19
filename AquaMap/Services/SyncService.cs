using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using AquaMap.Models;
using AquaMap.Domain.Entities;
using AquaMap.Client.Shared;

namespace AquaMap.Services
{
    /// <summary>Outcome of one synchronization pass.</summary>
    /// <param name="Rejected">Collections the server refused as invalid (removed from the queue), as text for the technician.</param>
    /// <param name="SessionExpired">The server answered 401/403: pending collections stay queued until a new login.</param>
    public sealed record SyncReport(IReadOnlyList<string> Rejected, bool SessionExpired);

    public class SyncService
    {
        private readonly ApiService _apiService;
        private readonly LocalDatabaseService _localDbService;
        // Startup, connectivity changes and the collection form can all trigger a sync at once.
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private static readonly TimeSpan InteractiveLockWait = TimeSpan.FromSeconds(30);

        public SyncService(ApiService apiService, LocalDatabaseService localDbService)
        {
            _apiService = apiService;
            _localDbService = localDbService;
        }

        public void StartMonitoring()
        {
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                _ = Task.Run(async () => await SyncPendingAnalysisAsync().ConfigureAwait(false));
            }
        }

        public void StopMonitoring()
        {
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        }

        private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            try
            {
                if (e.NetworkAccess == NetworkAccess.Internet)
                {
                    await SyncPendingAnalysisAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncService] Erro na sincronização ao reconectar: {ex}");
            }
        }

        /// <param name="interactive">
        /// True when called from the collection form, which shows the report to the technician: it waits for a
        /// running pass (so the collection just saved is included) and discards server-rejected collections.
        /// Background passes (startup, network back) skip if busy and keep rejected collections queued,
        /// because nobody would see the warning and a field measurement must never vanish silently.
        /// </param>
        public async Task<SyncReport> SyncPendingAnalysisAsync(bool interactive = false)
        {
            var rejected = new List<string>();
            if (Connectivity.NetworkAccess != NetworkAccess.Internet) return new SyncReport(rejected, false);
            // A background pass stuck on a slow API (100 s HTTP timeout per collection) must not keep the form
            // busy for minutes: after the wait the collection stays saved on the device and is retried later.
            var wait = interactive ? InteractiveLockWait : TimeSpan.Zero;
            if (!await _syncLock.WaitAsync(wait).ConfigureAwait(false))
                return new SyncReport(rejected, false);

            try
            {
                var token = await SecureStorage.Default.GetAsync("jwt_token").ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SyncService] Nenhum token JWT encontrado para sincronização.");
                    return new SyncReport(rejected, true);
                }

                var pendingList = await _localDbService.GetPendingSyncAnalysisAsync().ConfigureAwait(false);
                if (pendingList == null || pendingList.Count == 0)
                {
                    return new SyncReport(rejected, false);
                }

                Debug.WriteLine($"[SyncService] Iniciando sincronização de {pendingList.Count} análises pendentes...");

                foreach (var localAnalysis in pendingList)
                {
                    var domainAnalysis = localAnalysis.ToDomain();
                    domainAnalysis.Id = 0; // the server assigns the id
                    domainAnalysis.IsPendingSync = false;

                    var result = await _apiService.SubmitWaterAnalysisAsync(domainAnalysis, token).ConfigureAwait(false);
                    switch (result.Outcome)
                    {
                        case SubmitOutcome.Accepted:
                            localAnalysis.IsPendingSync = false;
                            await _localDbService.SaveAnalysisAsync(localAnalysis).ConfigureAwait(false);
                            Debug.WriteLine($"[SyncService] Análise local {localAnalysis.LocalId} enviada com sucesso!");
                            break;

                        case SubmitOutcome.SessionExpired:
                            Debug.WriteLine("[SyncService] Sessão expirada; coletas continuam pendentes.");
                            return new SyncReport(rejected, true);

                        case SubmitOutcome.Rejected when interactive:
                            // Retrying forever never succeeds and made every later success message lie. Discard it,
                            // but only here, where the technician sees the reservoir and values to record it again.
                            await _localDbService.DeleteAnalysisAsync(localAnalysis).ConfigureAwait(false);
                            rejected.Add(await DescribeRejectedAsync(localAnalysis, result.ErrorMessage).ConfigureAwait(false));
                            Debug.WriteLine($"[SyncService] Análise local {localAnalysis.LocalId} recusada e descartada: {result.ErrorMessage}");
                            break;

                        default:
                            // Rejected in a background pass, or a transient failure: stays queued.
                            Debug.WriteLine($"[SyncService] Análise local {localAnalysis.LocalId} continua pendente ({result.Outcome}, HTTP {result.StatusCode}).");
                            break;
                    }
                }

                return new SyncReport(rejected, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncService] Erro na sincronização: {ex}");
                return new SyncReport(rejected, false);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        // Everything the technician needs to record the collection again once it is discarded.
        private async Task<string> DescribeRejectedAsync(LocalWaterAnalysis a, string? reason)
        {
            string reservoir = $"reservatório #{a.ReservoirId}";
            try
            {
                var cached = await _localDbService.GetReservoirsAsync().ConfigureAwait(false);
                var name = cached?.FirstOrDefault(r => r.Id == a.ReservoirId)?.Name;
                if (!string.IsNullOrWhiteSpace(name)) reservoir = name.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncService] Erro ao buscar nome do reservatório: {ex}");
            }

            var pt = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
            return string.Format(pt,
                "{0} em {1:dd/MM HH:mm} (cloro {2}, pH {3}, turbidez {4}, ferro {5}, E. coli {6}): {7}",
                reservoir, a.ToDomain().AnalysisDateLocal, a.ResidualChlorine, a.Ph, a.Turbidity, a.Iron,
                a.EColiAbsent ? "ausente" : "presente", reason ?? "dados inválidos");
        }
    }
}
