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
using System.Xml.Serialization;


public class GameSession
{
    public string RoomID { get; set; } //This is actually the room id for a specific room
    public string Password { get; set; }
    public Socket HostSocket { get; set; }

    public Socket NonHostSocket { get; set; }
    public List<Socket> MemberSockets { get; set; } = new List<Socket>();

    public Dictionary<Socket, string> PlayerCharacters { get; set; } = new Dictionary<Socket, string>();

    public int NumberOfReadyClients = 0;
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
    

    private readonly object characterSelectLock = new object();

    private readonly object characterReadyLock=new object();

    private Queue<Action> characterSelectionQueue = new Queue<Action>();
    private bool isProcessingCharacterSelection = false;
    [SerializeField] private Text waitForHostTxt;
    [SerializeField] private Button gameStartBtn;
    GameSession currentGameSession= null;
    private bool hostReady = false,NonHostReady=false;

    public GameObject heavyBanditPrefab, lightBanditPrefab;

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
        gameStartBtn.gameObject.SetActive(false);

        waitForHostTxt.gameObject.SetActive(false);
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
       
        // Now broadcast the message to all connected clients before adding the new client to the list
        string connectMessage = "New player joined the game from IP: " + socket.RemoteEndPoint.ToString();
        BroadcastMessage(connectMessage);

        // Add the new client socket to the list
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
            newSession.MemberSockets.Add(current);
            Debug.Log($"Session created. Total active sessions: {activeSessions.Count}");
            SendMessage(current, $"RoomCreated:{roomID}");
            SyncCharacterSelectionState(current, newSession);
            Debug.Log($"Room created with session ID: {sessionID}, Active Room ID: {string.Join(", ", activeSessions.Keys)}, Active Room Password: {newSession.Password}");
            Debug.Log($"Room {roomID} created. Number of MemberSockets after creation: {newSession.MemberSockets.Count}");
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
    private string GenerateUniqueClientId() //Differentiate different clients for the local and the remote by using this function
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
                if (session.HostSocket != current)
                {
                    session.NonHostSocket = current; // Assign non-host client
                }
                session.MemberSockets.Add(current); // Add the client to the room's member list
               
