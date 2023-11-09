using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using System.Net;
using System.Net.Sockets;

public class ClientManager : MonoBehaviour
{
    
    private Thread clientThread;
    private bool isListening = true;
    [DllImport("NetworkAccess")]
    private static extern void SetLogCallback(LogCallback callback);// This is the function used for callback of debugging

    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ConnectToServer();
    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern ushort QuerryServerPort();
    
    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QuerryServerIP(); //Convert it into IntPtr and don't forget to use Marshal to make it into a string

    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SendClientMessage(string message);

    public delegate void LogCallback(string message);

    [AOT.MonoPInvokeCallback(typeof(LogCallback))]
    private static void LogFromDLL(string message)
    {
        Debug.Log(message);
    }
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    // Start is called before the first frame update
    void Start()
    {
        SetLogCallback(LogFromDLL);
        /* string ip=QuerryServerIP();
       ushort port = QuerryServerPort();

        InitializeClient(ip, port);*/

        InitializeClient();

    }

    void InitializeClient()
    {
        // Connect to server and start listening for messages
        ConnectToServer(); // Make sure this function is set up to handle being called on the client side

        // Start a new thread for listening to server messages
        clientThread = new Thread(ClientListen);
        clientThread.Start();
    }
    void ClientListen()
    {

        IntPtr ipPtr = QuerryServerIP();  // Get the pointer to the IP string
        string ip = Marshal.PtrToStringAnsi(ipPtr);  // Convert pointer to string
        ushort port = QuerryServerPort();

        Debug.Log("Client is listening! "+"IP: "+ip+"Port: "+ port);

        SendClientMessage("Client Joined!");

    }

    private void OnServerMessageReceived(string message)
    {
        Debug.Log("Message received from server: " + message);

        if(message== "Client 1 Joined!")
        {
            Debug.Log("A client has joined!");
        }


    }
}
