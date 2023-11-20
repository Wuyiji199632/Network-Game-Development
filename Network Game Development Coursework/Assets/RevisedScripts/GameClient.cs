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


public class GameClient : MonoBehaviour
{
    public static GameClient Instance;
    private string roomID;
    private Socket clientSocket;
    private const int BUFFER_SIZE = 2048;
    private byte[] buffer = new byte[BUFFER_SIZE];
    private string welcomeMsg = "Welcome to the Game Server";
    [SerializeField]
    private string serverIp = "127.0.0.1"; //Test with a fixed ip address, we will generate an ip address later on for more robust connection or get the ip address the current network is connected to
    [SerializeField]
    private int serverPort = 7777;
    [SerializeField]
    private Button startGameBtn, quitGameBtn, createRoomBtn, joinRoomBtn, createRoomBtn2, joinRoomBtn2, mainMenuBtn;
    private bool receivedDebugMessage = false;
    [SerializeField]
    private InputField createSessionIDField, createSessionPasswordField, joinSessionIDField, joinSessionPasswordField;
    [SerializeField]
    private UnityEngine.UI.Text gameNameTxt,createRoomTxt,joinRoomTxt,readyTxt,opponentReadyTxt;

    public GameServer gameServer;

    public Button heavyBanditBtn, lightBanditBtn;

    public Canvas canvas1, canvas2;
    

    private Dictionary<Socket, List<byte>> clientMessageBuffers = new Dictionary<Socket, List<byte>>();//Ensure dynamic change for buffer sizes

    public GameObject afterSelectionBtn;

    private string selectedCharacter = null;

    private bool isReady = false;

    private string localClientId=string.Empty;

    private bool localClientReady = false;
    private bool opponentReady = false;

    private void Awake()
    {
       
        DontDestroyOnLoad(this);
    }
    void Start()
    {
        afterSelectionBtn.SetActive(false);
        opponentReadyTxt.gameObject.SetActive(false);
        opponentReadyTxt.text = string.Empty;
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
            clientSocket.BeginConnect(serverIp, serverPort, ConnectCallback, clientSocket);
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

        if (message.StartsWith("SetClientId:"))
        {
            string[] splitMessage= message.Split(':');
            localClientId = message.Substring("SetClientId:".Length);
            Debug.Log($"Client ID set: {localClientId}");
            return; // Exit the method to avoid processing the rest of the function
        }



        if (message.StartsWith("ProcessCharacterSelectionRequest:"))//Non-host client
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
        Debug.Log($"Client Received Message: {message}");
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
                Debug.Log($"Joined room with ID: {roomID}");
            }
        }
        if (message.StartsWith("CharacterReadyUpdate:"))
        {
            string[] splitMessage = message.Split(':');
            if (splitMessage.Length >= 5)
            {
                string roomID = splitMessage[1];
                string characterName = splitMessage[2];
                string readinessFlag = splitMessage[3];
                string clientIdentifier = splitMessage[4];
                //bool isForLocalClient = (IsHost() && readinessFlag == "Host") || (!IsHost() && readinessFlag == "Member");
                // Ensure the update is for the opponent and not the local client
                UpdateOpponentReadinessInfo(characterName, readinessFlag, clientIdentifier);

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
    private void UpdateLocalClientReadinessUI(string characterName, string readinessFlag)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            opponentReadyTxt.text = string.Empty;
            readyTxt.color = readinessFlag == "Ready" ? Color.green : Color.red;
        });
    }

    // Update the readiness UI for the opponent client
    private void UpdateOpponentReadinessInfo(string characterName, string readinessFlag, string clientIdentifier)
    {

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            bool isOpponentReady = readinessFlag == "Ready";
            opponentReadyTxt.gameObject.SetActive(isOpponentReady);
            opponentReadyTxt.text = clientIdentifier != localClientId ? $"Opponent is ready with character {characterName}" : string.Empty;
        });
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
        canvas1.gameObject.SetActive(false);
        canvas2.gameObject.SetActive(true);
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
        string message = IsHost() ? $"CharacterSelectionUpdate:{roomID}:{characterName}" : $"ProcessCharacterSelectionRequest:{roomID}:{characterName}";
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
             // This will clear the text on the main thread
            
            if (!isReady)
            {
                isReady = true;
                if (!string.IsNullOrEmpty(selectedCharacter))
                {
                    string msg = $"CharacterIsReady:{roomID}:{selectedCharacter}";
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
                    string msg = $"CharacterCancelReadiness:{roomID}:{selectedCharacter}";
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
    private bool IsHost()
    {
        // Compare the client's socket with the host's socket in the session
        if (gameServer == null || string.IsNullOrEmpty(roomID))
            return false;

        if (!gameServer.activeSessions.TryGetValue(roomID, out var session))
            return false;

        return clientSocket == session.HostSocket;
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
}

    
