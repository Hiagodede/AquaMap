using AquaMap.Domain.Entities;
using AquaMap.Domain.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading.Tasks;

namespace AquaMap.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Services.ApiService _apiService;

        // Lista que a tela vai "vigiar" para mostrar os itens
        public ObservableCollection<Reservoir> Points { get; set; } = new();

        public ICommand LoadPointsCommand { get; }

        public MainViewModel(Services.ApiService apiService)
        {
            _apiService = apiService;

            // Configura o comando para carregar os dados
            LoadPointsCommand = new Command(async () => await LoadDataAsync());
        }

        private async Task LoadDataAsync()
        {
            var data = await _apiService.GetReservoirsAsync();

            Points.Clear();
            foreach (var point in data)
            {
                Points.Add(point);
            }
        }

        // Boilerplate do MVVM (padrão para avisar a tela que algo mudou)
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}