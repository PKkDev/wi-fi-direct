using Microsoft.UI.Xaml;
using WinUIEx;

namespace WiFiDirect.Hub
{
    public partial class App : Application
    {
        private Window m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            m_window.SetIsResizable(false);
            m_window.SetWindowSize(500, 400);

            m_window.Activate();
        }
    }
}
