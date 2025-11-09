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
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Task.Run(() => HandleClient(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error accepting client connection:");
                    Console.WriteLine($"  Error Type: {ex.GetType().Name}");
                    Console.WriteLine($"  Error Message: {ex.Message}");
                    Console.WriteLine($"  Stack Trace: {ex.StackTrace}");
                }
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
                string createMessagesTable = @"
                    CREATE TABLE IF NOT EXISTS Messages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FromUser TEXT NOT NULL,
                        ToUser TEXT NOT NULL,
                        Message TEXT NOT NULL,
                        Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                
                string createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        Password TEXT NOT NULL,
                        IP TEXT
                    )";
                
                using (var command = new SQLiteCommand(createMessagesTable, connection))
                {
                    command.ExecuteNonQuery();
                }
                
                using (var command = new SQLiteCommand(createUsersTable, connection))
                {
                    command.ExecuteNonQuery();
                }
                
                // Check if IP column exists, if not add it (for existing databases)
                try
                {
                    string checkColumnQuery = "SELECT IP FROM Users LIMIT 1";
                    using (var checkCommand = new SQLiteCommand(checkColumnQuery, connection))
                    {
                        checkCommand.ExecuteScalar();
                    }
                }
                catch
                {
                    // IP column doesn't exist, add it
                    Console.WriteLine("Adding IP column to Users table...");
                    string addColumnQuery = "ALTER TABLE Users ADD COLUMN IP TEXT";
                    using (var alterCommand = new SQLiteCommand(addColumnQuery, connection))
                    {
                        alterCommand.ExecuteNonQuery();
                    }
                    Console.WriteLine("IP column added successfully");
                }
            }
        }

        static void HandleClient(TcpClient client)
        {
            string clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            string clientIP = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
            
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];
                
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Client connected from {clientEndpoint}");
                
                while (client.Connected)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Connection closed by client {clientEndpoint} (received 0 bytes)");
                            break;
                        }

                        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Received from {clientEndpoint}: {request}");

                        string response = ProcessRequest(request, client);
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sending to {clientEndpoint}: {response}");
                        
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                        stream.Write(responseBytes, 0, responseBytes.Length);
                        stream.Flush();
                    }
                    catch (SocketException socketEx)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Socket error with client {clientEndpoint}:");
                        Console.WriteLine($"  Error Code: {socketEx.SocketErrorCode}");
                        Console.WriteLine($"  Error Message: {socketEx.Message}");
                        Console.WriteLine($"  Stack Trace: {socketEx.StackTrace}");
                        break;
                    }
                    catch (IOException ioEx)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] IO error with client {clientEndpoint}:");
                        Console.WriteLine($"  Error Message: {ioEx.Message}");
                        if (ioEx.InnerException != null)
                        {
                            Console.WriteLine($"  Inner Exception: {ioEx.InnerException.Message}");
                        }
                        Console.WriteLine($"  Stack Trace: {ioEx.StackTrace}");
                        break;
                    }
                }
            }
            catch (SocketException socketEx)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Socket exception while handling client {clientEndpoint}:");
                Console.WriteLine($"  Error Code: {socketEx.SocketErrorCode}");
                Console.WriteLine($"  Error Message: {socketEx.Message}");
                Console.WriteLine($"  Stack Trace: {socketEx.StackTrace}");
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] IO exception while handling client {clientEndpoint}:");
                Console.WriteLine($"  Error Message: {ioEx.Message}");
                if (ioEx.InnerException != null)
                {
                    Console.WriteLine($"  Inner Exception: {ioEx.InnerException.Message}");
                }
                Console.WriteLine($"  Stack Trace: {ioEx.StackTrace}");
            }
            catch (ObjectDisposedException disposedEx)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Object disposed exception with client {clientEndpoint}:");
                Console.WriteLine($"  Error Message: {disposedEx.Message}");
                Console.WriteLine($"  Stack Trace: {disposedEx.StackTrace}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unexpected error handling client {clientEndpoint}:");
                Console.WriteLine($"  Error Type: {ex.GetType().Name}");
                Console.WriteLine($"  Error Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                Console.WriteLine($"  Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                try
                {
                    if (client.Connected)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Closing connection with {clientEndpoint}");
                    }
                    client.Close();
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Client {clientEndpoint} disconnected");
                }
                catch (Exception closeEx)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error closing client {clientEndpoint}: {closeEx.Message}");
                }
            }
        }

        static string ProcessRequest(string request, TcpClient client)
        {
            string clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            
            try
            {
                string[] parts = request.Split('|');
                string command = parts[0].Trim();
                
                // Handle commands that don't require parameters
                if (command == "PING")
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Received PING from {clientEndpoint}");
                    return "PONG";
                }
                
                // Other commands require at least 2 parts (command and username)
                if (parts.Length < 2)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Invalid request format from {clientEndpoint}: {request}");
                    return "ERROR|Invalid request format";
                }

                string username = parts[1];
                
                // Handle GET_USERS command (requires username to exclude current user)
                if (command == "GET_USERS")
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Getting list of users from {clientEndpoint} (excluding {username})");
                    return GetUsers(username);
                }
                
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Processing {command} request from {clientEndpoint} for user: {username}");

                switch (command)
                {
                    case "REGISTER":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Invalid REGISTER format from {clientEndpoint}");
                            return "ERROR|Invalid REGISTER format";
                        }
                        string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Registering user {username} from IP {clientIP}");
                        return RegisterUser(parts[1], parts[2], clientIP);

                    case "LOGIN":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Invalid LOGIN format from {clientEndpoint}");
                            return "ERROR|Invalid LOGIN format";
                        }
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Attempting login for user {username}");
                        return LoginUser(parts[1], parts[2]);

                    case "SEND":
                        if (parts.Length < 4)
                        {
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Invalid SEND format from {clientEndpoint}");
                            return "ERROR|Invalid SEND format";
                        }
                        return SendMessage(parts[1], parts[2], parts[3]);

                    case "GET":
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Getting messages for user {username}");
                        return GetMessages(username);

                    default:
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unknown command '{command}' from {clientEndpoint}");
                        return "ERROR|Unknown command";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing request from {clientEndpoint}:");
                Console.WriteLine($"  Error Type: {ex.GetType().Name}");
                Console.WriteLine($"  Error Message: {ex.Message}");
                Console.WriteLine($"  Stack Trace: {ex.StackTrace}");
                return $"ERROR|{ex.Message}";
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

        static string RegisterUser(string username, string password, string ip)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Check if nickname already exists
                    string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @user";
                    using (var checkCommand = new SQLiteCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@user", username);
                        int count = Convert.ToInt32(checkCommand.ExecuteScalar());
                        
                        if (count > 0)
                        {
                            return "ERROR|This nickname is already in use. Please choose another nickname.";
                        }
                    }
                    
                    // Insert new user
                    string insertQuery = "INSERT INTO Users (Username, Password, IP) VALUES (@user, @pass, @ip)";
                    using (var command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@user", username);
                        command.Parameters.AddWithValue("@pass", password);
                        command.Parameters.AddWithValue("@ip", ip);
                        command.ExecuteNonQuery();
                    }
                }
                return "OK|Registration successful. Please enter your nickname and password to login.";
            }
            catch (SQLiteException ex) when (ex.Message.Contains("UNIQUE"))
            {
                return "ERROR|This nickname is already in use. Please choose another nickname.";
            }
            catch (Exception ex)
            {
                return $"ERROR|{ex.Message}";
            }
        }

        static string LoginUser(string username, string password)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Check if username exists
                    string checkUserQuery = "SELECT COUNT(*) FROM Users WHERE Username = @user";
                    int userCount;
                    using (var checkCommand = new SQLiteCommand(checkUserQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@user", username);
                        userCount = Convert.ToInt32(checkCommand.ExecuteScalar());
                    }
                    
                    if (userCount == 0)
                    {
                        return "ERROR|Nickname not found in database. Please try again with a different nickname or register first.";
                    }
                    
                    // Check if password matches
                    string selectQuery = "SELECT COUNT(*) FROM Users WHERE Username = @user AND Password = @pass";
                    using (var command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@user", username);
                        command.Parameters.AddWithValue("@pass", password);
                        int count = Convert.ToInt32(command.ExecuteScalar());
                        
                        if (count > 0)
                        {
                            return "OK|Login successful";
                        }
                        else
                        {
                            return "ERROR|Password is incorrect. Please try again with the correct password.";
                        }
                    }
                }
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

        static string GetUsers(string excludeUsername)
        {
            try
            {
                var users = new List<string>();
                
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    // Select all users except the current user
                    string selectQuery = "SELECT Username FROM Users WHERE Username != @excludeUser ORDER BY Username";
                    
                    using (var command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@excludeUser", excludeUsername);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string username = reader.GetString(0);
                                users.Add(username);
                            }
                        }
                    }
                }

                string result = string.Join("||", users);
                return $"OK|{result}";
            }
            catch (Exception ex)
            {
                return $"ERROR|{ex.Message}";
            }
        }
    }
}

