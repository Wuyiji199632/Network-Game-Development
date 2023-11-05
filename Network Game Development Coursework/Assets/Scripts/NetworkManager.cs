using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    public bool isClient = true;

    /*External C++ Functions for Networking*/

    [DllImport("NetworkAccess")]
    private static extern void SetLogCallback(LogCallback callback);// This is the function used for callback of debugging


    [DllImport("NetworkAccess")]
    private static extern void InitializeServer();


    [DllImport("NetworkAccess")]
    private static extern void InitializeClient();

    [DllImport("NetworkAccess")]
    private static extern void ConnectToServer();
    [DllImport("NetworkAccess")]
    private static extern void BroadcastBanditSelection(string playerID, string banditType);

    // This delegate matches the C++ callback signature
    public delegate void LogCallback(string message);

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
        InitializeServer();
        //InitializeClient();
        //ConnectToServer();


        if (isClient)
        {
            InitializeClient();
            ConnectToServer();
        }
        else
        {
            InitializeServer();
        }
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
}
