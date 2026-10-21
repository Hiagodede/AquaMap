using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class AnalysisFormPage : ContentPage
    {
        public AnalysisFormPage(AnalysisFormViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
