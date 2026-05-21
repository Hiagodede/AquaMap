namespace AquaMap
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly Services.SyncService _syncService;

        public App(Services.SyncService syncService)
        {
            InitializeComponent();
            _syncService = syncService;
            _syncService.StartMonitoring();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}