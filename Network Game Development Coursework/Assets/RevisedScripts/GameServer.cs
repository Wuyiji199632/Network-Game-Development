using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using TMPro;
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

    public Dictionary<Socket, string> SelectedCharacters { get; set; } = new Dictionary<Socket, string>();

    public int NumberOfReadyClients = 0;
}


public class GameServer : MonoBehaviour
{

    
    public static GameServer Instance;
    private Socket tcpServerSocket,udpServerSocket=null;
    public bool isRunning,joinRoomDecision;
    private List<Socket> clientSockets = new List<Socket>();
    private const int BUFFER_SIZE = 2048;
    private byte[] buffer = new byte[BUFFER_SIZE];
    [SerializeField]
    private int tcpPort = 8888;
    [SerializeField]
    private int udpPort = 8899;
    public Button startGameBtn, quitGameBtn, createRoomBtn, createRoomBtn2, joinRoomBtn, joinRoomBtn2;

    public Dictionary<string, GameSession> activeSessions=new Dictionary<string, GameSession>();//Dictionary to store the active game sessions
   
    public List<string> AvailableCharacters = new List<string> {"HeavyBandit", "LightBandit"};


    public InputField createSessionIDField, createSessionPasswordField;
    

    private readonly object characterSelectLock = new object();


    private Queue<Action> characterSelectionQueue = new Queue<Action>();
    private bool isProcessingCharacterSelection = false;
    [SerializeField] private Text waitForHostTxt;
    [SerializeField] private Button gameStartBtn;
    public GameSession currentGameSession= null;
    private bool hostReady = false,NonHostReady=false;
    public bool canJoinRoom=false;
    public GameObject heavyBanditPrefab, lightBanditPrefab;

    //public GameObject udpClientObj,udpServerObj;

    public bool isLocalPlayer = false;
    public bool isHost = false;
    public string hostClientID, nonHostClientID;

