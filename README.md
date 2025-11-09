# TCP Messenger System

A messaging system with a central server and WPF client applications using TCP protocol.

## Components

### Server (Console Application)
- Listens on port 8888
- Uses SQLite database to store messages
- Handles client connections using TcpListener
- Commands:
  - `SEND|fromUser|toUser|message` - Send a message
  - `GET|username` - Retrieve messages for a user
  - `PING` - Test connection

### Client (WPF Application)
- Connects to server using TcpClient
- Send messages to other users
- Receive messages addressed to the current user
- View message history

## Running

1. Start the server:
```bash
cd Server
dotnet run
```

2. Start one or more client instances:
```bash
cd Client
dotnet run
```

3. In each client:
   - Enter username
   - Enter server address (default: localhost) and port (default: 8888)
   - Click Connect
   - Send messages to other users
   - Click Refresh Messages to retrieve new messages

## Protocol

Messages are sent as pipe-delimited strings:
- Send: `SEND|fromUser|toUser|message`
- Get: `GET|username`
- Response: `OK|data` or `ERROR|message`

