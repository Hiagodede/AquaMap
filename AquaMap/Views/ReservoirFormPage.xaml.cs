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
    }
}
