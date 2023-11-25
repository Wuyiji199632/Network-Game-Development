using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine.SceneManagement;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using System.Linq;


public class GameClient : MonoBehaviour //This is the class specifying the use of tcp-ip
{
    public static GameClient Instance;
    private string roomID,roomPassword;
    private Socket clientSocket;
    private const int BUFFER_SIZE = 2048;
    private byte[] buffer = new byte[BUFFER_SIZE];
    private string welcomeMsg = "Welcome to the Game Server";
    [SerializeField]
    private string serverIp = "127.0.0.1"; //Test with a fixed ip address, we will generate an ip address later on for more robust connection or get the ip address the current network is connected to
    [SerializeField]
    private int serverPortTCP = 8888;
    [SerializeField]
    private int serverPortUDP = 8889;
    [SerializeField]
    private Button startGameBtn, quitGameBtn, createRoomBtn, joinRoomBtn, createRoomBtn2, joinRoomBtn2, mainMenuBtn;
    private bool receivedDebugMessage = false;
    [SerializeField]
    private InputField createSessionIDField, createSessionPasswordField, joinSessionIDField, joinSessionPasswordField;
    [SerializeField]
    private UnityEngine.UI.Text gameNameTxt, createRoomTxt, joinRoomTxt, readyTxt,waitForTxt,unableToJoinTxt;
    [SerializeField]
    private Button gameStartBtn;
    public GameServer gameServer;

    public Button heavyBanditBtn, lightBanditBtn;

    public Canvas canvas1, canvas2,inGameCanvas;
    

    private Dictionary<Socket, List<byte>> clientMessageBuffers = new Dictionary<Socket, List<byte>>();//Ensure dynamic change for buffer sizes

    public GameObject afterSelectionBtn;

    private string selectedCharacter = null;

    private bool isReady = false;

    //public string localClientId=string.Empty;
    public string localHostClientId=string.Empty,localNonHostClientId=string.Empty;

    private bool isHost=false;

    private bool hostIsReady=false, nonHostIsReady=false;

    public GameObject lightBandit, heavyBandit;

    public Transform spawnPoint;

    private bool instantiateNonhostCharForHost = false, instantiateHostForNonhost = false;

    private string g_selectedHostCharacter=string.Empty,g_selectedNonHostCharacter=string.Empty; // Store the global host and non-host characters

    private bool isSocketConnected = false;
    #region UDP variables

    private UdpClient udpClient;
    private IPEndPoint udpServerEndPoint;
   
    #endregion
    private void Awake()
    {
       
        DontDestroyOnLoad(this);

       
    }
    void Start()
    {
        afterSelectionBtn.SetActive(false);
        waitForTxt.gameObject.SetActive(true);
        unableToJoinTxt.gameObject.SetActive(false);
        //opponentReadyTxt.gameObject.SetActive(false);
        //opponentReadyTxt.text = string.Empty;
    }
    private void Update()
    {
        //Change UI locally for the title texts

        if(SceneManager.GetActiveScene().ToString()=="MainMenu")
        if (gameServer.isRunning)
        {
            gameNameTxt?.gameObject.SetActive(false);
            createRoomTxt?.gameObject.SetActive(true);
        }         
    }
  
