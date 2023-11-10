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

public class GameServer : MonoBehaviour
{
    private Socket serverSocket;
    private bool isRunning;
    private List<Socket> clientSockets = new List<Socket>();
    private const int BUFFER_SIZE = 2048;
    private byte[] buffer = new byte[BUFFER_SIZE];
    [SerializeField]
    private int port = 7777;
    [SerializeField]
    private Button startGameBtn, quitGameBtn, createRoomBtn, joinRoomBtn;
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
    }

    public void StartServer()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); //Ensure that each socket address can be reused
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        serverSocket.Listen(10);
        isRunning = true;
        serverSocket.BeginAccept(AcceptCallback, null);
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

        // Remove the socket from the list of client sockets.
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
            ProcessReceivedData(current, received);

            
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
            socket.EndSend(AR);
        }
        catch (SocketException e)
        {
            Debug.Log("Failed to send message to client: " + e.Message);
            // Optionally, you might want to remove the client from the list if sending fails
            clientSockets.Remove(socket);
        }
    }
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
