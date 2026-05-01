using System.Linq;
using System.Windows;

namespace GHelperAutoProfileSwitcher
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            MainWindow mainWindow = new MainWindow();
            
            if (!e.Args.Contains("-hidden"))
            {
                mainWindow.Show();
            }
        }
    }
}