    public bool gameStarted = false,gameEnds=false;
    public List<BanditScript> inGameBandits=new List<BanditScript>();
    public BanditScript hostBandit=null, nonHostBandit=null;
    public BanditScript opponentBandit = null;
    public Dictionary<string, IPEndPoint> tcpIdToUdpEndpoint = new Dictionary<string, IPEndPoint>();
    public BanditScript defeatedBandit = null;
    public GameClient gameClient;
    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this);

    }
    private void Update()
    {
        if (gameStarted)// When the game starts, observe and manipulate the bandit behaviours
        {
            bool flag = false;
            
            if(!flag)
            {
                flag = true;
                hostBandit=inGameBandits.Where(t=>t.playerID==t.gameClient.localHostClientId).First();
                nonHostBandit=inGameBandits.Where(t=>t.playerID==t.gameClient.localNonHostClientId).First();
                                    
            }
                   
        }
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
        tcpServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        tcpServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); //Ensure that each socket address can be reused
        tcpServerSocket.Bind(new IPEndPoint(IPAddress.Any, tcpPort));
        tcpServerSocket.Listen(100);
        isRunning = true;
        tcpServerSocket.BeginAccept(AcceptCallback, null);


        createSessionIDField.gameObject.SetActive(true);
        createSessionPasswordField.gameObject.SetActive(true);
        createRoomBtn.gameObject.SetActive(false);
        joinRoomBtn.gameObject.SetActive(false);
        createRoomBtn2.gameObject.SetActive(true);


        Debug.Log("Server started on port " + tcpPort);
    }

   

    private void AcceptCallback(IAsyncResult AR)
    {
        Socket socket;

        try
        {
            socket = tcpServerSocket.EndAccept(AR);
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
        tcpServerSocket.BeginAccept(AcceptCallback, null);
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
        try
        {
            if (!activeSessions.ContainsKey(roomID))
            {
                string sessionID = GenerateUniqueSessionID();
                var newSession = new GameSession { RoomID = roomID, Password = roomPassword, HostSocket = current, PlayerCharacters = new Dictionary<Socket, string>() };
                activeSessions.Add(roomID, newSession);
                newSession.MemberSockets.Add(current);
                hostClientID = current.RemoteEndPoint.ToString().Split(":")[1];
                SendMessage(current, $"SetHostClientId:{hostClientID}");
                BroadcastMessageToSession(newSession, $"SetHostClientId:{hostClientID}");
                tcpIdToUdpEndpoint[hostClientID] = (IPEndPoint)current.RemoteEndPoint;

                Debug.Log($"The host client id is {hostClientID}");
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
        catch (Exception e)
        {
            if(e is SocketException||e is Exception)
            {
                Debug.LogError($"Fail to create a room due to {e}");
            }
        }
        finally
        {
            if (!activeSessions.ContainsKey(roomID))
            {
                string sessionID = GenerateUniqueSessionID();
                var newSession = new GameSession { RoomID = roomID, Password = roomPassword, HostSocket = current, PlayerCharacters = new Dictionary<Socket, string>() };
                activeSessions.Add(roomID, newSession);
                newSession.MemberSockets.Add(current);
                hostClientID = current.RemoteEndPoint.ToString().Split(":")[1];
                SendMessage(current, $"SetHostClientId:{hostClientID}");
                BroadcastMessageToSession(newSession, $"SetHostClientId:{hostClientID}");
                tcpIdToUdpEndpoint[hostClientID] = (IPEndPoint)current.RemoteEndPoint;

                Debug.Log($"The host client id is {hostClientID}");
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
        try
        {
            Debug.Log($"Attempting to join session with room ID: {roomID}, Active Sessions: {string.Join(", ", activeSessions.Keys)}");
            Debug.Log($"Active Sessions count before join attempt: {activeSessions.Count}");

            if (activeSessions.TryGetValue(roomID, out GameSession session))
            {
                if (roomPassword == session.Password)
                {
                    canJoinRoom = true;
                    if (session.HostSocket != current)
                    {
                        session.NonHostSocket = current; // Assign non-host client
                    }
                    session.MemberSockets.Add(current); // Add the client to the room's member list
                    nonHostClientID = current.RemoteEndPoint.ToString().Split(":")[1];
                    SendMessage(current, $"SetNonHostClientId:{nonHostClientID}");
                    BroadcastMessageToSession(session, $"SetNonHostClientId:{nonHostClientID}");
                    tcpIdToUdpEndpoint[nonHostClientID] = (IPEndPoint)current.RemoteEndPoint;

                    Debug.Log($"The non-host client id is {nonHostClientID}");
                    Debug.Log($"Client joined room {roomID}. Number of MemberSockets after joining: {session.MemberSockets.Count}");
                    SendMessage(current, $"JoinRoom Accepted:{roomID}:{roomPassword}");
                    SyncCharacterSelectionState(current, session);
                }
                else
                {
                    // Client provided the wrong password
                    canJoinRoom = false;
                    SendMessage(current, "JoinRoom Request Rejected:Incorrect Password");
                }

            }
            else
            {
               
                canJoinRoom = false;
                SendMessage(current, "JoinRoom Request Rejected:Session does not exist");
            }
        }
        catch (Exception e)
        {
            if (e is SocketException || e is Exception)
            {
                Debug.LogError($"Fail to join room {roomID} due to {e}");
            }
        }
        finally
        {
            Debug.Log($"Attempting to join session with room ID: {roomID}, Active Sessions: {string.Join(", ", activeSessions.Keys)}");
            Debug.Log($"Active Sessions count before join attempt: {activeSessions.Count}");

            if (activeSessions.TryGetValue(roomID, out GameSession session))
            {
                if (roomPassword == session.Password)
                {
                    canJoinRoom = true;
                    if (session.HostSocket != current)
                    {
                        session.NonHostSocket = current; // Assign non-host client
                    }
                    session.MemberSockets.Add(current); // Add the client to the room's member list
                    nonHostClientID = current.RemoteEndPoint.ToString().Split(":")[1];
                    SendMessage(current, $"SetNonHostClientId:{nonHostClientID}");
                    BroadcastMessageToSession(session, $"SetNonHostClientId:{nonHostClientID}");
                    tcpIdToUdpEndpoint[nonHostClientID] = (IPEndPoint)current.RemoteEndPoint;

                    Debug.Log($"The non-host client id is {nonHostClientID}");
                    Debug.Log($"Client joined room {roomID}. Number of MemberSockets after joining: {session.MemberSockets.Count}");
                    SendMessage(current, $"JoinRoom Accepted:{roomID}:{roomPassword}");
                    SyncCharacterSelectionState(current, session);
                }
                else
                {
                    // Client provided the wrong password
                    canJoinRoom = false;
                    SendMessage(current, "JoinRoom Request Rejected:Incorrect Password");
                }

            }
            else
            {

                canJoinRoom = false;
                SendMessage(current, "JoinRoom Request Rejected:Session does not exist");
            }
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
        if (splitData.Length < 1) // Not enough parts to interpret the command.
        {
            Debug.Log($"Message received is {splitData}");
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
                    SynchroniseHostNonhostSelection(current, roomID, characterName);
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
            case "CharacterPrep":
                if (splitData.Length == 3)
                {
                    string roomID = splitData[1];
                    string characterName = splitData[2];

                    InstantiateCharacter(current, roomID, characterName);
                }
                break;

            case "HostMovement":
                             
                Debug.Log($"Host is moving.");
                var session = FindSessionByClient(current);
                if (session != null)
                {
                    BroadcastMessageToSessionMembers(session, text, current);
                }
                break;
            case "NonHostMovement":
                
                Debug.Log($"Non-host is moving.");
                var session0 = FindSessionByClient(current);
                if (session0 != null)
                {
                    BroadcastMessageToSessionMembers(session0, text, current);
                }
                break;
            case "HostAnimated":
                Debug.Log($"Host is animated!");
                var session1 = FindSessionByClient(current);
                if (session1 != null)
                {
                    BroadcastMessageToSessionMembers(session1, text, current);
                }
                break;
            case "NonHostAnimated":
                Debug.Log($"Non-ost is animated!");
                var session2 = FindSessionByClient(current);
                if (session2 != null)
                {
                    BroadcastMessageToSessionMembers(session2, text, current);
                }
                break;

            case "HostAttack":
                Debug.Log($"Playing attacking animations for host player!");
                var session3 = FindSessionByClient(current);
                if (session3 != null)
                {
                    BroadcastMessageToSessionMembers(session3, text, current);
                }
                break;
            case "NonHostAttack":
                Debug.Log($"Playing attacking animations for host player!");
                var session4 = FindSessionByClient(current);
                if (session4 != null)
                {
                    BroadcastMessageToSessionMembers(session4, text, current);
                }
                break;
            case "HostApplyDamage":
                Debug.Log($"Host is applying damage!");
                var session5 = FindSessionByClient(current);
                if (session5 != null)
                {
                    BroadcastMessageToSessionMembers(session5, text, current);
                }
                break;
            case "NonHostApplyDamage":
                Debug.Log($"Non host is applying damage");
                var session6 = FindSessionByClient(current);
                if (session6 != null)
                {
                    BroadcastMessageToSessionMembers(session6, text, current);
                }
                break;
            case "GameEnds":
                Debug.Log("The game ends!");
                var session7 = FindSessionByClient(current);
                if (session7 != null)
                {
                    BroadcastMessageToSessionMembers(session7, text, current);
                }break;
            default:
                Debug.Log($"Unknown command received: {commandType}");
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
    private void NotifyHostClientsOfCharacterSelections(GameSession session)
    {
        string hostCharacter = session.PlayerCharacters[session.HostSocket];
            
        foreach (var clientSocket in session.MemberSockets)
        {
            Debug.Log($"Attempting to notify host character selection for all clients in the session!");
            string message = $"CharacterSelectionsForHost:{hostCharacter}";
            //SendMessage(clientSocket, message);
            BroadcastMessageToSession(session, message);
        }

        
    }
    private void NotifyNonHostClientsOfCharacterSelections(GameSession session)
    {
        string nonHostCharacter = session.PlayerCharacters[session.NonHostSocket];
        foreach (var clientSocket in session.MemberSockets)
        {
            Debug.Log($"Attempting to notify non-host character selection for all clients in the session!");
            string message = $"CharacterSelectionsForNonHost:{nonHostCharacter}";
            //SendMessage(clientSocket, message);
            BroadcastMessageToSession(session, message);
        }
    }
    private void SetHostCharacterReady(Socket current, string roomID, string characterName, bool isReady)
    {
        try
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

                    NotifyHostClientsOfCharacterSelections(session);
                }

            }
            else
            {
                Debug.Log($"Error: Session with ID {roomID} does not exist.");
                SendMessage(current, $"Error:Session with ID {roomID} does not exist.");
            }
        }
        catch( Exception e )
        {
            if(e is Exception)
            {
                Debug.LogError($"Failed to set host player ready due to {e}.");
            }
        }
        finally
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

                    NotifyHostClientsOfCharacterSelections(session);
                }

            }
            else
            {
                Debug.Log($"Error: Session with ID {roomID} does not exist.");
                SendMessage(current, $"Error:Session with ID {roomID} does not exist.");
            }
        }
      
    }
    
    private void SetNonHostCharacterReady(Socket current, string roomID, string characterName, bool isReady)
    {
        try
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

                    NotifyNonHostClientsOfCharacterSelections(session);

                }

            }
        }
        catch(Exception e)
        {
            if(e is Exception||e is SocketException)
            {
                Debug.LogError($"Failed to set non-host player ready because of {e}.");
            }
        }
        finally
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

                    NotifyNonHostClientsOfCharacterSelections(session);

                }

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
        /*Without error check*/
        /* foreach (var client in session.MemberSockets)
        {
            SendMessage(client, message);
        }
        // Also send to host if not included in MemberSockets
        if (!session.MemberSockets.Contains(session.HostSocket))
        {
            SendMessage(session.HostSocket, message);
        }*/


        /*With error check*/

        List<Socket> failedSockets = new List<Socket>();

        foreach (var client in session.MemberSockets)
        {
            if (!SendMessageWithRetry(client, message))
            {
                failedSockets.Add(client);
            }

        }

        // Also send to host if not included in MemberSockets
        if (!session.MemberSockets.Contains(session.HostSocket))
        {
            if (!SendMessageWithRetry(session.HostSocket, message))
            {
                failedSockets.Add(session.HostSocket);
            }
        }

        // Handle failed sockets
        foreach (var failedSocket in failedSockets)
        {
            HandleFailedSocket(failedSocket);
        }
    }
    private bool SendMessageWithRetry(Socket socket, string message, int retryCount = 10) //Re-send the messages for char instantiation when it fails
    {
        int attempts = 0;
        while (attempts < retryCount)
        {
            try
            {
                SendMessage(socket, message);
                return true; // Message sent successfully
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send message to {socket.RemoteEndPoint}. Attempt {attempts + 1}/{retryCount}. Error: {ex.Message}");
                attempts++;
            }
        }

        return false; // Message sending failed after retries
    }
    private void HandleFailedSocket(Socket failedSocket)
    {
        // Log the error
        Debug.LogError($"Message sending failed for {failedSocket.RemoteEndPoint}. Removing the client from the session.");

        // Remove the client from the session
        RemoveClientFromSession(failedSocket);

        // Optionally, notify other clients in the session about the disconnection
        NotifyClientsOfDisconnection(failedSocket);

        // Close the socket
        CloseClientSocket(failedSocket);
    }

    private void RemoveClientFromSession(Socket clientSocket)
    {
        foreach (var session in activeSessions.Values)
        {
            if (session.MemberSockets.Contains(clientSocket))
            {
                session.MemberSockets.Remove(clientSocket);
                Debug.Log($"Client {clientSocket.RemoteEndPoint} removed from session {session.RoomID}");
                break; // Assuming a client is only part of one session
            }
        }
    }

    private void NotifyClientsOfDisconnection(Socket disconnectedSocket)
    {
        string disconnectMessage = $"ClientDisconnected:{disconnectedSocket.RemoteEndPoint}";
        foreach (var session in activeSessions.Values)
        {
            if (session.MemberSockets.Contains(disconnectedSocket))
            {
                foreach (var client in session.MemberSockets)
                {
                    SendMessageWithRetry(client, disconnectMessage);
                }
                break; // Assuming a client is only part of one session
            }
        }
    }

    private void CloseClientSocket(Socket clientSocket)
    {
        try
        {
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            Debug.Log($"Closed connection with {clientSocket.RemoteEndPoint}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error closing socket {clientSocket.RemoteEndPoint}: {ex.Message}");
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
    private void SynchroniseHostNonhostSelection(Socket current, string roomID, string characterName)
    {
        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
            Debug.Log($"Current Socket: {current.RemoteEndPoint}, Host Socket: {session.HostSocket.RemoteEndPoint}");

            if (!IsHost(current,roomID))
            {
                string message = $"ProcessCharacterSelectionRequest:{roomID}:{characterName}:{current.RemoteEndPoint.ToString()}";
                SendMessage(session.HostSocket, message);                
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
    public GameSession FindSessionByClient(Socket clientSocket)
    {
        // Iterate through all active game sessions
        foreach (var sessionEntry in activeSessions)
        {
            var session = sessionEntry.Value;

            // Check if the client socket is the host of the session
            if (session.HostSocket == clientSocket||session.NonHostSocket==clientSocket)
            {
                return session;
            }

            // Alternatively, check if the client socket is one of the members (non-host players) of the session
            if (session.MemberSockets.Contains(clientSocket))
            {
                return session;
            }
        }

        // Return null if the client socket is not part of any session
        return null;
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
        return activeSessions.TryGetValue(roomID, out var session) && current==session.HostSocket;
    }
    #endregion

    #region Start Game Logics
    private void StartGameAndLoadAssignGameCharacters(Socket current, string roomID, string characterName)
    {
        //StartUDPServer(); // Start the UDP server
        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
            Debug.Log($"The game has started and characters are assigned to the individual player");

            string startGameMsg = $"GameHasStarted:{roomID}:{characterName}";

            //SendMessage(current, startGameMsg);
            BroadcastMessageToSession(session, startGameMsg);

        }
            
    }


    private void InstantiateCharacter(Socket current, string roomID, string characterName)
    {
        try
        {
            if (activeSessions.TryGetValue(roomID, out GameSession session))
            {
               
                Debug.Log($"Instantiating {characterName} for the host in room {roomID}!");
                string instantiationMsgForHost = $"InstantiateCharacterForHost:{roomID}:{characterName}";
                BroadcastMessageToSession(session, instantiationMsgForHost);

                Debug.Log($"Instantiating {characterName} for the non-host client in room {roomID}!");
                string instantiationMsgForNonHost = $"InstantiateCharacterForNonHost:{roomID}:{characterName}";
                BroadcastMessageToSession(session, instantiationMsgForNonHost);

            }

        }
        catch(Exception e)
        {
            if(e is Exception)
            {
                Debug.LogError($"Character instantiation failed due to {e}.");
            }
        }
        finally
        {
            if (activeSessions.TryGetValue(roomID, out GameSession session))
            {
                
                Debug.Log($"Instantiating {characterName} for the host in room {roomID}!");
                string instantiationMsgForHost = $"InstantiateCharacterForHost:{roomID}:{characterName}";
                BroadcastMessageToSession(session, instantiationMsgForHost);

                Debug.Log($"Instantiating {characterName} for the non-host client in room {roomID}!");
                string instantiationMsgForNonHost = $"InstantiateCharacterForNonHost:{roomID}:{characterName}";
                BroadcastMessageToSession(session, instantiationMsgForNonHost);

               

            }
        }
       
    }
    
    private void ResendInstantiationRequest(Socket current, string roomID, string characterName) //Please reflect this in the client side so that it ensures re-sending the character instantiation messages
    {

        if (activeSessions.TryGetValue(roomID, out GameSession session))
        {
            Debug.Log($"Attempting to reinstantiate characters in {roomID}");
            string resendInstantiationMsg = $"ReSendInstantiation:{roomID}:{characterName}";
            BroadcastMessageToSession(session, resendInstantiationMsg);
        }

        Debug.Log($"Resent instantiation message for {characterName} in room {roomID}");
    }


    #endregion

    #region In-game Logics

   
   
    #endregion
    private void CloseAllSockets()
    {
        foreach (Socket socket in clientSockets)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        tcpServerSocket.Close();
       
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