    public void ConnectToServer()
    {
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            clientSocket.BeginConnect(serverIp, serverPortTCP, ConnectCallback, clientSocket);
            //SceneManager.LoadScene("RoomCreationPage");
           
        }
        catch (SocketException ex)
        {
            Debug.Log(ex.Message);
        }
    }

    private void ConnectCallback(IAsyncResult AR)
    {
        try
        {
            clientSocket.EndConnect(AR);
            Debug.Log("Connected to the server.");
            clientSocket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, clientSocket);
        }
        catch (SocketException ex)
        {
            Debug.Log(ex.Message);
        }
    }

    private void ReceiveCallback(IAsyncResult AR)
    {
        int received;
        Socket current = (Socket)AR.AsyncState;

        try
        {
            received = current.EndReceive(AR);
            if (received == 0)
            {
                Debug.Log("Server closed connection");
                return;
            }

            // Process the received data
            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);
            Debug.Log($"Client Received: {text} from server");

            UnityMainThreadDispatcher.RunOnMainThread(() => OnServerMessageReceived(text));
        }
        catch (SocketException ex)
        {
            Debug.Log("Server closed connection: " + ex.Message);
            return;
        }
        finally
        {
            // Clear the buffer for accepting following messages
            Array.Clear(buffer, 0, BUFFER_SIZE);

            // Continue listening for new data from the server.
            if (current != null && current.Connected)
            {
                current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
            }
        }
       

    }
    private void OnServerMessageReceived(string message)
    {
        Debug.Log("Server Broadcast: " + message);

        if (message.StartsWith("SetHostClientId:"))
        {
            string[] splitMessage= message.Split(':');
            localHostClientId = gameServer.hostClientID;
            isHost = true;
            Debug.Log($"Host Client ID set: {localHostClientId}");
          
            return; // Exit the method to avoid processing the rest of the function
        }else if (message.StartsWith("SetNonHostClientId"))
        {
            string[] splitMessage = message.Split(':');
            localNonHostClientId = gameServer.NonHostClientID;
            isHost = false;
            Debug.Log($"Non Host Client ID set: {localNonHostClientId}");
          
            return; // Exit the method to avoid processing the rest of the function
        }



        if (message.StartsWith("ProcessCharacterSelectionRequest:"))//Non-host client's selection message
        {
            // the host should receive and process this request
            string[] splitMessage = message.Split(':');
            if (splitMessage.Length >= 4)
            {
                string roomID = splitMessage[1];
                string characterName = splitMessage[2];
                string requestingClientEndpoint = splitMessage[3];
              
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    UpdateCharacterSelectionUI(characterName, true);
                });
                Debug.Log($"Character {characterName} selected, button disabled.");

            }
            
        }
       
        if (message.StartsWith("CharacterSelectionUpdate:"))// Host client
        {
            string[] messageParts = message.Split(':');
            if (messageParts.Length >= 3)
            {
                string roomID = messageParts[1];
                string characterName = messageParts[2];

                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    UpdateCharacterSelectionUI(characterName, true);
                });
            }

        }
        
        if (message.StartsWith("CharacterSelectionCancelled:"))
        {
            string[] splitMessage = message.Split(':');
            if (splitMessage.Length >= 3)
            {
                string cancelledCharacter = splitMessage[2];
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    UpdateCharacterSelectionUI(cancelledCharacter, false);
                });
            }
        }

        if (message.StartsWith("SessionClosed:"))
        {
            string closedRoomID = message.Split(':')[1];
            if (roomID == closedRoomID)
            {
                Debug.Log($"Session {roomID} has been successfully closed.");
                roomID = null; 
            }
            ResetCharacterSelectionUI();
        }

        if (message.StartsWith("RoomCreated:"))
        {
            string[] splitMessage = message.Split(':');
            roomID = splitMessage[1]; // Store the sessionID
            Debug.Log($"Room created with room ID: {roomID}");
            // Update UI to show room as created or navigate to room screen
        }
        if (message.StartsWith("JoinRoom Accepted"))
        {
            // Assuming the message format is "JoinRoom Accepted:ROOM_ID"
            string[] splitMessage = message.Split(':');
            if (splitMessage.Length > 1)
            {
                roomID = splitMessage[1];
                roomPassword = splitMessage[2];
                canvas1.gameObject.SetActive(false);
                canvas2.gameObject.SetActive(true);
                Debug.Log($"Joined room with ID and Password: {roomID} and {roomPassword}");
            }
        }
        if(message.StartsWith("JoinRoom Request Rejected:"))
        {
          

            if (!(string.IsNullOrEmpty(joinSessionIDField.text) && string.IsNullOrEmpty(joinSessionPasswordField.text)))
            {
                unableToJoinTxt.gameObject.SetActive(true);
                unableToJoinTxt.text = "Please enter a valid room id and password!";
            }
            else
            {
                unableToJoinTxt.gameObject.SetActive(true);
                unableToJoinTxt.text = "room id and password can't be null!";
            }
           

            Debug.Log($"You can't join the room because the id-password you enter is incorrect!");
        }
        if (message.StartsWith("HostCharacterReadyUpdate:")) //Rediness update for host
        {
            string[] splitMessage = message.Split(':');
            if (splitMessage.Length >= 6)
            {
                string roomID = splitMessage[1];
                string characterName = splitMessage[2];
                string readinessFlag = splitMessage[3];
                string memberIdentityFlag= splitMessage[4];
                string clientIdentifier = splitMessage[5];                        
                UpdateHostReadinessInfo(characterName, readinessFlag,clientIdentifier);
               
                Debug.Log($"Host client is ready!");
             
            }
          
        }else if (message.StartsWith("NonHostCharacterReadyUpdate:"))
        {
            string[] splitMessage = message.Split(':');
            if (splitMessage.Length >= 6)
            {
                Debug.Log($"Hadnling UI elements for non host!");

                string roomID = splitMessage[1];
                string characterName = splitMessage[2];
                string readinessFlag = splitMessage[3];
                string memberIdentityFlag = splitMessage[4];
                string clientIdentifier = splitMessage[5];
                UpdateNonHostReadinessInfo(characterName, readinessFlag, clientIdentifier);
               
                Debug.Log($"Non-host client is ready!");
            }
        }

        if (message.StartsWith("GameHasStarted:"))
        {
            string[] splitMessage = message.Split(':');

            if (splitMessage.Length >= 3)
            {
                string roomID = splitMessage[1];
                string characterName = splitMessage[2];
                Debug.Log($"Loading game scene and assigning characters in room {roomID}, current selected character is {characterName}");
                canvas1.gameObject.SetActive(false);
                canvas2.gameObject.SetActive(false);
                inGameCanvas.gameObject.SetActive(true);
             
                //Instantiate characters based on the type
            }
               
        }

        if (message.StartsWith("CharacterSelectionsForHost:"))
        {
            string[] splitMessage = message.Split(":");

            if (splitMessage.Length >= 2)
            {
                string hostCharacter = splitMessage[1];                
                g_selectedHostCharacter = hostCharacter;
                Debug.Log($"The selected host character is {hostCharacter}");
                instantiateNonhostCharForHost = true;
                Vector3 spawnPos = spawnPoint.position;
                spawnPos.z = 0;
                Debug.Log($"Host is {hostCharacter}");

               

            }
        }
        else if (message.StartsWith("CharacterSelectionsForNonHost:"))
        {
            string[] splitMessage = message.Split(":");

            if (splitMessage.Length >= 2)
            {
               
                string nonHostCharacter = splitMessage[1];
                g_selectedNonHostCharacter = nonHostCharacter;
                Debug.Log($"The selected non-host character is {nonHostCharacter}");
                instantiateHostForNonhost = true;
                Vector3 spawnPos = spawnPoint.position;
                spawnPos.z = 0;
                Debug.Log($"Non-host is {nonHostCharacter}.");
              
            }
        }
      
        if (message.StartsWith("InstantiateCharacterForHost:"))
        {
            
            string[] splitMessage = message.Split(':');

            if (splitMessage.Length >= 2)
            {
               
                if (instantiateNonhostCharForHost)
                {
                    instantiateNonhostCharForHost = false;
                    string roomID = splitMessage[1];
                    splitMessage[2] = g_selectedHostCharacter;
                    string characterName = splitMessage[2];
                    Vector3 spawnPos = spawnPoint.position;
                    spawnPos.z = 0;
                    Debug.Log($"Instantiate character for host.The host character is {characterName}.");
                  
                    switch (g_selectedHostCharacter)
                    {
                        case "LightBandit":
                            Instantiate(lightBandit, spawnPos, Quaternion.identity);
                            break;
                        case "HeavyBandit":
                            Instantiate(heavyBandit, spawnPos, Quaternion.identity);
                            break;
                            default :break;
                    }
                                     
                }
              
            }
        }
        else if (message.StartsWith("InstantiateCharacterForNonHost:"))
        {
           
            string[] splitMessage = message.Split(':');

            if (splitMessage.Length >= 3)
            {
               

                if (instantiateHostForNonhost)
                {
                    instantiateHostForNonhost = false;
                    string roomID = splitMessage[1];
                    splitMessage[2] = g_selectedNonHostCharacter;
                    string characterName = splitMessage[2];
                    Vector3 spawnPos = spawnPoint.position;
                    spawnPos.z = 0;
                    Debug.Log($"Instantiate character for non host! the non-host character is{characterName}");
                   
                    switch (g_selectedNonHostCharacter)
                    {
                        case "LightBandit":
                            Instantiate(lightBandit, spawnPos, Quaternion.identity);
                            break;
                        case "HeavyBandit":
                            Instantiate(heavyBandit, spawnPos, Quaternion.identity);
                            break;
                    }
                                    
                }
               
            }
        }

        // Add any logics needed when a message is received here
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
           
            OnScreenConsole.Log(message);
        });

        if (message.StartsWith("HostDisconnected:"))
        {
            string sessionID = message.Split(':')[1];
            // Handle the host disconnection here, such as updating the UI and navigating back to the lobby
        }
        else if (message.StartsWith("CLIENT DISCONNECTED:"))
        {
            string disconnectedClient = message.Substring("CLIENT DISCONNECTED:".Length);
            HandleClientDisconnection(disconnectedClient);
        }

    }
    
    // Update the readiness UI for the local client
   
    // Update the readiness UI for the opponent client
    private void UpdateHostReadinessInfo(string characterName, string readinessFlag,string clientIdentifier)
    {
        if (!isHost) return;
        bool isOpponentReady = readinessFlag == "Ready";
        hostIsReady = isOpponentReady;
       
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            
            gameStartBtn.gameObject.SetActive(isOpponentReady);
            //waitForHostTxt.gameObject.SetActive(isOpponentReady);
            waitForTxt.text = $"You are the host!";
            Debug.Log($"Current client identifier is {clientIdentifier}, current local client id is {localHostClientId}");


        });
    }
    private void UpdateNonHostReadinessInfo(string characterName, string readinessFlag, string clientIdentifier)
    {
        if (!isHost)
        {
            bool isOpponentReady = readinessFlag == "Ready";
            nonHostIsReady = isOpponentReady;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
               
                waitForTxt.gameObject.SetActive(isOpponentReady);             
                waitForTxt.text = $"Wating for the host to start...";
                Debug.Log($"Current client identifier is {clientIdentifier}, current local client id is {localNonHostClientId}");

            });
        }
       

    }
   
    public bool IsLocal()
    {
        return localHostClientId == gameServer.hostClientID || localNonHostClientId == gameServer.NonHostClientID;
    }
    private void HandleClientDisconnection(string clientInfo)
    {
        // Update the UI to remove the disconnected client
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {

            // This code runs on the main thread
            Debug.Log(clientInfo + " has left the game.");
            OnScreenConsole.Log(clientInfo + " has left the game.");
            // Here you can update your client list or lobby UI
            // ...
        });
    }
    public void DisconnectFromServer()
    {
        if (clientSocket != null && clientSocket.Connected)
        {
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
        }
        ResetCharacterSelectionUI();
        

    }
    #region Session Generation Logics

    // Call this method when you want to create a room.
    public void SendCreateRoomRequest()
    {
        ResetCharacterSelectionUI();
        string roomID = createSessionIDField.text;
        string roomPassword = createSessionPasswordField.text;
        string message = $"CreateRoom:{roomID}:{roomPassword}";
        canvas1.gameObject.SetActive(false);
        canvas2.gameObject.SetActive(true);
        SendMessageToServer(message);
    }

    // Call this method when you want to join a room.
    public void SendJoinRoomRequest()
    {
        
        ResetCharacterSelectionUI();
        string roomID = joinSessionIDField.text;
        string roomPassword = joinSessionPasswordField.text;
        string message = $"JoinRoom:{roomID}:{roomPassword}";
        SendMessageToServer(message);

                   
    }

    // This method sends a message to the server.
    private void SendMessageToServer(string message)
    {
        if (clientSocket != null && clientSocket.Connected)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            clientSocket.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, null);
            Debug.Log($"Attempting to send message: {message}");
           
        }
        else
        {
            Debug.LogError("Cannot send data, socket is not connected!");            
            // You could also call ConnectToServer() here to try to reconnect
        }
    }

    private void SendCallback(IAsyncResult AR)
    {
        try
        {
            // Complete sending the data to the server.
            int bytesSent = clientSocket.EndSend(AR);
            Debug.Log($"Sent {bytesSent} bytes to server. Message sent successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to send data to server: {e.Message}");
           
        }
    }
    #endregion


    #region Character Selection Logics

    public void SelectCharacter(string characterName)
    {
        string message = $"ProcessCharacterSelectionRequest:{roomID}:{characterName}";
        Debug.Log($"Attempting to send character selection message: {message}");
        selectedCharacter = characterName;
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            UpdateCharacterSelectionUI(characterName, true); //Ensure the local client's button is disabled
            afterSelectionBtn.SetActive(true);
        });
        SendMessageToServer(message);
    }

    public void CancelSelection()
    {
        if (!string.IsNullOrEmpty(selectedCharacter))
        {
            string message = $"CharacterCancelSelection:{roomID}:{selectedCharacter}";
            SendMessageToServer(message);
            Debug.Log($"Attempting to send character selection cancellation message: {message}");
            UpdateCharacterSelectionUI(selectedCharacter, false);
            selectedCharacter = null; // Reset selected character
            afterSelectionBtn.SetActive(false); // Hide the cancel button
        }
    }

    public void GetReadyForBattle()//TODO:Handle the non-host passing of readiness messages
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            gameStartBtn.gameObject.SetActive(false);
            if (!isReady)
            {
                isReady = true;
                if (!string.IsNullOrEmpty(selectedCharacter))
                {
                    string msg =$"ProcessReadiness:{roomID}:{selectedCharacter}";
                    SendMessageToServer(msg);
                    Debug.Log($"Attempting to send character ready message: {msg}");
                    readyTxt.color = Color.green;
                    // Other UI updates related to readiness can be added here
                }
            }
            else
            {
                isReady = false;
                if (!string.IsNullOrEmpty(selectedCharacter))
                {
                    string msg = $"ProcessCacelReadiness:{roomID}:{selectedCharacter}";
                    SendMessageToServer(msg);
                    Debug.Log($"Attempting to send cancel readiness message: {msg}");
                    readyTxt.color = Color.red;
                    // Other UI updates related to canceling readiness can be added here
                }
            }
            

        });
    }

   
    private void UpdateCharacterSelectionUI(string characterName, bool isSelected)
    {
        if(isSelected)
        {
            switch (characterName)
            {
                case "HeavyBandit":
                    heavyBanditBtn.interactable = false;
                    break;
                case "LightBandit":
                    lightBanditBtn.interactable = false;
                    break;
                    // Add cases for other characters
            }
        }
        else
        {
            switch (characterName)
            {
                case "HeavyBandit":
                    heavyBanditBtn.interactable = true;
                    break;
                case "LightBandit":
                    lightBanditBtn.interactable = true;
                    break;
                    // Add cases for other characters
            }
        }
        Debug.Log($"Updating UI for character {characterName} - Selected: {isSelected}");

    }
    private bool IsLocalClient(string clientInfo)
    {
        return clientSocket.RemoteEndPoint.ToString() == clientInfo;
    }
    private void ResetCharacterSelectionUI()
    {
        // Reactivating all character buttons
        heavyBanditBtn.interactable = true;
        lightBanditBtn.interactable = true;
        // Add reactivation for other character buttons
    }

    #endregion



    #region Start & In-game logics

    public void StartGameAndPrepareCharacters()
    {
        Debug.Log("Game started!");
        StartUDPClient();
        if (nonHostIsReady)
        {
            if (!string.IsNullOrEmpty(selectedCharacter))
            {
                string startGameMsg = $"HostStartGame:{roomID}:{selectedCharacter}";
                string charPrepMsg = $"CharacterPrep:{roomID}:{selectedCharacter}";
                SendMessageToServer(startGameMsg);
                SendMessageToServer(charPrepMsg);
                Debug.Log($"Attampting to send start game message {startGameMsg}");
            }
        }

        else
        {
            Debug.Log($"Non host client isn't ready yet, failed to start!");
            waitForTxt.text = $"Please wait for another player to get ready!";
            waitForTxt.gameObject.SetActive(true);
        }
       
    }

    #endregion

    public void GoToRoomCreationPage()
    {
        startGameBtn.gameObject.SetActive(false);
        quitGameBtn.gameObject.SetActive(false);
        createRoomBtn.gameObject.SetActive(true);
        joinRoomBtn.gameObject.SetActive(true);
        mainMenuBtn.gameObject.SetActive(true);
    }
    public void GoToJoinRoomPage()
    {
        startGameBtn.gameObject.SetActive(false);
        quitGameBtn.gameObject.SetActive(false);
        createRoomBtn.gameObject.SetActive(false);
        joinRoomBtn.gameObject.SetActive(false);
        createRoomBtn2.gameObject.SetActive(false);
        joinRoomBtn2.gameObject.SetActive(true);
        joinSessionIDField.gameObject.SetActive(true);
        joinSessionPasswordField.gameObject.SetActive(true);
        mainMenuBtn.gameObject.SetActive(true);
    }
    public void BackToMenuUIChange()
    {
        canvas1.gameObject.SetActive(true);
        canvas2.gameObject.SetActive(false);
        startGameBtn.gameObject.SetActive(true);
        quitGameBtn.gameObject.SetActive(true);
        createRoomBtn.gameObject.SetActive(false);
        gameServer.createRoomBtn2.gameObject.SetActive(false);
        gameServer.createSessionIDField.gameObject.SetActive(false);
        gameServer.createSessionPasswordField.gameObject.SetActive(false);
        joinSessionIDField.gameObject.SetActive(false);
        joinSessionPasswordField.gameObject.SetActive(false);
        joinRoomBtn.gameObject.SetActive(false);
        joinRoomBtn2.gameObject.SetActive(false);
        mainMenuBtn.gameObject.SetActive(false);

        
    }
   
    private void OnApplicationQuit()
    {
        if (clientSocket != null && clientSocket.Connected)
        {
            string quitGameCommand = "QuitGame";
            string message = $"{quitGameCommand}:{roomID}";
            SendMessageToServer(message);

            // Consider a slight delay or a confirmation mechanism here
            // to ensure the message is sent before the application quits
        }

      
        DisconnectFromServer();
    }
    public void QuitGame()
    {
        string quitGameCommand = "QuitGame";
        string message = $"{quitGameCommand}:{roomID}";
        SendMessageToServer(message);      
        UnityEngine.Application.Quit();
        DisconnectFromServer();
        
    }

    public void GoBackToMainMenu()
    {
        string toMenuCommand = "BackToMenu";
        string message= $"{toMenuCommand}:{roomID}";
        SendMessageToServer(message);       
        DisconnectFromServer();
        ResetCharacterSelectionUI();

    }

    #region Set up UDP client
    public void StartUDPClient()
    {
        udpClient = new UdpClient();
        udpServerEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPortTCP); // Use the server's IP and UDP port
    }

    public void SendUDPMessage(string message)
    {
        byte[] data = Encoding.ASCII.GetBytes(message);
        udpClient.Send(data, data.Length, udpServerEndPoint);
        Debug.Log("UDP message sent: " + message);
    }

    // Optional: Implement a method to receive UDP data if needed
    public void StartReceivingUDP()
    {
        udpClient.BeginReceive(ReceiveUDP, null);
    }

    private void ReceiveUDP(IAsyncResult result)
    {
        byte[] receivedData = udpClient.EndReceive(result, ref udpServerEndPoint);
        // Process received data...

        // Restart listening for UDP data
        udpClient.BeginReceive(ReceiveUDP, null);
    }
    public string GetLocalPlayerId()
    {
        // Generate a unique identifier for this client
        return Guid.NewGuid().ToString();
    }
    #endregion
}