                Debug.Log($"Client joined room {roomID}. Number of MemberSockets after joining: {session.MemberSockets.Count}");
                SendMessage(current, $"JoinRoom Accepted:{roomID}");
                SyncCharacterSelectionState(current, session);
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
    private void InterpretData(Socket current, string text) //Interpret data sent from the client side
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
            case "CharacterSelectionConfirmed":
                if (splitData.Length == 4) // Ensure we have enough parts
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];
                    string clientEndpoint = splitData[3];
                    HandleCharacterSelectionConfirmed(roomID, characterName, clientEndpoint);
                }
                else
                {
                    SendMessage(current, "Error:Invalid CharacterSelectionConfirmed command format.");
                }
                break;
            case "CheckCharacterSelection":
                if (splitData.Length == 3)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];
                    CheckCharacterSelection(current, roomID, characterName);
                }
                else
                {
                    SendMessage(current, "Error:Invalid CheckCharacterSelection command format.");
                }
                break;
            case "ProcessCharacterSelectionRequest": //Process character selection for non-host client
                if (splitData.Length == 3)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];

                    //ProcessCharacterSelectionRequest(current, roomID, characterName, requestingClientEndpoint);
                    ForwardSelectionRequestToHost(current, roomID, characterName);
                }
              
                break;
            case "CharacterSelectionFailed":
                if (splitData.Length == 4)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];
                    string clientEndpoint = splitData[3];
                    NotifyClientOfSelectionFailure(roomID, characterName, clientEndpoint);
                }
                else
                {
                    SendMessage(current, "Error:Invalid CharacterSelectionFailed command format.");
                }
                break;
            case "CharacterCancelSelection":
                if (splitData.Length == 3)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];
                    CancelCharacterSelection(current, roomID, characterName);
                }
                else
                {
                    SendMessage(current, "Error:Invalid CharacterCancelSelection command format.");
                }
                break;
            case "ProcessReadiness":
                if (splitData.Length == 3)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];

                    if(IsHost(current,roomID))
                        SetHostCharacterReady(current, roomID, characterName,true);
                    else 
                        SetNonHostCharacterReady(current,roomID, characterName, true);
                }
                    break;
            case "ProcessCacelReadiness":

                if (splitData.Length == 3)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];
                    if (IsHost(current, roomID))
                        SetHostCharacterReady(current, roomID, characterName, false);
                    else
                        SetNonHostCharacterReady(current, roomID, characterName, false);
                }
                    break;

            case "HostStartGame":
                if (splitData.Length == 3)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];
                    if (IsHost(current, roomID))
                    {
                        StartGameAndLoadAssignGameCharacters(current, roomID, characterName);
                        Debug.Log($"Game started in room {roomID}");
                    }
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
    private void SelectCharacter(Socket current, string roomID, string characterName)// This needs to differentiate whether it is a host or a non-host
    {
        lock (characterSelectLock) //Ensure that only one thread executes this function at a time
        {
            try
            {
               
                if (activeSessions.TryGetValue(roomID, out GameSession session))
                {
                   
                  
                    if (!session.PlayerCharacters.Values.Contains(characterName))
                    {
                        session.PlayerCharacters[current] = characterName;

                        BroadcastCharacterSelection_Host(session, current, characterName);

                        Debug.Log($"Character {characterName} selected for client {current.RemoteEndPoint}");
                        // Broadcast to other clients that character is selected
                        BroadcastMessageToSessionMembers(session, $"CharacterSelectionUpdate:{roomID}:{characterName}", current);

                        string message = $"CharacterSelectionUpdate:{roomID}:{characterName}";
                        BroadcastMessageToSession(session, message);



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
                        Debug.Log($"Character {characterName} already selected by another player.");

                    }
                    Debug.Log($"After SelectCharacter: {session.PlayerCharacters.Count} characters selected in session {roomID}");
                }
                else
                {
                    Debug.Log($"Error: Session with ID {roomID} does not exist.");
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
    private void SetHostCharacterReady(Socket current, string roomID, string characterName, bool isReady)
    {
        
        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
           
            
           
            Debug.Log($"number of ready clients: {session.NumberOfReadyClients}");
            if (IsHost(current, roomID))
            {
                string readinessFlag = isReady ? "Ready" : "NotReady";
                string memberIdentityFlag = $"Host";
                string clientIdentifier = GetClientIdentifier(current);
                string[] uniqueClientId = current.RemoteEndPoint.ToString().Split(":");
                SendMessage(current, $"SetHostClientId:{uniqueClientId[1]}");
                string message = $"HostCharacterReadyUpdate:{roomID}:{characterName}:{readinessFlag}:{memberIdentityFlag}:{clientIdentifier}";
                Debug.Log($"Current client identifier is {clientIdentifier}.");
                Debug.Log($"Character readiness update: {message}");
                BroadcastMessageToSession(session, message);
               
               
            }

           

        }
        else
        {
            Debug.Log($"Error: Session with ID {roomID} does not exist.");
            SendMessage(current, $"Error:Session with ID {roomID} does not exist.");
        }
    }
    
    private void SetNonHostCharacterReady(Socket current, string roomID, string characterName, bool isReady)
    {
        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
           
            Debug.Log($"Current Socket: {current.RemoteEndPoint}, Host Socket: {session.HostSocket.RemoteEndPoint}");
            
           
            if (!IsHost(current, roomID))
            {
                string readinessFlag = isReady ? "Ready" : "NotReady";
                string memberIdentityFlag = $"Member";
                string clientIdentifier = GetClientIdentifier(current);
                string[] uniqueClientId = current.RemoteEndPoint.ToString().Split(":");
                SendMessage(current, $"SetNonHostClientId:{uniqueClientId[1]}");
                BroadcastMessageToSession(session, $"SetNonHostClientId:{uniqueClientId[1]}");
                string msg = $"NonHostCharacterReadyUpdate:{roomID}:{characterName}:{readinessFlag}:{memberIdentityFlag}:{clientIdentifier}";
                Debug.Log($"Current client identifier is {clientIdentifier}.");
                Debug.Log($"Character readiness update: {msg}");
                BroadcastMessageToSession(session, msg);
                        
            }
            
        }
    }
   
    private void BroadcastStartGame(GameSession session)
    {
        string message = "StartGame:"; // You can customize this message
        foreach (var client in session.MemberSockets)
        {
            SendMessage(client, message);
        }
        // Also send to host if not included in MemberSockets
        if (!session.MemberSockets.Contains(session.HostSocket))
        {
            SendMessage(session.HostSocket, message);
        }
        Debug.Log("Start game message broadcasted to all clients in the session. The host side should have a button displayed for starting the game!");
    }

    private string GetClientIdentifier(Socket clientSocket)
    {
        string[] msgSplit = clientSocket.RemoteEndPoint.ToString().Split(":");
        return msgSplit[1];
    }
    private void BroadcastMessageToSession(GameSession session, string message)
    {
        foreach (var client in session.MemberSockets)
        {
            SendMessage(client, message);
        }
        // Also send to host if not included in MemberSockets
        if (!session.MemberSockets.Contains(session.HostSocket))
        {
            SendMessage(session.HostSocket, message);
        }
    }
    private void HandleCharacterSelectionConfirmed(string roomID, string characterName, string clientEndpoint)
    {
        if (activeSessions.TryGetValue(roomID, out var session))
        {
            // Update the session with the new character selection
            // Find the client socket using the endpoint string (clientEndpoint)
            var selectingClient = FindClientByEndpoint(session, clientEndpoint);
            if (selectingClient != null)
            {
                session.PlayerCharacters[selectingClient] = characterName;

                // Broadcast the character selection to all clients in the session
                BroadcastCharacterSelection_Host(session, selectingClient, characterName);

              
            }
        }
    }
    private Socket FindClientByEndpoint(GameSession session, string endpointString)
    {
        return session.MemberSockets.FirstOrDefault(s => s.RemoteEndPoint.ToString() == endpointString);
    }
    private void CheckCharacterSelection(Socket current, string roomID, string characterName)
    {
        if (activeSessions.TryGetValue(roomID, out var session))
        {
            if (session.PlayerCharacters.Values.Contains(characterName))
            {
                SendMessage(current, $"CharacterSelectionFailed:{characterName}");
            }
            else
            {
                
                SendMessage(current, $"CharacterSelectionAvailable:{characterName}");
            }
        }
    }
    private void NotifyClientOfSelectionFailure(string roomID, string characterName, string clientEndpoint)
    {
        if (activeSessions.TryGetValue(roomID, out var session))
        {
            var clientSocket = FindClientByEndpoint(session, clientEndpoint);
            if (clientSocket != null)
            {
                SendMessage(clientSocket, $"CharacterSelectionFailed:{characterName}");
            }
        }
    }
    private void SyncCharacterSelectionState(Socket client, GameSession session)
    {
        foreach (var pair in session.PlayerCharacters)
        {
            string message = $"CharacterSelectionUpdate:{pair.Key.RemoteEndPoint}:{pair.Value}";
            SendMessage(client, message);
        }
    }
    private void ForwardSelectionRequestToHost(Socket current, string roomID, string characterName)
    {
        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
            Debug.Log($"Current Socket: {current.RemoteEndPoint}, Host Socket: {session.HostSocket.RemoteEndPoint}");

            if (!IsHost(current,roomID))
            {
                string message = $"ProcessCharacterSelectionRequest:{roomID}:{characterName}:{current.RemoteEndPoint.ToString()}";
                SendMessage(session.HostSocket, message);
                Debug.Log("Forwarding message to host!");
                session.PlayerCharacters[current] = characterName;
            }
            else
            {
                Debug.Log("Current client is the host, processing directly.");
                QueueCharacterSelection(current, roomID, characterName);
            }
        }
        else
        {
            Debug.Log($"Error: Session with ID {roomID} does not exist.");
            SendMessage(current, $"Error:Session with ID {roomID} does not exist.");
        }
    }
    private void BroadcastCharacterSelection_Host(GameSession session, Socket characterSelector, string characterName)// For host client
    {
        Debug.Log($"BroadcastCharacterSelection called. Number of MemberSockets: {session.MemberSockets.Count}");

        string selectorEndpoint = characterSelector.RemoteEndPoint.ToString();
        string message = $"CharacterSelectionUpdate:{selectorEndpoint}:{characterName}";

        // Broadcast to all clients in the session
        foreach (var memberSocket in session.MemberSockets)
        {
            SendMessage(memberSocket, message);
        }

        // Also send to host if not included in MemberSockets
        if (!session.MemberSockets.Contains(session.HostSocket))
        {
            SendMessage(session.HostSocket, message);
        }

        Debug.Log("BroadcastCharacterSelection finished for host.");
    }
    
    private void BroadcastMessageToSessionMembers(GameSession session, string message, Socket excludeSocket)
    {
        Debug.Log($"Broadcasting message to session members (excluding {excludeSocket?.RemoteEndPoint}): {message}");
        foreach (var memberSocket in session.MemberSockets)
        {
            SendMessage(memberSocket, message);
        }
        if (!session.MemberSockets.Contains(session.HostSocket))
        {
            SendMessage(session.HostSocket, message);
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

    private void CancelCharacterSelection(Socket current, string roomID, string characterName)
    {
        Debug.Log($"Attempting to cancel character selection: {characterName} in Room: {roomID}");

        try
        {
            if (activeSessions.TryGetValue(roomID, out GameSession session))
            {
                Debug.Log($"Session found. Checking for character selection...");
                Debug.Log($"Total Characters Selected in Session: {session.PlayerCharacters.Count}");

                if (session.PlayerCharacters.ContainsKey(current))
                {
                    Debug.Log($"Character selection found for this client.");
                    if (session.PlayerCharacters[current] == characterName)
                    {
                        Debug.Log($"Cancelling character: {characterName}");
                        string message = $"CharacterSelectionCancelled:{roomID}:{characterName}";
                        session.PlayerCharacters.Remove(current);
                        BroadcastCharacterSelectionCancellation(session, message);
                    }
                    else
                    {
                        Debug.Log($"Mismatch in character cancellation. Expected: {session.PlayerCharacters[current]}, Received: {characterName}");
                    }
                }
                else
                {
                    Debug.Log($"Character selection not found for the current socket {current.RemoteEndPoint}. Possible reasons: socket mismatch, character not added, or concurrent modification issue.");
                }
                Debug.Log($"After CancelCharacterSelection: {session.PlayerCharacters.Count} characters selected in session {roomID}");
            }
            else
            {
                Debug.Log($"Session with ID {roomID} not found for cancellation.");
            }
        }
        catch
        {
            Debug.Log($"Fail to cancel selection! Please try again!");
        }
       
    }

    


    private void BroadcastCharacterSelectionCancellation(GameSession session, string message)
    {
        foreach (var client in session.MemberSockets)
        {
            SendMessage(client, message);
        }
        if (!session.MemberSockets.Contains(session.HostSocket))
        {
            SendMessage(session.HostSocket, message);
        }
        Debug.Log($"Broadcasting cancellation message: {message}");
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
    public bool IsHost(Socket current, string roomID)
    {
        return activeSessions.TryGetValue(roomID, out var session) && session.HostSocket == current;
    }
    #endregion

    #region Start Game Logics
    private void StartGameAndLoadAssignGameCharacters(Socket current, string roomID, string characterName)
    {
        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
            Debug.Log($"The game has started and characters are assigned to the individual player");

            string startGameMsg = $"GameHasStarted:{roomID}:{characterName}";

            SendMessage(current, startGameMsg);
            BroadcastMessageToSession(session, startGameMsg);
        }
            
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
