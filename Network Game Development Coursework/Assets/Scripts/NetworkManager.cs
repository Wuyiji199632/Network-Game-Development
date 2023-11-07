using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading;
using System.Collections.Concurrent;

public class NetworkManager : MonoBehaviour
{
    public bool isClient = true;

    // Thread to handle the listening process
    
    private Thread clientThread;
    private bool isListening = true; // Control flag for the listening thread

    // A thread-safe queue to hold messages received from the server
    private static ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();


    /*External C++ Functions for Networking*/
    [DllImport("NetworkAccess")]
    private static extern void SetLogCallback(LogCallback callback);// This is the function used for callback of debugging


    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern void InitializeServer();//This will be called inside an internal function of unity's side


    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern void InitializeClient(string queryServiceIP, int queryServicePort);

    [DllImport("NetworkAccess")]
    private static extern void ConnectToServer();
    [DllImport("NetworkAccess")]
    private static extern void CleanUpServer();
    [DllImport("NetworkAccess")]
    private static extern void BroadcastBanditSelection(string playerID, string banditType);

    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool ReceiveMessagesFromServer(IntPtr clientSocket);

    [DllImport("NetworkAccess")]
    private static extern IntPtr GetClientSocket();

    [DllImport("NetworkAccess")]
    private static extern void SetMessageReceivedCallback(MessageReceivedCallback callback);

    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern ushort QuerryServerPort();

    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QuerryServerIP(); //Hint: don't define it as a string type as memory management between c# and c++ are different



    // This delegate matches the C++ callback signature
    public delegate void LogCallback(string message);

    // Delegate for message receiving from the server
    public delegate void MessageReceivedCallback(string message);

    [AOT.MonoPInvokeCallback(typeof(MessageReceivedCallback))]
    private static void OnMessageReceived(string message)
    {
        messageQueue.Enqueue(message);
    }

    [AOT.MonoPInvokeCallback(typeof(LogCallback))]
    private static void LogFromDLL(string message)
    {
        Debug.Log(message);
    }

    public Button selectHeavyBanditBtn, selectLightBanditBtn, confirmBtn;

    public string selectedBanditType = "";


    // Start is called before the first frame update
    void Start()
    {
        SetLogCallback(LogFromDLL);
        SetMessageReceivedCallback(OnMessageReceived); // Set the message received callback for the communication between client and server
        /* InitializeServer();
        ushort port=QuerryServerPort();
        IntPtr ipPtr=QuerryServerIP();
        string serverIP=Marshal.PtrToStringAnsi(ipPtr);
        if (port != 0 && serverIP != null)
        {
            Debug.Log("Successfully accessed port number " + port + " and ip: " + serverIP);
           

        }
        else
        {
            Debug.LogError("Failed to retrieve server information!");
        }*/


    }

    // Update is called once per frame
    void Update()
    {

    }

    
    public void SelectBandit(string banditType)
    {
        selectedBanditType = banditType;
        confirmBtn.gameObject.SetActive(true);
    }

    public void ConfirmSelection()
    {
        if (!string.IsNullOrEmpty(selectedBanditType))
        {

            string playerID = "Player_" + UnityEngine.Random.Range(1000, 9999).ToString();

            BroadcastBanditSelection(playerID, selectedBanditType);
        }
        else
        {
            Debug.LogWarning("No bandit type selected.");
        }
        //TODO: switch the game screen the the lobby room
        SceneManager.LoadScene("LobbyRoom");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Game Quitted");
    }

    private void OnApplicationQuit()
    {
        CleanUpServer();
    }

    public void StartServer()
    {
        InitializeServer();
        SceneManager.LoadScene("SelectionPage");
    }
}