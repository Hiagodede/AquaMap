using System.Security.Principal;

namespace AquaMap;

public partial class App : Application
{
    // Recebe AppShell via DI
    public App(AppShell shell)
    {
        InitializeComponent();
        MainPage = shell;
    }

    // Mant�m override para compatibilidade com platforms que chamam CreateWindow
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(MainPage);
    }
}