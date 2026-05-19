using AquaMap.ViewModels;

namespace AquaMap.Views
{
    public partial class UserListPage : ContentPage
    {
        private readonly UserListViewModel _viewModel;

        public UserListPage(UserListViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = _viewModel.LoadUsersAsync();
        }
    }
}
