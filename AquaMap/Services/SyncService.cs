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

        public async Task<SyncReport> SyncPendingAnalysisAsync()
        {
            var rejected = new List<string>();
            if (Connectivity.NetworkAccess != NetworkAccess.Internet) return new SyncReport(rejected, false);
            if (!await _syncLock.WaitAsync(0).ConfigureAwait(false)) return new SyncReport(rejected, false);

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
                    if (result.Success)
                    {
                        localAnalysis.IsPendingSync = false;
                        await _localDbService.SaveAnalysisAsync(localAnalysis).ConfigureAwait(false);
                        Debug.WriteLine($"[SyncService] Análise local {localAnalysis.LocalId} enviada com sucesso!");
                    }
                    else if (result.StatusCode is 401 or 403)
                    {
                        // Token expired or revoked: keep everything queued until the technician logs in again.
                        Debug.WriteLine("[SyncService] Sessão expirada; coletas continuam pendentes.");
                        return new SyncReport(rejected, true);
                    }
                    else if (result.StatusCode is 400 or 422)
                    {
                        // The server will never accept it: retrying forever blocked the queue and made every
                        // later success message say "sincronização em andamento". Drop it and tell the technician.
                        await _localDbService.DeleteAnalysisAsync(localAnalysis).ConfigureAwait(false);
                        rejected.Add($"{localAnalysis.ToDomain().AnalysisDateLocal:dd/MM HH:mm}: {result.ErrorMessage ?? "dados inválidos"}");
                        Debug.WriteLine($"[SyncService] Análise local {localAnalysis.LocalId} recusada pelo servidor: {result.ErrorMessage}");
                    }
                    else
                    {
                        // Network failure or server error: stays pending for the next attempt.
                        Debug.WriteLine($"[SyncService] Falha transitória ao enviar análise local {localAnalysis.LocalId} (HTTP {result.StatusCode}).");
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
    }
}
