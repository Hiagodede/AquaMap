using AquaMap.ViewModels;

namespace AquaMap
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // O '?' resolve o aviso de nulidade.
            // Ele faz o mesmo que o seu 'if', mas em uma linha só.
            _viewModel.LoadPointsCommand?.Execute(null);
        }
    }
}