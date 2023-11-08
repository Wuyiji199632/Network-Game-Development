using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Threading;
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
    private static extern string QuerryServerIP();

    

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
        Debug.Log("Client is listening!");
        
    }
}
