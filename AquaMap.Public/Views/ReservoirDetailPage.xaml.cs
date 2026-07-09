using AquaMap.Public.ViewModels;

namespace AquaMap.Public.Views
{
    public partial class ReservoirDetailPage : ContentPage
    {
        public ReservoirDetailPage(ReservoirDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
