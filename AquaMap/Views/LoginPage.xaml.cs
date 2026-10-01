using AquaMap.ViewModels;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;
using System.Linq;

namespace AquaMap.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage(LoginViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Verifica se já tem token salvo. Se sim, pula direto pro formulário.
            try
            {
                var token = await SecureStorage.Default.GetAsync("jwt_token");
                if (!string.IsNullOrEmpty(token))
                {
                    await Shell.Current.GoToAsync("CollectionFormPage");
                }
            }
            catch (Exception ex)
            {
                // Keystore corrompido (ex.: restauração de backup) — descarta o token e segue no login.
                System.Diagnostics.Debug.WriteLine($"Erro ao ler token salvo: {ex}");
                try { SecureStorage.Default.Remove("jwt_token"); } catch { }
            }
        }
    }
}
