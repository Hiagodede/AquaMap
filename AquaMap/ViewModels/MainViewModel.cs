using AquaMap.Domain.Entities;
using AquaMap.Domain.Interfaces;
using System;
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

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(HasData)); }
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        public bool IsEmpty => !IsBusy && Points.Count == 0;
        public bool HasData => !IsBusy && Points.Count > 0;

        public ICommand LoadPointsCommand { get; }
        public ICommand OpenDetailCommand { get; }
        public ICommand AddReservoirCommand { get; }

        public MainViewModel(Services.ApiService apiService)
        {
            _apiService = apiService;

            // Configura o comando para carregar os dados
            LoadPointsCommand = new Command(async () => await LoadDataAsync());
            OpenDetailCommand = new Command<Reservoir>(async (r) =>
            {
                if (r != null)
                    await Shell.Current.GoToAsync($"ReservoirDetailPage?ReservoirId={r.Id}&ReservoirName={Uri.EscapeDataString(r.Name)}");
            });
            AddReservoirCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync("ReservoirFormPage");
            });
        }

        private async Task LoadDataAsync()
        {
            if (IsBusy) return;

            IsBusy = true;

            try
            {
                var data = await _apiService.GetReservoirsAsync();

                Points.Clear();
                foreach (var point in data)
                {
                    Points.Add(point);
                }
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasData));
            }
        }

        // Boilerplate do MVVM (padrão para avisar a tela que algo mudou)
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}