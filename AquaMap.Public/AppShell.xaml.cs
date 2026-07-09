using AquaMap.Public.Views;

namespace AquaMap.Public;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        Routing.RegisterRoute(nameof(ReservoirDetailPage), typeof(ReservoirDetailPage));
	}
}
