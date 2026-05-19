namespace AquaMap
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("CollectionFormPage", typeof(Views.CollectionFormPage));
            Routing.RegisterRoute("ReservoirFormPage", typeof(Views.ReservoirFormPage));
            Routing.RegisterRoute("ReservoirDetailPage", typeof(Views.ReservoirDetailPage));
            Routing.RegisterRoute("UserListPage", typeof(Views.UserListPage));
            Routing.RegisterRoute("UserFormPage", typeof(Views.UserFormPage));
        }
    }
}
