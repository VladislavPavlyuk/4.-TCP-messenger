using System;
using System.IO;
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
        private string _server;
        private int _port;

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
            _server = server;
            _port = port;
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
            if (_isConnected)
            {
                // Disconnect
                Disconnect();
            }
            else
            {
                // Reconnect
                Connect();
            }
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

        private void Connect()
        {
            try
            {
                // Request password for reconnection
                var passwordDialog = new PasswordDialog();
                passwordDialog.Owner = this;
                if (passwordDialog.ShowDialog() != true)
                {
                    return; // User cancelled
                }

                string password = passwordDialog.Password;

                // Connect to server
                _tcpClient = new TcpClient();
                _tcpClient.Connect(_server, _port);
                _stream = _tcpClient.GetStream();

                // Send LOGIN request
                string request = $"LOGIN|{_username}|{password}";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                _stream.Write(requestBytes, 0, requestBytes.Length);
                _stream.Flush();

                // Read response
                byte[] responseBuffer = new byte[4096];
                int bytesRead = _stream.Read(responseBuffer, 0, responseBuffer.Length);
                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                if (response.StartsWith("OK"))
                {
                    _isConnected = true;
                    StatusTextBlock.Text = $"Connected as {_username}";
                    UpdateUI();
                    
                    // Reload users list
                    LoadUsersList();
                }
                else
                {
                    // Login failed - close connection
                    _stream.Close();
                    _tcpClient.Close();
                    _stream = null;
                    _tcpClient = null;
                    
                    string error = response.Contains("|") ? response.Split('|')[1] : response;
                    StatusTextBlock.Text = $"Reconnection failed: {error}";
                    MessageBox.Show($"Reconnection failed: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateUI();
                }
            }
            catch (SocketException socketEx)
            {
                _isConnected = false;
                StatusTextBlock.Text = $"Connection error: {socketEx.Message}";
                MessageBox.Show($"Connection error: {socketEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateUI();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                StatusTextBlock.Text = $"Reconnection error: {ex.Message}";
                MessageBox.Show($"Reconnection error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateUI();
            }
        }

        private void HandleConnectionLoss()
        {
            if (_isConnected)
            {
                _isConnected = false;
                StatusTextBlock.Text = "Connection lost";
                UpdateUI();
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
                    // Add new message to the end of history
                    HistoryListBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] To {ToUserTextBox.Text}: {MessageTextBox.Text}");
                    MessageTextBox.Clear();
                    
                    // Scroll to the last (newest) message in history
                    if (HistoryListBox.Items.Count > 0)
                    {
                        HistoryListBox.UpdateLayout();
                        var lastItem = HistoryListBox.Items[HistoryListBox.Items.Count - 1];
                        HistoryListBox.ScrollIntoView(lastItem);
                    }
                }
                else
                {
                    MessageBox.Show($"Send failed: {response}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (SocketException)
            {
                HandleConnectionLoss();
                MessageBox.Show("Connection lost. Please reconnect.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException)
            {
                HandleConnectionLoss();
                MessageBox.Show("Connection lost. Please reconnect.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        
                        // Scroll to the last message
                        if (MessagesListBox.Items.Count > 0)
                        {
                            MessagesListBox.UpdateLayout();
                            var lastItem = MessagesListBox.Items[MessagesListBox.Items.Count - 1];
                            MessagesListBox.ScrollIntoView(lastItem);
                        }
                    }
                }
                else
                {
                    MessageBox.Show($"Get messages failed: {response}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (SocketException)
            {
                HandleConnectionLoss();
                MessageBox.Show("Connection lost. Please reconnect.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException)
            {
                HandleConnectionLoss();
                MessageBox.Show("Connection lost. Please reconnect.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Refresh error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateUI()
        {
            ConnectButton.Content = _isConnected ? "Disconnect" : "Connect";
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
                // Send request with current username to exclude it from the list
                string request = $"GET_USERS|{_username}";
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
                    
                    System.Diagnostics.Debug.WriteLine($"Received users data: {usersData}");
                    
                    if (!string.IsNullOrEmpty(usersData))
                    {
                        string[] users = usersData.Split(new string[] { "||" }, StringSplitOptions.None);
                        foreach (string userData in users)
                        {
                            if (!string.IsNullOrWhiteSpace(userData))
                            {
                                string[] parts = userData.Split('|');
                                System.Diagnostics.Debug.WriteLine($"Parsing userData: '{userData}', parts.Length: {parts.Length}");
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  parts[{i}]: '{parts[i]}'");
                                }
                                
                                if (parts.Length >= 2)
                                {
                                    string username = parts[0].Trim();
                                    string statusStr = parts[1].Trim();
                                    bool isOnline = statusStr == "1";
                                    System.Diagnostics.Debug.WriteLine($"User: '{username}', Status: '{statusStr}', IsOnline: {isOnline}");
                                    UsersListBox.Items.Add(new UserInfo(username, isOnline));
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Invalid user data format: {userData}, parts.Length: {parts.Length}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Get users failed: {response}");
                }
            }
            catch (SocketException)
            {
                HandleConnectionLoss();
            }
            catch (System.IO.IOException)
            {
                HandleConnectionLoss();
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

