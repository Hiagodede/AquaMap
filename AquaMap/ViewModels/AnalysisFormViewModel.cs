using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using AquaMap.Domain.Entities;
using AquaMap.Services;
using AquaMap.Client.Shared;

namespace AquaMap.ViewModels
{
    /// <summary>
    /// Corrects the readings of an analysis already on the server (administrator only).
    /// Date, reservoir and collection point stay as recorded in the field.
    /// </summary>
    public class AnalysisFormViewModel : INotifyPropertyChanged, IQueryAttributable
    {
        private readonly ApiService _apiService;
        private readonly LocalDatabaseService _localDbService;

        private int _analysisId;

        private string _reservoirName = string.Empty;
        public string ReservoirName
        {
            get => _reservoirName;
            set { _reservoirName = value; OnPropertyChanged(); }
        }

        private string _analysisDateText = string.Empty;
        public string AnalysisDateText
        {
            get => _analysisDateText;
            set { _analysisDateText = value; OnPropertyChanged(); }
        }

        private string _residualChlorine = string.Empty;
        public string ResidualChlorine
        {
            get => _residualChlorine;
            set { _residualChlorine = value; OnPropertyChanged(); }
        }

        private string _ph = string.Empty;
        public string Ph
        {
            get => _ph;
            set { _ph = value; OnPropertyChanged(); }
        }

        private string _turbidity = string.Empty;
        public string Turbidity
        {
            get => _turbidity;
            set { _turbidity = value; OnPropertyChanged(); }
        }

        private string _iron = string.Empty;
        public string Iron
        {
            get => _iron;
            set { _iron = value; OnPropertyChanged(); }
        }

        private bool? _eColiAbsent;
        public bool IsEColiAbsentSelected
        {
            get => _eColiAbsent == true;
            set { if (value) { _eColiAbsent = true; OnPropertyChanged(); OnPropertyChanged(nameof(IsEColiPresentSelected)); } }
        }

        public bool IsEColiPresentSelected
        {
            get => _eColiAbsent == false;
            set { if (value) { _eColiAbsent = false; OnPropertyChanged(); OnPropertyChanged(nameof(IsEColiAbsentSelected)); } }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
        }
        public bool IsNotBusy => !IsBusy;

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatus)); }
        }

        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }
        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        public ICommand SaveCommand { get; }

        public AnalysisFormViewModel(ApiService apiService, LocalDatabaseService localDbService)
        {
            _apiService = apiService;
            _localDbService = localDbService;
            SaveCommand = new Command(async () => await SaveAsync());
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ReservoirName", out var name))
                ReservoirName = name as string ?? string.Empty;

            if (query.TryGetValue("Analysis", out var value) && value is WaterAnalysis analysis)
            {
                _analysisId = analysis.Id;
                AnalysisDateText = analysis.AnalysisDateLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
                // Same notation as the collection form placeholders ("Ex: 1.5"); the parser accepts ',' and '.'.
                ResidualChlorine = analysis.ResidualChlorine.ToString(CultureInfo.InvariantCulture);
                Ph = analysis.Ph.ToString(CultureInfo.InvariantCulture);
                Turbidity = analysis.Turbidity.ToString(CultureInfo.InvariantCulture);
                Iron = analysis.Iron.ToString(CultureInfo.InvariantCulture);
                _eColiAbsent = analysis.EColiAbsent;
                OnPropertyChanged(nameof(IsEColiAbsentSelected));
                OnPropertyChanged(nameof(IsEColiPresentSelected));
            }
        }

        private async Task SaveAsync()
        {
            if (IsBusy || _analysisId <= 0) return;

            var input = WaterAnalysisInput.Validate(ResidualChlorine, Ph, Turbidity, Iron, _eColiAbsent);
            if (!input.IsValid)
            {
                IsSuccess = false;
                StatusMessage = input.Error!;
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                // Out-of-standard readings are the alerts this system exists to record: confirm, never block.
                if (input.OutOfStandard.Count > 0 && !await Shell.Current.DisplayAlert(
                        "Parâmetros fora do padrão",
                        "Fora da Portaria 888:\n• " + string.Join("\n• ", input.OutOfStandard) +
                        "\n\nA análise ficará como alerta no reservatório. Os valores estão corretos?",
                        "Salvar", "Revisar"))
                    return;

                var token = await SecureStorage.Default.GetAsync("jwt_token");
                if (string.IsNullOrEmpty(token))
                {
                    IsSuccess = false;
                    StatusMessage = "Sessão expirada. Faça login novamente.";
                    return;
                }

                var reading = input.Analysis!;
                var result = await _apiService.UpdateWaterAnalysisAsync(_analysisId, reading, token);
                if (result.Success)
                {
                    await _localDbService.UpdateSyncedAnalysisReadingsAsync(_analysisId,
                        reading.ResidualChlorine, reading.Ph, reading.Turbidity, reading.Iron, reading.EColiAbsent);
                    IsSuccess = true;
                    StatusMessage = "Análise atualizada!";
                    try { Microsoft.Maui.Devices.HapticFeedback.Default.Perform(Microsoft.Maui.Devices.HapticFeedbackType.Click); } catch { }
                    await Task.Delay(1000);
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = result.StatusCode switch
                    {
                        400 => result.ErrorMessage ?? "Valores inválidos.",
                        401 => "Sessão expirada. Faça login novamente.",
                        403 => "Apenas administradores podem editar análises.",
                        404 => "Esta análise foi excluída por outra pessoa. Volte ao histórico.",
                        _ => "Erro ao salvar. Verifique a conexão e tente de novo."
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao editar análise: {ex}");
                IsSuccess = false;
                StatusMessage = "Não foi possível salvar a análise. Verifique a conexão e tente de novo.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
