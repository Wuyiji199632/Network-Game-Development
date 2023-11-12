using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Security.Cryptography;
using System.Linq;


public class GameSession
{
    public string SessionID { get; set; }
    public string Password { get; set; }
    public Socket HostSocket { get; set; }
    public List<Socket> MemberSockets { get; set; } = new List<Socket>();
}


public class GameServer : MonoBehaviour
{
    private Socket serverSocket;
    public bool isRunning,joinRoomDecision;
    private List<Socket> clientSockets = new List<Socket>();
    private const int BUFFER_SIZE = 2048;
    private byte[] buffer = new byte[BUFFER_SIZE];
    [SerializeField]
    private int port = 7777;
    
    public Button startGameBtn, quitGameBtn, createRoomBtn, createRoomBtn2, joinRoomBtn, joinRoomBtn2;

    public Dictionary<string, GameSession> activeSessions=new Dictionary<string, GameSession>();//Dictionary to store the active game sessions

   
    
    public InputField createSessionIDField, createSessionPasswordField;
    private void Awake()
    {
        DontDestroyOnLoad(this);
    }
    void Start()
    {
        //StartServer();
        startGameBtn.gameObject.SetActive(true);
        quitGameBtn.gameObject.SetActive(true);
        createRoomBtn.gameObject.SetActive(false);
        joinRoomBtn.gameObject.SetActive(false);
        createSessionIDField.gameObject.SetActive(false);
        createSessionIDField.gameObject.SetActive(false);
        createSessionPasswordField.gameObject.SetActive(false);

    }

