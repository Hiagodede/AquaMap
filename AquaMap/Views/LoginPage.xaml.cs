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
            string? token;
            try
            {
                token = await SecureStorage.Default.GetAsync("jwt_token");
            }
            catch (Exception ex)
            {
                // Keystore corrompido (ex.: restauração de backup) — descarta o token e segue no login.
                System.Diagnostics.Debug.WriteLine($"Erro ao ler token salvo: {ex}");
                try { SecureStorage.Default.Remove("jwt_token"); } catch { }
                return;
            }

            if (string.IsNullOrEmpty(token) || _isRedirecting) return;

            _isRedirecting = true;
            try
            {
                // OnAppearing also fires when this page is revealed by a pop (e.g. re-tapping the
                // Técnico tab). Pushing during that transition can leave a black screen on Android,
                // so wait for it to finish and only redirect if this page is still the visible one.
                await Task.Delay(300);
                if (Shell.Current?.CurrentPage != this) return;

                await Shell.Current.GoToAsync("CollectionFormPage");
            }
            catch (Exception ex)
            {
                // Falha de navegação não invalida a sessão: mantém o token e o usuário fica no login.
                System.Diagnostics.Debug.WriteLine($"Erro ao navegar para o formulário: {ex}");
            }
            finally
            {
                _isRedirecting = false;
            }
        }

        private bool _isRedirecting;
    }
}
