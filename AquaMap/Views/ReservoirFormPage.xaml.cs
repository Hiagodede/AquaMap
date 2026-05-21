using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class ReservoirFormPage : ContentPage
    {
        public ReservoirFormPage(ReservoirFormViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is ReservoirFormViewModel viewModel)
            {
                viewModel.InitializeForm();
            }
        }
    }
}
