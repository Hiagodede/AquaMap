using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class UserFormPage : ContentPage
    {
        public UserFormPage(UserFormViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
