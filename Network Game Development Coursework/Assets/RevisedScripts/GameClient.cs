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
    private UnityEngine.UI.Text gameNameTxt,createRoomTxt,joinRoomTxt;

    public GameServer gameServer;

    public Button heavyBanditBtn, lightBanditBtn;

    public Canvas canvas1, canvas2;
    public Button startGameButton;

    private Dictionary<Socket, List<byte>> clientMessageBuffers = new Dictionary<Socket, List<byte>>();//Ensure dynamic change for buffer sizes
    private void Awake()
    {
       
        DontDestroyOnLoad(this);
    }
    void Start()
    {

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

        /*if (message == "ShowStartGameButton")
        {
            UnityMainThreadDispatcher.RunOnMainThread(() =>
            {
                // Code to show the "Start Game" button
                startGameButton.gameObject.SetActive(true);
            });
        }*/


        if (message.StartsWith("ProcessCharacterSelectionRequest:"))
        {
            // Only the host should receive and process this request
            if (IsHost())
            {
                string[] splitMessage = message.Split(':');
                if (splitMessage.Length >= 4)
                {
                    string roomID = splitMessage[1];
                    string characterName = splitMessage[2];
                    string requestingClientEndpoint = splitMessage[3];
                    ProcessCharacterSelectionRequest(roomID, characterName, requestingClientEndpoint);

                    Debug.Log($"Character {characterName} selected, button disabled.");

                }
            }
        }
        if (message.StartsWith("CharacterSelectionConfirmed:"))
        {
            string[] messageParts = message.Split(':');
            if (messageParts.Length >= 3)
            {
                string selectedCharacter = messageParts[2];
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    UpdateCharacterSelectionUI(selectedCharacter, true);
                    Debug.Log($"Character {selectedCharacter} selected, button disabled.");
                });
            }
        }
        else if (message.StartsWith("CharacterSelectionFailed:"))
        {
            string characterName = message.Split(':')[1];
            UnityMainThreadDispatcher.RunOnMainThread(() =>
            {
                // Optionally update UI to indicate failure
                UpdateCharacterSelectionUI(characterName, false); // reset the UI
                Debug.Log($"Failed to select {characterName}. It is already selected by another player.");
            });
        }
       


        if (message.StartsWith("CharacterSelectionUpdate:"))
        {
            string[] messageParts = message.Split(':');
            if (messageParts.Length >= 3)
            {
                string selectingClientInfo = messageParts[1];
                string selectedCharacter = messageParts[2];
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    UpdateCharacterSelectionUI(selectedCharacter, true);
                });
               
                
                Debug.Log($"Character {selectedCharacter} selected, button disabled.");

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
        SendMessageToServer(message);
    }
   
    private void UpdateCharacterSelectionUI(string characterName, bool isSelected)
    {
        if (isSelected)
        {
            switch (characterName)
            {
                case "HeavyBandit":
                    heavyBanditBtn.interactable = !isSelected;
                    break;
                case "LightBandit":
                    lightBanditBtn.interactable = !isSelected;
                    break;
                    // Add cases for other characters
            }
        }
       
    }
    private void ResetCharacterSelectionUI()
    {
        // Reactivating all character buttons
        heavyBanditBtn.interactable = true;
        lightBanditBtn.interactable = true;
        // Add reactivation for other character buttons
    }

    private void ProcessCharacterSelectionRequest(string roomID, string characterName, string requestingClientEndpoint)
    {
        // Check if the character is already selected in the session
        if (IsCharacterAlreadySelected(roomID, characterName))
        {
            // Notify the server to send a failure message to the requesting client
            SendMessageToServer($"CharacterSelectionFailed:{roomID}:{characterName}:{requestingClientEndpoint}");
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                UpdateCharacterSelectionUI(characterName, true);
            });

        }
        else
        {
            // Notify the server to update the character selection and inform all clients
            SendMessageToServer($"CharacterSelectionConfirmed:{roomID}:{characterName}:{requestingClientEndpoint}");
        }
    }

    private bool IsCharacterAlreadySelected(string roomID, string characterName)
    {
        // This client cannot directly check if a character is already selected in the session
        // Instead, send a request to the server to check this
        SendMessageToServer($"CheckCharacterSelection:{roomID}:{characterName}");
        return false; // Temporarily return false, actual logic depends on server response
    }


    private void UpdateCharacterSelection(string roomID, string characterName, Socket clientSocket)
    {
        // Update the character selection in the session
        if (gameServer.activeSessions.TryGetValue(roomID, out var session))
        {
            session.PlayerCharacters[clientSocket] = characterName;
        }
    }

    private void BroadcastCharacterSelectionUpdate(string roomID, string characterName, Socket clientSocket)
    {
        // Send an update to all clients in the session about the new character selection
        if (gameServer.activeSessions.TryGetValue(roomID, out var session))
        {
            string clientEndpointString = clientSocket.RemoteEndPoint.ToString();
            foreach (var memberSocket in session.MemberSockets)
            {
                SendMessage(memberSocket.RemoteEndPoint.ToString(), $"CharacterSelectionUpdate:{roomID}:{characterName}:{clientEndpointString}");
            }
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

    
