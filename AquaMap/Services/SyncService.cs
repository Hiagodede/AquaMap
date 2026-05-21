using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using AquaMap.Models;
using AquaMap.Domain.Entities;

namespace AquaMap.Services
{
    public class SyncService
    {
        private readonly ApiService _apiService;
        private readonly LocalDatabaseService _localDbService;
        private bool _isSyncing = false;

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
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                await SyncPendingAnalysisAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> SyncPendingAnalysisAsync()
        {
            if (_isSyncing) return false;
            if (Connectivity.NetworkAccess != NetworkAccess.Internet) return false;

            _isSyncing = true;
            try
            {
                var token = await SecureStorage.Default.GetAsync("jwt_token").ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SyncService] Nenhum token JWT encontrado para sincronização.");
                    return false;
                }

                var pendingList = await _localDbService.GetPendingSyncAnalysisAsync().ConfigureAwait(false);
                if (pendingList == null || pendingList.Count == 0)
                {
                    return true;
                }

                Debug.WriteLine($"[SyncService] Iniciando sincronização de {pendingList.Count} análises pendentes...");

                bool allSuccess = true;
                foreach (var localAnalysis in pendingList)
                {
                    var domainAnalysis = new WaterAnalysis
                    {
                        ResidualChlorine = localAnalysis.ResidualChlorine,
                        Ph = localAnalysis.Ph,
                        Turbidity = localAnalysis.Turbidity,
                        EColiAbsent = localAnalysis.EColiAbsent,
                        ReservoirId = localAnalysis.ReservoirId,
                        AnalysisDate = localAnalysis.AnalysisDate
                    };

                    bool success = await _apiService.SubmitWaterAnalysisAsync(domainAnalysis, token).ConfigureAwait(false);
                    if (success)
                    {
                        localAnalysis.IsPendingSync = false;
                        await _localDbService.SaveAnalysisAsync(localAnalysis).ConfigureAwait(false);
                        Debug.WriteLine($"[SyncService] Análise local {localAnalysis.LocalId} enviada com sucesso!");
                    }
                    else
                    {
                        allSuccess = false;
                        Debug.WriteLine($"[SyncService] Falha ao enviar análise local {localAnalysis.LocalId}.");
                    }
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncService] Erro na sincronização: {ex.Message}");
                return false;
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }
}
