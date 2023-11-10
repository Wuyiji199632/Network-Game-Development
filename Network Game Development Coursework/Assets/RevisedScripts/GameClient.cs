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
    private Socket clientSocket;
    private const int BUFFER_SIZE = 2048;
    private byte[] buffer = new byte[BUFFER_SIZE];
    private string welcomeMsg = "Welcome to the Game Server";
    [SerializeField]
    private string serverIp = "127.0.0.1"; //Test with a fixed ip address, we will generate an ip address later on for more robust connection or get the ip address the current network is connected to
    [SerializeField]
    private int serverPort = 7777;
    [SerializeField]
    private Button startGameBtn, quitGameBtn, createRoomBtn, joinRoomBtn, mainMenuBtn;
    private bool receivedDebugMessage = false;
    private void Awake()
    {
        DontDestroyOnLoad(this);
    }
    void Start()
    {

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
        Debug.Log("Received: " + text);



        UnityMainThreadDispatcher.RunOnMainThread(() => OnServerMessageReceived(text));

        current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
    }
    private void OnServerMessageReceived(string message)
    {
        Debug.Log("Server Broadcast: " + message);
        
        // Add any other logic you need when a message is received here
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            // Code here is running on the main thread
            // Update your UI here

            OnScreenConsole.Log(message);
        });

        if (message.StartsWith("CLIENT DISCONNECTED:"))
        {
            string disconnectedClient = message.Substring("CLIENT DISCONNECTED:".Length);
            HandleClientDisconnection(disconnectedClient);
        }
        else
        {
            // Handle other types of messages
            OnScreenConsole.Log("Server Broadcast: " + message);
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
    public void GoToRoomCreationPage()
    {
        startGameBtn.gameObject.SetActive(false);
        quitGameBtn.gameObject.SetActive(false);
        createRoomBtn.gameObject.SetActive(true);
        joinRoomBtn.gameObject.SetActive(true);
        mainMenuBtn.gameObject.SetActive(true);
    }
    public void BackToMenuUIChange()
    {
        startGameBtn.gameObject.SetActive(true);
        quitGameBtn.gameObject.SetActive(true);
        createRoomBtn.gameObject.SetActive(false);
        joinRoomBtn.gameObject.SetActive(false);
        mainMenuBtn.gameObject.SetActive(false);
    }
    private void OnApplicationQuit()
    {
        DisconnectFromServer();
    }
    public void QuitGame()
    {

        UnityEngine.Application.Quit();
        DisconnectFromServer();

    }
}

    
