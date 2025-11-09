using System;
using System.Collections.Generic;
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
        private string? _selectedUser;

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
                    MessageTextBox.Clear();
                    
                    // Reload messages with selected user if one is selected
                    if (!string.IsNullOrEmpty(_selectedUser))
                    {
                        LoadMessagesWithUser(_selectedUser);
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

        private void LoadMessagesWithUser(string otherUser)
        {
            if (!_isConnected || _stream == null || string.IsNullOrEmpty(otherUser))
            {
                return;
            }

            try
            {
                // Get all messages for current user
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
                        var messageList = new List<(DateTime timestamp, string fromUser, string toUser, string message)>();
                        
                        string[] messages = messagesData.Split(new string[] { "||" }, StringSplitOptions.None);
                        foreach (string msg in messages)
                        {
                            string[] parts = msg.Split('|');
                            if (parts.Length >= 4)
                            {
                                string fromUser = parts[0];
                                string message = parts[1];
                                string timestampStr = parts[2];
                                string toUser = parts[3];
                                
                                // Parse timestamp
                                if (DateTime.TryParse(timestampStr, out DateTime timestamp))
                                {
                                    // Only show messages where the other party is the selected user
                                    if ((fromUser == _username && toUser == otherUser) ||
                                        (fromUser == otherUser && toUser == _username))
                                    {
                                        messageList.Add((timestamp, fromUser, toUser, message));
                                    }
                                }
                            }
                        }
                        
                        // Sort by timestamp
                        messageList.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
                        
                        // Display messages
                        foreach (var msg in messageList)
                        {
                            if (msg.fromUser == _username)
                            {
                                // Outgoing message
                                MessagesListBox.Items.Add($"[{msg.timestamp:HH:mm:ss}] To {msg.toUser}: {msg.message}");
                            }
                            else
                            {
                                // Incoming message
                                MessagesListBox.Items.Add($"[{msg.timestamp:HH:mm:ss}] From {msg.fromUser}: {msg.message}");
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
            }
            catch (SocketException)
            {
                HandleConnectionLoss();
            }
            catch (IOException)
            {
                HandleConnectionLoss();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load messages error: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            ConnectButton.Content = _isConnected ? "Disconnect" : "Connect";
            SendButton.IsEnabled = _isConnected;
            RefreshUsersButton.IsEnabled = _isConnected;
        }

        private void RefreshUsersButton_Click(object sender, RoutedEventArgs e)
        {
            LoadUsersList();
        }

        private void UsersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (UsersListBox.SelectedItem is UserInfo selectedUser)
            {
                _selectedUser = selectedUser.Username;
                ToUserTextBox.Text = selectedUser.Username;
                LoadMessagesWithUser(selectedUser.Username);
            }
        }

        private void UsersListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var listBox = sender as System.Windows.Controls.ListBox;
            if (listBox == null) return;

            // Get the item that was double-clicked
            var item = listBox.SelectedItem;
            if (item == null)
            {
                // If no item is selected, try to get it from the click position
                var point = e.GetPosition(listBox);
                var hitTestResult = System.Windows.Media.VisualTreeHelper.HitTest(listBox, point);
                if (hitTestResult != null)
                {
                    var dependencyObject = hitTestResult.VisualHit;
                    while (dependencyObject != null && !(dependencyObject is System.Windows.Controls.ListBoxItem))
                    {
                        dependencyObject = System.Windows.Media.VisualTreeHelper.GetParent(dependencyObject);
                    }
                    if (dependencyObject is System.Windows.Controls.ListBoxItem listBoxItem)
                    {
                        item = listBoxItem.DataContext;
                    }
                }
            }

            if (item is UserInfo selectedUser)
            {
                ToUserTextBox.Text = selectedUser.Username;
                // Focus on message text box for quick typing
                MessageTextBox.Focus();
            }
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

