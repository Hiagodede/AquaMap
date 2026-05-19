using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class ReservoirDetailPage : ContentPage
    {
        private readonly ReservoirDetailViewModel _viewModel;

        public ReservoirDetailPage(ReservoirDetailViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.LoadHistoryCommand?.Execute(null);
        }
    }
}
