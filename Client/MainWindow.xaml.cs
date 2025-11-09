using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private bool _isConnected = false;
        private string _username;

        public MainWindow(TcpClient tcpClient, NetworkStream stream, string username, string server, int port)
        {
            InitializeComponent();
            
            // Verify connection is valid and open
            if (tcpClient == null || stream == null)
            {
                throw new ArgumentNullException("Connection objects cannot be null");
            }
            
            if (!tcpClient.Connected)
            {
                throw new InvalidOperationException("TcpClient is not connected");
            }
            
            // Transfer connection from LoginWindow - do not close it
            _tcpClient = tcpClient;
            _stream = stream;
            _username = username;
            _isConnected = true;
            
            System.Diagnostics.Debug.WriteLine($"MainWindow created: TcpClient.Connected={tcpClient.Connected}, Stream={stream != null}");
            
            UsernameTextBox.Text = username;
            ServerTextBox.Text = server;
            PortTextBox.Text = port.ToString();
            UsernameTextBox.IsEnabled = false;
            ServerTextBox.IsEnabled = false;
            PortTextBox.IsEnabled = false;
            
            StatusTextBlock.Text = $"Connected as {username}";
            UpdateUI();
            
            // Connection is transferred from LoginWindow and should remain open
            // Connection will be used for messaging operations
            
            // Load list of registered users
            LoadUsersList();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
                _isConnected = false;
                StatusTextBlock.Text = "Disconnected";
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Disconnect error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected || _stream == null)
            {
                MessageBox.Show("Not connected to server", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(ToUserTextBox.Text) || string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                MessageBox.Show("Please enter recipient and message", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string request = $"SEND|{_username}|{ToUserTextBox.Text}|{MessageTextBox.Text}";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                _stream.Write(requestBytes, 0, requestBytes.Length);

                byte[] responseBuffer = new byte[4096];
                int bytesRead = _stream.Read(responseBuffer, 0, responseBuffer.Length);
                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                if (response.StartsWith("OK"))
                {
                    HistoryListBox.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] To {ToUserTextBox.Text}: {MessageTextBox.Text}");
                    MessageTextBox.Clear();
                }
                else
                {
                    MessageBox.Show($"Send failed: {response}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Send error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected || _stream == null)
            {
                MessageBox.Show("Not connected to server", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string request = $"GET|{_username}";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                _stream.Write(requestBytes, 0, requestBytes.Length);

                byte[] responseBuffer = new byte[4096];
                int bytesRead = _stream.Read(responseBuffer, 0, responseBuffer.Length);
                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                if (response.StartsWith("OK"))
                {
                    MessagesListBox.Items.Clear();
                    string messagesData = response.Substring(3); // Remove "OK|"
                    
                    if (!string.IsNullOrEmpty(messagesData))
                    {
                        string[] messages = messagesData.Split(new string[] { "||" }, StringSplitOptions.None);
                        foreach (string msg in messages)
                        {
                            string[] parts = msg.Split('|');
                            if (parts.Length >= 3)
                            {
                                string fromUser = parts[0];
                                string message = parts[1];
                                string timestamp = parts[2];
                                MessagesListBox.Items.Add($"[{timestamp}] From {fromUser}: {message}");
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show($"Get messages failed: {response}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Refresh error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUI()
        {
            ConnectButton.Content = "Disconnect";
            SendButton.IsEnabled = _isConnected;
            RefreshButton.IsEnabled = _isConnected;
            RefreshUsersButton.IsEnabled = _isConnected;
        }

        private void RefreshUsersButton_Click(object sender, RoutedEventArgs e)
        {
            LoadUsersList();
        }

        private void LoadUsersList()
        {
            if (!_isConnected || _stream == null)
            {
                return;
            }

            try
            {
                string request = "GET_USERS";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                _stream.Write(requestBytes, 0, requestBytes.Length);
                _stream.Flush();

                byte[] responseBuffer = new byte[4096];
                int bytesRead = _stream.Read(responseBuffer, 0, responseBuffer.Length);
                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                if (response.StartsWith("OK"))
                {
                    UsersListBox.Items.Clear();
                    string usersData = response.Substring(3); // Remove "OK|"
                    
                    if (!string.IsNullOrEmpty(usersData))
                    {
                        string[] users = usersData.Split(new string[] { "||" }, StringSplitOptions.None);
                        foreach (string user in users)
                        {
                            if (!string.IsNullOrWhiteSpace(user))
                            {
                                UsersListBox.Items.Add(user);
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Get users failed: {response}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load users error: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("MainWindow.OnClosed: Closing connection and shutting down");
            Disconnect();
            base.OnClosed(e);
            // Shutdown application when MainWindow closes
            Application.Current.Shutdown();
        }
    }
}

