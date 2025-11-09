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

        public MainWindow()
        {
            InitializeComponent();
            UpdateUI();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                Connect();
            }
            else
            {
                Disconnect();
            }
        }

        private void Connect()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                {
                    MessageBox.Show("Please enter a username", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string server = ServerTextBox.Text;
                int port = int.Parse(PortTextBox.Text);

                _tcpClient = new TcpClient();
                _tcpClient.Connect(server, port);
                _stream = _tcpClient.GetStream();

                _isConnected = true;
                StatusTextBlock.Text = $"Connected as {UsernameTextBox.Text}";
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isConnected = false;
                UpdateUI();
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
                string request = $"SEND|{UsernameTextBox.Text}|{ToUserTextBox.Text}|{MessageTextBox.Text}";
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
                string request = $"GET|{UsernameTextBox.Text}";
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
            ConnectButton.Content = _isConnected ? "Disconnect" : "Connect";
            ServerTextBox.IsEnabled = !_isConnected;
            PortTextBox.IsEnabled = !_isConnected;
            UsernameTextBox.IsEnabled = !_isConnected;
            SendButton.IsEnabled = _isConnected;
            RefreshButton.IsEnabled = _isConnected;
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}

