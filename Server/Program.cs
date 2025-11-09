using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Data.SQLite;

namespace Server
{
    class Program
    {
        private static TcpListener? _listener;
        private static string _connectionString = "Data Source=messages.db;Version=3;";

        static void Main(string[] args)
        {
            InitializeDatabase();
            
            int port = 8888;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            
            Console.WriteLine($"Server started on port {port}");
            Console.WriteLine("Waiting for clients...");

            while (true)
            {
                TcpClient client = _listener.AcceptTcpClient();
                Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
                
                Task.Run(() => HandleClient(client));
            }
        }

        static void InitializeDatabase()
        {
            if (!File.Exists("messages.db"))
            {
                SQLiteConnection.CreateFile("messages.db");
            }

            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Messages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FromUser TEXT NOT NULL,
                        ToUser TEXT NOT NULL,
                        Message TEXT NOT NULL,
                        Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                
                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        static void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];
                
                while (client.Connected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Received: {request}");

                    string response = ProcessRequest(request);
                    
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseBytes, 0, responseBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine("Client disconnected");
            }
        }

        static string ProcessRequest(string request)
        {
            string[] parts = request.Split('|');
            if (parts.Length < 2) return "ERROR|Invalid request format";

            string command = parts[0];
            string username = parts[1];

            switch (command)
            {
                case "SEND":
                    if (parts.Length < 4) return "ERROR|Invalid SEND format";
                    return SendMessage(parts[1], parts[2], parts[3]);

                case "GET":
                    return GetMessages(username);

                case "PING":
                    return "PONG";

                default:
                    return "ERROR|Unknown command";
            }
        }

        static string SendMessage(string fromUser, string toUser, string message)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string insertQuery = "INSERT INTO Messages (FromUser, ToUser, Message) VALUES (@from, @to, @msg)";
                    
                    using (var command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@from", fromUser);
                        command.Parameters.AddWithValue("@to", toUser);
                        command.Parameters.AddWithValue("@msg", message);
                        command.ExecuteNonQuery();
                    }
                }
                return "OK|Message sent";
            }
            catch (Exception ex)
            {
                return $"ERROR|{ex.Message}";
            }
        }

        static string GetMessages(string username)
        {
            try
            {
                var messages = new List<string>();
                
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = "SELECT FromUser, Message, Timestamp FROM Messages WHERE ToUser = @user ORDER BY Timestamp";
                    
                    using (var command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@user", username);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string fromUser = reader.GetString(0);
                                string message = reader.GetString(1);
                                string timestamp = reader.GetString(2);
                                messages.Add($"{fromUser}|{message}|{timestamp}");
                            }
                        }
                    }
                }

                string result = string.Join("||", messages);
                return $"OK|{result}";
            }
            catch (Exception ex)
            {
                return $"ERROR|{ex.Message}";
            }
        }
    }
}

