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

    public Dictionary<string, string> sessionIDPasswordPairs;
    /*External C++ Functions for Networking*/
    [DllImport("NetworkAccess")]
    private static extern void SetLogCallback(LogCallback callback);// This is the function used for callback of debugging


    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern void InitializeServer(string sessionID,string password);//This will be called inside an internal function of unity's side


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


    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern string GetSessionID(string sessionID);

    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern string GetSessionPassword(string sessionID);

    [DllImport("NetworkAccess", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool ValidateSessionIDAndPassword(string sessionID,string password);

    

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

    public InputField createSessionIDInputField,createPasswordInputField;
    public InputField joinSessionIDInputField,joinPasswordInputField;
    private void Awake()
    {
        sessionIDPasswordPairs = new Dictionary<string, string>();
    }

    // Start is called before the first frame update
    void Start()
    {
        SetLogCallback(LogFromDLL);
        SetMessageReceivedCallback(OnMessageReceived); // Set the message received callback for the communication between client and server
        

    }

    // Update is called once per frame
   
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

    public void GoToRoomCreationPage()
    {
       
        SceneManager.LoadScene("RoomCreationPage");
        
    }
    public void GoToPasswordSettingPage()
    {
        SceneManager.LoadScene("PasswordSettingRoom");
    }
    public void GoToJoinRoomPage()
    {
        SceneManager.LoadScene("JoinRoomPage");
    }
    public void StartServer()
    {
        string sessionID = createSessionIDInputField.text;
        string password = createPasswordInputField.text;
        if (!string.IsNullOrEmpty(sessionID) && !string.IsNullOrEmpty(password))
        {
            // Store the session ID and password pair
            sessionIDPasswordPairs[sessionID] = password;
            // Call the DLL function to initialize the server with sessionID and password
            InitializeServer(sessionID, password);
            Debug.Log("Server started with session ID: " + sessionID+" and password: "+ password);
        }
        else
        {
            Debug.LogError("Session ID and password cannot be empty.");
        }
        SceneManager.LoadScene("SelectionPage");
    }
    public void JoinLobby()
    {
        string sessionID = joinSessionIDInputField.text;
        string password = joinPasswordInputField.text;

        // Check if the sessionID exists and the password matches.
        if (!string.IsNullOrEmpty(sessionID) && !string.IsNullOrEmpty(password))
        {
            if (sessionIDPasswordPairs.TryGetValue(sessionID, out string storedPassword))
            {
                // Check if the password entered matches the stored password.
                if (password == storedPassword)
                {
                    // If the session is valid and the password matches, connect to the server.
                    //ConnectToServer();
                    Debug.Log("Joined lobby with session ID: " + sessionID);
                    SceneManager.LoadScene("LobbyRoom");
                }
                else
                {
                    Debug.LogError("Incorrect password for the session ID.");
                }
            }
            else
            {
                Debug.LogError("Session ID does not exist.");
            }
        }
        else
        {
            Debug.LogError("Session ID and password cannot be empty.");
        }
    }
    public void EndServer()
    {
        CleanUpServer();
        SceneManager.LoadScene("MainMenu");
    }
}