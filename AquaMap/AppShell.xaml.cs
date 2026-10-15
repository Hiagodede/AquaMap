using System.Diagnostics;

namespace AquaMap
{
    public partial class AppShell : Shell
    {
        // A "back" (pop) requested while another navigation is still running can leave the Shell content
        // blank or black on Android until the app restarts (QA INS-07: two quick taps on back, or back right
        // after saving). Such pops are ignored. The guard expires so it can never block navigation for good.
        // Note: a programmatic GoToAsync("..") issued within 1 s of another navigation is ignored the same way.
        private static readonly TimeSpan NavigationGuardWindow = TimeSpan.FromSeconds(1);
        private readonly Stopwatch _sinceNavigationStarted = new();
        private bool _isNavigating;

        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("CollectionFormPage", typeof(Views.CollectionFormPage));
            Routing.RegisterRoute("ReservoirFormPage", typeof(Views.ReservoirFormPage));
            Routing.RegisterRoute("ReservoirDetailPage", typeof(Views.ReservoirDetailPage));
            Routing.RegisterRoute("UserListPage", typeof(Views.UserListPage));
            Routing.RegisterRoute("UserFormPage", typeof(Views.UserFormPage));
        }

        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            base.OnNavigating(args);
            if (args.Cancelled) return;

            bool isPop = args.Source is ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot;
            if (isPop && args.CanCancel && _isNavigating && _sinceNavigationStarted.Elapsed < NavigationGuardWindow)
            {
                args.Cancel();
                return;
            }

            _isNavigating = true;
            _sinceNavigationStarted.Restart();
        }

        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);
            _isNavigating = false;
        }
    }
}
