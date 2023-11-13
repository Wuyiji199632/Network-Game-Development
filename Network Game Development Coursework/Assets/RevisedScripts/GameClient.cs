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

public class GameClient : MonoBehaviour
{
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
        }
        catch (SocketException ex)
        {
            Debug.Log("Server closed connection: " + ex.Message);
            return;
        }

        if (received == 0)
        {
            Debug.Log("Server closed connection");
            return;
        }

        byte[] recBuf = new byte[received];
        Array.Copy(buffer, recBuf, received);
        string text = Encoding.ASCII.GetString(recBuf);
        Debug.Log($"Client Received: {text} from server");



        UnityMainThreadDispatcher.RunOnMainThread(() => OnServerMessageReceived(text));

        current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
    }
    private void OnServerMessageReceived(string message)
    {
        Debug.Log("Server Broadcast: " + message);
        if (message.StartsWith("CharacterSelectionUpdate:"))
        {
            // Split the message by ':' to get the different parts
            string[] messageParts = message.Split(':');
            // The second part of the message is the client's endpoint who selected the character
            string selectingClientEndPoint = messageParts[1];
            // The third part of the message is the name of the selected character
            string selectedCharacter = messageParts[2];

            // Log the selection information to the Unity console
            Debug.Log($"{selectingClientEndPoint} has selected {selectedCharacter}");

        }
        if (message.StartsWith("SessionClosed:"))
        {
            string closedRoomID = message.Split(':')[1];
            if (roomID == closedRoomID)
            {
                Debug.Log($"Session {roomID} has been successfully closed.");
                roomID = null; 
            }
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

    }
    #region Session Generation Logics

    // Call this method when you want to create a room.
    public void SendCreateRoomRequest()
    {
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
        string message = $"CharacterSelectionUpdate:{roomID}:{characterName}";
        Debug.Log($"Attempting to send character selection message: {message}");
        SendMessageToServer(message);
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
        SceneManager.LoadScene("MainMenu");
    }
}

    
