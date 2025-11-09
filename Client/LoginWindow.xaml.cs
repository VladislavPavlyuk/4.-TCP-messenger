using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace Client
{
    public partial class LoginWindow : Window
    {
        public string? Username { get; private set; }
        public string? Server { get; private set; }
        public int Port { get; private set; }
        public TcpClient? TcpClient { get; private set; }
        public NetworkStream? Stream { get; private set; }
        public bool IsAuthenticated { get; private set; }
        
        // Method to clear connection references after transfer to MainWindow
        public void ClearConnectionReferences()
        {
            // Clear references so OnClosed won't try to close the connection
            // Connection is now owned by MainWindow
            TcpClient = null;
            Stream = null;
        }

        public LoginWindow()
        {
            InitializeComponent();
            UsernameTextBox.Focus();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Only close connection if:
            // 1. Not authenticated (connection not passed to MainWindow)
            // 2. Connection references still exist (not yet transferred via ClearConnectionReferences)
            // If IsAuthenticated is true OR references are null (cleared), connection is being transferred to MainWindow
            // DO NOT close connection if it's being transferred
            
            // Check if connection should be closed
            bool connectionExists = TcpClient != null && Stream != null;
            bool isTransferred = IsAuthenticated || !connectionExists; // If authenticated or refs cleared, it's transferred
            
            System.Diagnostics.Debug.WriteLine($"LoginWindow.OnClosed: IsAuthenticated={IsAuthenticated}, connectionExists={connectionExists}, isTransferred={isTransferred}");
            
            if (!isTransferred && connectionExists)
            {
                // Only close if we're absolutely sure connection is not being transferred
                System.Diagnostics.Debug.WriteLine("LoginWindow.OnClosed: Closing connection (not transferred)");
                try
                {
                    if (TcpClient.Connected)
                    {
                        Stream?.Close();
                        TcpClient?.Close();
                    }
                }
                catch
                {
                    // Ignore errors when closing connection
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("LoginWindow.OnClosed: NOT closing connection (transferred to MainWindow)");
            }
            // If authenticated OR references cleared (null), connection is kept open and passed to MainWindow
            // Do NOT close connection in these cases - it's owned by MainWindow now
            base.OnClosed(e);
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Authenticate(isLogin: true);
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Authenticate(isLogin: false);
        }

        private void Authenticate(bool isLogin)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text) || 
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                StatusTextBlock.Text = "Please enter username and password";
                return;
            }

            try
            {
                // Connect to server if not already connected
                if (TcpClient == null || !TcpClient.Connected)
                {
                    Server = ServerTextBox.Text;
                    Port = int.Parse(PortTextBox.Text);

                    TcpClient = new TcpClient();
                    TcpClient.Connect(Server, Port);
                    Stream = TcpClient.GetStream();
                }

                // Send authentication request
                string command = isLogin ? "LOGIN" : "REGISTER";
                string request = $"{command}|{UsernameTextBox.Text}|{PasswordBox.Password}";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                Stream!.Write(requestBytes, 0, requestBytes.Length);
                Stream.Flush(); // Ensure data is sent immediately

                // Read response
                byte[] responseBuffer = new byte[4096];
                int bytesRead = Stream.Read(responseBuffer, 0, responseBuffer.Length);
                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                if (response.StartsWith("OK"))
                {
                    string message = response.Contains("|") ? response.Split('|')[1] : "";
                    
                    if (isLogin)
                    {
                        // Login successful - keep connection open and transfer to main window
                        Username = UsernameTextBox.Text;
                        IsAuthenticated = true; // Set before closing to prevent connection closure
                        
                        // Verify connection is still open before closing window
                        if (TcpClient == null || Stream == null || !TcpClient.Connected)
                        {
                            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                            StatusTextBlock.Text = "Connection was lost. Please try again.";
                            return;
                        }
                        
                        // Set DialogResult and close window
                        // Connection will be transferred to MainWindow in App.xaml.cs
                        // OnClosed will NOT close connection because IsAuthenticated=true
                        DialogResult = true;
                        Close(); // This will trigger OnClosed, but IsAuthenticated=true prevents connection closure
                    }
                    else
                    {
                        // Registration successful - show message and clear password
                        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                        StatusTextBlock.Text = message;
                        PasswordBox.Clear();
                        // Keep connection open for potential login
                    }
                }
                else
                {
                    // Extract error message from server response
                    string error = response.Contains("|") ? response.Split('|')[1] : response;
                    StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    StatusTextBlock.Text = error;
                    
                    // Keep connection open for both registration and login errors
                    // Allow user to try again without reconnecting
                }
            }
            catch (SocketException socketEx)
            {
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                StatusTextBlock.Text = $"Connection error: {socketEx.Message}. Please check server settings and try again.";
                // Close connection only on network errors
                Stream?.Close();
                TcpClient?.Close();
                Stream = null;
                TcpClient = null;
            }
            catch (System.IO.IOException ioEx)
            {
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                StatusTextBlock.Text = $"Network error: {ioEx.Message}. Please try again.";
                // Close connection only on IO errors
                Stream?.Close();
                TcpClient?.Close();
                Stream = null;
                TcpClient = null;
            }
            catch (Exception ex)
            {
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                StatusTextBlock.Text = $"Unexpected error: {ex.Message}";
                // Close connection only on unexpected errors
                Stream?.Close();
                TcpClient?.Close();
                Stream = null;
                TcpClient = null;
            }
        }
    }
}

