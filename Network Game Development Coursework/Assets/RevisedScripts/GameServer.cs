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
    public string RoomID { get; set; } //This is actually the room id for a specific room
    public string Password { get; set; }
    public Socket HostSocket { get; set; }
    public List<Socket> MemberSockets { get; set; } = new List<Socket>();

    public Dictionary<Socket, string> PlayerCharacters { get; set; } = new Dictionary<Socket, string>();
}


public class GameServer : MonoBehaviour
{
    public static GameServer Instance;
    private Socket serverSocket;
    public bool isRunning,joinRoomDecision;
    private List<Socket> clientSockets = new List<Socket>();
    private const int BUFFER_SIZE = 2048;
    private byte[] buffer = new byte[BUFFER_SIZE];
    [SerializeField]
    private int port = 7777;
    
    public Button startGameBtn, quitGameBtn, createRoomBtn, createRoomBtn2, joinRoomBtn, joinRoomBtn2;

    public Dictionary<string, GameSession> activeSessions=new Dictionary<string, GameSession>();//Dictionary to store the active game sessions
   
    public List<string> AvailableCharacters = new List<string> {"HeavyBandit", "LightBandit"};


    public InputField createSessionIDField, createSessionPasswordField;
    public Button startGameButton;

    private readonly object characterSelectLock = new object();

    private Queue<Action> characterSelectionQueue = new Queue<Action>();
    private bool isProcessingCharacterSelection = false;
    private void Awake()
    {
        
        DontDestroyOnLoad(this);

    }
    void Start()
    {
        
        startGameBtn.gameObject.SetActive(true);
        quitGameBtn.gameObject.SetActive(true);
        createRoomBtn.gameObject.SetActive(false);
        joinRoomBtn.gameObject.SetActive(false);
        createSessionIDField.gameObject.SetActive(false);
        createSessionIDField.gameObject.SetActive(false);
        createSessionPasswordField.gameObject.SetActive(false);


        startGameButton.gameObject.SetActive(false);
        startGameButton.onClick.AddListener(OnStartGameClicked);
    }

    private void OnStartGameClicked()
    {
        Debug.Log("Get ready to start game!");
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
                SendMessage(memberSocket, $"HostDisconnected:{session.RoomID}");
                // Consider disconnecting the member here, or allow them to return to the lobby.
            }
            activeSessions.Remove(session.RoomID);
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

            // Clear the buffer for accepting following messages, this is an important step because without it the buffer will be clogged with the previous data and incoming data will not be passed into it
            Array.Clear(buffer, 0, buffer.Length);

