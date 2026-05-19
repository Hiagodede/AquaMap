using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class CollectionFormPage : ContentPage
    {
        private readonly CollectionFormViewModel _viewModel;

        public CollectionFormPage(CollectionFormViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.LoadReservoirsCommand.Execute(null);
        }
    }
}
