using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using AquaMap.Models;

namespace AquaMap.Services
{
    public class LocalDatabaseService
    {
        private SQLiteAsyncConnection? _database;

        private async Task InitAsync()
        {
            if (_database != null)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "aquamap.db3");
            _database = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);

            await _database.CreateTableAsync<LocalReservoir>().ConfigureAwait(false);
            await _database.CreateTableAsync<LocalWaterAnalysis>().ConfigureAwait(false);
            await _database.CreateTableAsync<LocalUser>().ConfigureAwait(false);
        }

        // --- RESERVOIR OPERATIONS ---
        public async Task<List<LocalReservoir>> GetReservoirsAsync()
        {
            await InitAsync().ConfigureAwait(false);
            return await _database!.Table<LocalReservoir>().ToListAsync().ConfigureAwait(false);
        }

        public async Task SaveReservoirsAsync(List<LocalReservoir> reservoirs)
        {
            await InitAsync().ConfigureAwait(false);
            await _database!.DeleteAllAsync<LocalReservoir>().ConfigureAwait(false);
            if (reservoirs.Count > 0)
            {
                await _database.InsertAllAsync(reservoirs).ConfigureAwait(false);
            }
        }

        public async Task SaveReservoirAsync(LocalReservoir reservoir)
        {
            await InitAsync().ConfigureAwait(false);
            await _database!.InsertOrReplaceAsync(reservoir).ConfigureAwait(false);
        }

        public async Task DeleteReservoirAsync(int id)
        {
            await InitAsync().ConfigureAwait(false);
            await _database!.DeleteAsync<LocalReservoir>(id).ConfigureAwait(false);
        }

        // --- WATER ANALYSIS OPERATIONS ---
        public async Task<List<LocalWaterAnalysis>> GetAnalysisHistoryAsync(int reservoirId)
        {
            await InitAsync().ConfigureAwait(false);
            return await _database!.Table<LocalWaterAnalysis>()
                                    .Where(a => a.ReservoirId == reservoirId)
                                    .OrderByDescending(a => a.AnalysisDate)
                                    .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<LocalWaterAnalysis>> GetPendingSyncAnalysisAsync()
        {
            await InitAsync().ConfigureAwait(false);
            return await _database!.Table<LocalWaterAnalysis>()
                                    .Where(a => a.IsPendingSync == true)
                                    .ToListAsync().ConfigureAwait(false);
        }

        public async Task SaveAnalysisAsync(LocalWaterAnalysis analysis)
        {
            await InitAsync().ConfigureAwait(false);
            if (analysis.LocalId != 0)
            {
                await _database!.UpdateAsync(analysis).ConfigureAwait(false);
            }
            else
            {
                await _database!.InsertAsync(analysis).ConfigureAwait(false);
            }
        }

        public async Task SaveAnalysisHistoryAsync(int reservoirId, List<LocalWaterAnalysis> analysisList)
        {
            await InitAsync().ConfigureAwait(false);

            // Apaga do banco local apenas os registros deste reservatório que NÃO estão pendentes de sincronização
            await _database!.ExecuteAsync("DELETE FROM LocalWaterAnalysis WHERE ReservoirId = ? AND IsPendingSync = 0", reservoirId).ConfigureAwait(false);

            if (analysisList.Count > 0)
            {
                await _database.InsertAllAsync(analysisList).ConfigureAwait(false);
            }
        }

        // --- USER OPERATIONS ---
        public async Task<List<LocalUser>> GetUsersAsync()
        {
            await InitAsync().ConfigureAwait(false);
            return await _database!.Table<LocalUser>().ToListAsync().ConfigureAwait(false);
        }

        public async Task SaveUsersAsync(List<LocalUser> users)
        {
            await InitAsync().ConfigureAwait(false);
            await _database!.DeleteAllAsync<LocalUser>().ConfigureAwait(false);
            if (users.Count > 0)
            {
                await _database.InsertAllAsync(users).ConfigureAwait(false);
            }
        }

        public async Task SaveUserAsync(LocalUser user)
        {
            await InitAsync().ConfigureAwait(false);
            await _database!.InsertOrReplaceAsync(user).ConfigureAwait(false);
        }

        public async Task DeleteUserAsync(Guid id)
        {
            await InitAsync().ConfigureAwait(false);
            await _database!.DeleteAsync<LocalUser>(id).ConfigureAwait(false);
        }
    }
}