            // Continue listening for new data from the client.
            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);

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
        byte[] data = Encoding.ASCII.GetBytes(message);
        try
        {            
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
   

    private void CreateRoom(Socket current, string roomID, string roomPassword)
    {
        if (!activeSessions.ContainsKey(roomID))
        {
            string sessionID = GenerateUniqueSessionID();
            var newSession = new GameSession { RoomID = roomID, Password = roomPassword, HostSocket = current, PlayerCharacters = new Dictionary<Socket, string>() };
            activeSessions.Add(roomID, newSession);
            Debug.Log($"Session created. Total active sessions: {activeSessions.Count}");
            SendMessage(current, $"RoomCreated:{roomID}");
            Debug.Log($"Room created with session ID: {sessionID}, Active Room ID: {string.Join(", ", activeSessions.Keys)}, Active Room Password: {newSession.Password}");
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
        Debug.Log($"Attempting to join session with room ID: {roomID}, Active Sessions: {string.Join(", ", activeSessions.Keys)}");
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
        Debug.Log("Interpreting Data: " + text);
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
                string sessionId = splitData.Length > 1 ? splitData[1] : null;
                if (!string.IsNullOrEmpty(sessionId) && IsHost(current, sessionId))
                {
                    HandleHostLeaving(current, sessionId);
                }
                else if(!IsHost(current, sessionId))
                {
                    HandleClientLeaving(current, sessionId);
                }
                else
                {
                    // Send an error message back to the client if the session ID is missing or incorrect
                    SendMessage(current, "Error: Session ID is missing or incorrect.");
                }
                break;

            case "CharacterSelectionUpdate":
                
                if (splitData.Length == 3)
                {
                   
                    string roomID = splitData[1];
                    string characterName = splitData[2];
                    QueueCharacterSelection(current, roomID, characterName);
                }
                else
                {
                    SendMessage(current, "Error:Invalid SelectCharacter command format.");
                }
                break;
            case "CharacterSelectionFailed":
                if (splitData.Length == 2)
                {
                    string failedCharacterName = splitData[1];
                    // Handle the character selection failure here
                    // You might want to log this information or notify the client accordingly
                    Debug.Log($"Character selection failed for {failedCharacterName}, because another player has selected it");
                }
                else
                {
                    SendMessage(current, "Error:Invalid CharacterSelectionFailed command format.");
                }
                break;
            default:
                Debug.LogError($"Unknown command received: {commandType}");
                break;
        }
    }
    #region Character Selection Logics

    private void QueueCharacterSelection(Socket current, string roomID, string characterName)
    {
        lock (characterSelectionQueue)
        {
            characterSelectionQueue.Enqueue(() => SelectCharacter(current, roomID, characterName));
            if (!isProcessingCharacterSelection)
            {
                ProcessNextCharacterSelection();
            }
        }
    }
    private void ProcessNextCharacterSelection()
    {
        lock (characterSelectionQueue)
        {
            if (characterSelectionQueue.Count > 0)
            {
                isProcessingCharacterSelection = true;
                var selectCharacterAction = characterSelectionQueue.Dequeue();
                selectCharacterAction.Invoke();
            }
            else
            {
                isProcessingCharacterSelection = false;
            }
        }
    }
    private void SelectCharacter(Socket current, string roomID, string characterName)
    {
        lock (characterSelectLock) //Ensure that only one thread executes this function at a time
        {
            try
            {
                if (activeSessions.TryGetValue(roomID, out GameSession session))
                {
                    if (!session.PlayerCharacters.Values.Contains(characterName))
                    {
                        // Character is not yet selected by anyone else, so select it for the current player
                        session.PlayerCharacters[current] = characterName;
                        BroadcastCharacterSelection(session, current, characterName);

                        // Check if all players have selected characters; if so, broadcast start game button
                        if (session.PlayerCharacters.Count == session.MemberSockets.Count)
                        {
                            BroadcastStartGameButton(session);
                        }
                    }
                    else
                    {
                        // Character is already selected by another player, inform the current player
                        SendMessage(current, $"CharacterSelectionFailed:{characterName}");
                    }
                }
                else
                {
                    SendMessage(current, $"Error:Session with ID {roomID} does not exist.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in SelectCharacter: " + ex.Message);
            }
            finally
            {
                ProcessNextCharacterSelection();
            }
        }

    }
    private void BroadcastCharacterSelection(GameSession session, Socket characterSelector, string characterName)
    {
        string selectorEndpoint = characterSelector.RemoteEndPoint.ToString();
        string message = $"CharacterSelectionUpdate:{selectorEndpoint}:{characterName}";

        foreach (var memberSocket in session.MemberSockets)
        {
            Debug.Log($"Broadcasting character selection: {message}");

            SendMessage(memberSocket, message);
        }
    }
    private void BroadcastMessageToSessionMembers(GameSession session, string message, Socket excludeSocket)
    {
        Debug.Log($"Broadcasting message to session members (excluding {excludeSocket?.RemoteEndPoint}): {message}");
        foreach (var memberSocket in session.MemberSockets)
        {
            if (memberSocket != excludeSocket)
            {
                Debug.Log($"Sending to {memberSocket.RemoteEndPoint}");
                SendMessage(memberSocket, message);
            }
        }
    }
    private void BroadcastStartGameButton(GameSession session)
    {
        string message = "ShowStartGameButton";
        foreach (var memberSocket in session.MemberSockets)
        {
            SendMessage(memberSocket, message);
        }
    }
    #endregion

    private void HandleHostLeaving(Socket hostSocket, string roomID)
    {
        if (string.IsNullOrEmpty(roomID))
        {
            Debug.LogError("Session ID is null or empty in HandleHostLeaving.");
            return;
        }

        if (activeSessions.TryGetValue(roomID, out var session))
        {
            CloseSession(session);
            activeSessions.Remove(roomID);
            SendMessage(hostSocket, $"SessionClosed:{roomID}");
            // Inform all members that the host has left and the session is closing
            BroadcastMessageToSessionMembers(session, "Host has disconnected. Session is closing.", hostSocket);

            // Close the member sockets and remove them from the list
            foreach (var memberSocket in session.MemberSockets)
            {
                try
                {
                    memberSocket.Shutdown(SocketShutdown.Both);
                    memberSocket.Close();
                    clientSockets.Remove(memberSocket);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error closing member socket: {ex.Message}");
                }
            }

            // Close the host socket
            try
            {
                session.HostSocket.Shutdown(SocketShutdown.Both);
                session.HostSocket.Close();
                clientSockets.Remove(hostSocket);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error closing host socket: {ex.Message}");
            }

          
            Debug.Log($"Session {session.RoomID} closed because the host has left.");
        }
        else
        {
            Debug.LogError($"Failed to find session with ID: {roomID}.");
        }

    }

    private void HandleClientLeaving(Socket current, string roomID)
    {
        if (activeSessions.TryGetValue(roomID, out GameSession session))
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
       
        Debug.Log($"Processed client leaving: {current.RemoteEndPoint}");
    }
    private void CloseSession(GameSession session)
    {
        // Close all member sockets
        foreach (var socket in session.MemberSockets)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error closing socket: {ex.Message}");
            }
        }

        // Close the host socket if it is still connected
        if (session.HostSocket != null && session.HostSocket.Connected)
        {
            try
            {
                session.HostSocket.Shutdown(SocketShutdown.Both);
                session.HostSocket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error closing host socket: {ex.Message}");
            }
        }

        // Remove the session from the active sessions
        activeSessions.Remove(session.RoomID);
        Debug.Log($"Session {session.RoomID} has been closed.");
    }
    private bool IsHost(Socket current, string roomID)
    {
        return activeSessions.TryGetValue(roomID, out var session) && session.HostSocket == current;
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
