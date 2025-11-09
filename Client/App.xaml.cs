using System.Net.Sockets;
using System.Windows;

namespace Client
{
    public partial class App : Application
    {
        // Temporary storage for connection during window transition
        private static TcpClient? _pendingTcpClient;
        private static NetworkStream? _pendingStream;
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true && loginWindow.IsAuthenticated)
            {
                // Verify connection is still open before transferring
                if (loginWindow.TcpClient == null || loginWindow.Stream == null)
                {
                    MessageBox.Show("Connection was lost during authentication", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                
                if (!loginWindow.TcpClient.Connected)
                {
                    MessageBox.Show("Connection was closed during authentication", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
                
                // Save connection references in static variables to prevent GC/finalization
                // This ensures connection stays alive during window transition
                _pendingTcpClient = loginWindow.TcpClient;
                _pendingStream = loginWindow.Stream;
                var username = loginWindow.Username!;
                var server = loginWindow.Server!;
                var port = loginWindow.Port;
                
                // Clear references in LoginWindow IMMEDIATELY to prevent any cleanup
                // Connection is now stored in static variables, safe from GC
                loginWindow.ClearConnectionReferences();
                
                // Now create MainWindow with the saved connection references
                // Connection is stored in static variables, so it won't be closed
                var mainWindow = new MainWindow(
                    _pendingTcpClient,
                    _pendingStream,
                    username,
                    server,
                    port
                );
                
                // Clear static references - MainWindow now owns the connection
                _pendingTcpClient = null;
                _pendingStream = null;
                
                // Set MainWindow as the main window to prevent application shutdown
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}