    public void StartServer()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); //Ensure that each socket address can be reused
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        serverSocket.Listen(10);
        isRunning = true;
        serverSocket.BeginAccept(AcceptCallback, null);

        createSessionIDField.gameObject.SetActive(true);
        createSessionPasswordField.gameObject.SetActive(true);
        createRoomBtn.gameObject.SetActive(false);
        joinRoomBtn.gameObject.SetActive(false);
        createRoomBtn2.gameObject.SetActive(true);

        Debug.Log("Server started on port " + port);
    }


    private void AcceptCallback(IAsyncResult AR)
    {
        Socket socket;

        try
        {
            socket = serverSocket.EndAccept(AR);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        // First, broadcast the message to all connected clients before adding the new client to the list
        string connectMessage = "New player joined the game from IP: " + socket.RemoteEndPoint.ToString();
        BroadcastMessage(connectMessage);

        // Now add the new client socket to the list
        clientSockets.Add(socket);

        // Send the welcome message to the connected client
        SendMessage(socket, "Welcome to the Game Server");

        // Begin receiving data from the new client
        socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);

        // Log the connection
        Debug.Log(connectMessage);

        // Continue listening for new clients
        serverSocket.BeginAccept(AcceptCallback, null);
    }
    private void ProcessReceivedData(Socket current, int received)
    {
        byte[] recBuf = new byte[received];
        Array.Copy(buffer, recBuf, received);
        string text = Encoding.ASCII.GetString(recBuf);
        Debug.Log("Received Text: " + text);

        // Echo the text back to the client or process further based on your needs.
        SendMessage(current, text);

        // Continue to listen for messages from this client.
        current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
    }

    private void HandleClientDisconnection(Socket current)
    {
        Debug.Log("Client disconnected: " + current.RemoteEndPoint);
        string disconnectMessage = "CLIENT DISCONNECTED:" + current.RemoteEndPoint.ToString();
        BroadcastMessage(disconnectMessage);

        // Check if the disconnected client is the host of any session
        var session = activeSessions.Values.FirstOrDefault(s => s.HostSocket == current);
        if (session != null)
        {
            foreach (var memberSocket in session.MemberSockets)
            {
                SendMessage(memberSocket, $"HostDisconnected:{session.SessionID}");
                // Consider disconnecting the member here, or allow them to return to the lobby.
            }
            activeSessions.Remove(session.SessionID);
        }
        else
        {
            // If not a host, remove from the member list of the respective session
            foreach (var s in activeSessions.Values)
            {
                if (s.MemberSockets.Remove(current))
                {
                    // Optionally, notify the host and other members that a player has left
                    break;
                }
            }
        }
        clientSockets.Remove(current);
        current.Close();
    }

    private void ReceiveCallback(IAsyncResult AR)
    {
        Socket current = (Socket)AR.AsyncState;
        int received;

        try
        {
            received = current.EndReceive(AR);
            if (received == 0) // Client has disconnected gracefully
            {
                HandleClientDisconnection(current);
                
                return;
            }

            // Process the received data and prepare for the next message.
            
            ProcessRequestData(current, received);

        }
        catch (SocketException ex)
        {
            Debug.Log("Client forcefully disconnected at " + current.RemoteEndPoint + ": " + ex.Message);
            current.Close();
            clientSockets.Remove(current);
            BroadcastMessage("Player forcibly left the game from IP: " + current.RemoteEndPoint);
        }
    }

    private new void BroadcastMessage(string message)
    {
        
        foreach (Socket clientSocket in clientSockets)
        {
            SendMessage(clientSocket, message);
        }
    }
    private void SendMessage(Socket socket, string message)
    {
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            socket.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, socket);
        }
        catch (SocketException e)
        {
            Debug.Log("Failed to send message to client: " + e.Message);
            // Optionally, you might want to remove the client from the list if sending fails
            clientSockets.Remove(socket);
        }
    }
    private void SendCallback(IAsyncResult AR)
    {
        Socket socket = (Socket)AR.AsyncState;
        try
        {
            int bytesSent=socket.EndSend(AR);
            Debug.Log($"Sent {bytesSent} bytes to client {socket.RemoteEndPoint}");
        }
        catch (SocketException e)
        {
            Debug.Log("Failed to send message to client: " + e.Message);
           
            clientSockets.Remove(socket);
        }
    }

    #region Session Communication Logics
    /* public void CreateRoom()
    {
        string sessionID = createSessionIDField.text;
        string sessionPassword = createSessionPasswordField.text;

        if (!activeSessions.ContainsKey(sessionID))
        {
            activeSessions.Add(sessionID, sessionPassword);

            Debug.Log($"Session created with ID: {sessionID} and Password: {sessionPassword}");
        }
        else
        {
            Debug.LogError("Session ID already exists.");
        }

    }*/

    private void CreateRoom(Socket current, string roomID, string sessionPassword)
    {
        if (!activeSessions.ContainsKey(roomID))
        {
            string sessionID = GenerateUniqueSessionID();
            var newSession = new GameSession { SessionID = roomID, Password = sessionPassword, HostSocket = current };
            activeSessions.Add(roomID, newSession);
            Debug.Log($"Session created. Total active sessions: {activeSessions.Count}");
            SendMessage(current, $"Room Created:Room Creation Succeeded with{sessionID}");
            Debug.Log($"Room created with ID: {sessionID}, Active Sessions: {string.Join(", ", activeSessions.Keys)}");
        }
        else
        {
            SendMessage(current, "Room Created:Fail:Session ID already exists.");
        }
    }
    private string GenerateUniqueSessionID()
    {
        
        return Guid.NewGuid().ToString();
    }
    private void JoinRoom(Socket current, string roomID, string roomPassword)
    {
        Debug.Log($"Attempting to join session with ID: {roomID}, Active Sessions: {string.Join(", ", activeSessions.Keys)}");
        Debug.Log($"Active Sessions count before join attempt: {activeSessions.Count}");
        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
            if (roomPassword==session.Password)
            {
                session.MemberSockets.Add(current); // Add the client to the room's member list
                SendMessage(current, "JoinRoom Accepted");
            }
            else
            {
                // Client provided the wrong password
                SendMessage(current, "JoinRoom Request Rejected:Incorrect Password");
            }
        }
        else
        {
            //Debug.Log($"Active Sessions: {string.Join(", ", activeSessions.Keys)}");
            SendMessage(current, "JoinRoom Request Rejected:Session does not exist");
        }


    }
    private void ProcessRequestData(Socket current, int received)
    {
        // Convert received bytes into a text string.
        byte[] recBuf = new byte[received];
        Array.Copy(buffer, recBuf, received);
        string text = Encoding.ASCII.GetString(recBuf);
        Debug.Log("Received Text: " + text);

        // Now we need to interpret the text and take action based on it.
        InterpretData(current, text);
    }
    private void InterpretData(Socket current, string text)
    {
        
        string[] splitData = text.Split(':');
        if (splitData.Length < 2) // Not enough parts to interpret the command.
        {
            Debug.LogError("Invalid command received.");
            return;
        }

     
        string commandType = splitData[0];
        switch (commandType)
        {
            case "CreateRoom":
                if (splitData.Length == 3) // Ensure we have enough parts.
                {
                    CreateRoom(current, splitData[1], splitData[2]);
                }
                else
                {
                    SendMessage(current, "Error:Invalid CreateRoom command format.");
                }
                break;
            case "JoinRoom":
                if (splitData.Length == 3) // Ensure we have enough parts.
                {
                    JoinRoom(current, splitData[1], splitData[2]);
                }
                else
                {
                    SendMessage(current, "Error:Invalid JoinRoom command format.");
                }
                break;
            case "QuitGame":
            case "BackToMenu":
                HandleClientLeaving(current, splitData[1], commandType == "QuitGame");
                break;

            default:
                Debug.LogError($"Unknown command received: {commandType}");
                break;
        }
    }

    private void BroadcastMessageToSessionMembers(GameSession session, string message, Socket excludeSocket)
    {
        foreach (var memberSocket in session.MemberSockets.Where(s => s != excludeSocket))
        {
            SendMessage(memberSocket, message);
        }
    }
    private void HandleClientLeaving(Socket current, string sessionID, bool isQuitting)
    {
        if (activeSessions.TryGetValue(sessionID, out GameSession session))
        {
            if (session.HostSocket == current)
            {
                // Host is leaving, inform members and close the session.
                BroadcastMessageToSessionMembers(session, "Host has left the session. Returning to main menu.", null);
                CloseSession(session);
            }
            else
            {
                // A member is leaving, inform the host and other members.
                session.MemberSockets.Remove(current);
                var leaveMessage = $"{current.RemoteEndPoint} has left the session.";
                BroadcastMessageToSessionMembers(session, leaveMessage, current);
                SendMessage(session.HostSocket, leaveMessage);
            }
        }
        if (isQuitting)
        {
            current.Shutdown(SocketShutdown.Both);
            current.Close();
            clientSockets.Remove(current);
        }
    }
    private void CloseSession(GameSession session)
    {
        foreach (var socket in session.MemberSockets)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception ex)
            {
                Debug.Log($"Error closing socket: {ex.Message}");
            }
        }
        activeSessions.Remove(session.SessionID);
    }

    #endregion

    private void CloseAllSockets()
    {
        foreach (Socket socket in clientSockets)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        serverSocket.Close();
       
    }

    private void OnApplicationQuit()
    {
        CloseAllSockets();
    }

    public void QuitGame()
    {
        CloseAllSockets();
        Application.Quit();
        
    }
}
