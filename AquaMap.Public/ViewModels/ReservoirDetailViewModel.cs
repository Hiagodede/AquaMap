using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AquaMap.Domain.Entities;
using AquaMap.Client.Shared;

namespace AquaMap.Public.ViewModels
{
    /// <summary>One collection in the bulletin, with every parameter already put into words.</summary>
    public sealed class AnalysisItem
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public AnalysisItem(WaterAnalysis analysis)
        {
            Date = analysis.AnalysisDateLocal.ToString("'Coleta de' dd/MM/yyyy 'às' HH:mm", PtBr);
            IsPotable = analysis.IsPotable;
            StatusText = WaterQuality.StatusText(WaterQuality.Evaluate(analysis));
            Readings = WaterQuality.Readings(analysis);
        }

        public string Date { get; }
        public bool IsPotable { get; }
        public string StatusText { get; }
        public IReadOnlyList<ParameterReading> Readings { get; }
    }

    [QueryProperty(nameof(ReservoirId), "Id")]
    public class ReservoirDetailViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private int _reservoirId;
        public int ReservoirId
        {
            get => _reservoirId;
            set { _reservoirId = value; _ = LoadDataAsync(); }
        }

        private Reservoir? _reservoir;
        public Reservoir? Reservoir
        {
            get => _reservoir;
            set { _reservoir = value; OnPropertyChanged(); }
        }

        public ObservableCollection<AnalysisItem> Analyses { get; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(IsEmpty)); }
        }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // ── Current situation (QA INS-14): what the citizen needs to understand an alert on the map ──
        private WaterStatus _status = WaterStatus.NoData;
        public WaterStatus Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }
        public string StatusText => WaterQuality.StatusText(Status);
        public Color StatusColor => Status switch
        {
            WaterStatus.Potable => Color.FromArgb("#047857"),
            WaterStatus.Alert => Color.FromArgb("#B91C1C"),
            _ => Color.FromArgb("#B45309")
        };

        private string _statusDetail = string.Empty;
        public string StatusDetail
        {
            get => _statusDetail;
            private set { _statusDetail = value; OnPropertyChanged(); }
        }

        private bool _hasLoaded;
        public bool HasSituation => _hasLoaded && !HasError;
        public bool IsEmpty => _hasLoaded && !IsBusy && !HasError && Analyses.Count == 0;

        public ICommand ReloadCommand { get; }

        public ReservoirDetailViewModel(ApiService apiService)
        {
            _apiService = apiService;
            ReloadCommand = new Command(async () => await LoadDataAsync());
        }

        private async Task LoadDataAsync()
        {
            if (IsBusy || ReservoirId == 0) return;
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var reservoir = await _apiService.GetReservoirByIdAsync(ReservoirId);
                if (reservoir == null)
                {
                    ErrorMessage = "Não foi possível carregar os dados deste reservatório. Verifique sua conexão e tente novamente.";
                    return;
                }

                Reservoir = reservoir;
                var ordered = (reservoir.WaterAnalyses ?? Enumerable.Empty<WaterAnalysis>())
                    .OrderByDescending(a => a.AnalysisDate).ToList();

                Analyses.Clear();
                foreach (var a in ordered) Analyses.Add(new AnalysisItem(a));

                var latest = ordered.FirstOrDefault();
                Status = WaterQuality.Evaluate(latest);
                StatusDetail = Status switch
                {
                    WaterStatus.Alert => "Fora do padrão da Portaria 888 na última coleta:\n• "
                        + string.Join("\n• ", WaterQuality.OutOfStandard(latest!).Select(r => r.Summary))
                        + $"\n\nÚltima coleta: {WaterQuality.LastCollectionDate(latest)}",
                    WaterStatus.Potable => "Todos os parâmetros da última coleta estão dentro do padrão da Portaria 888."
                        + $"\n\nÚltima coleta: {WaterQuality.LastCollectionDate(latest)}",
                    _ => "Quando o SAAE registrar a primeira coleta, o resultado aparece aqui."
                };
                _hasLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar reservatório: {ex}");
                ErrorMessage = "Não foi possível carregar os dados deste reservatório. Verifique sua conexão e tente novamente.";
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(HasSituation));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
