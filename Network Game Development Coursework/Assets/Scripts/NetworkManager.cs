using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.UIElements;


public class NetworkManager : MonoBehaviour
{

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
    
    private string selectedBanditType = "";
    // Start is called before the first frame update
    void Start()
    {
        SetLogCallback(LogFromDLL);
        InitializeServer();
        //InitializeClient();
        //ConnectToServer();

       


    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SelectBandit(string banditType)
    {
        selectedBanditType = banditType;
    }

    public void ConfirmSelection()
    {
        if (!string.IsNullOrEmpty(selectedBanditType))
        {
            // Ideally, you'd have a unique playerID for each player.
            // This is a placeholder; replace with your playerID generation method.
            string playerID = "Player_" + UnityEngine.Random.Range(1000, 9999);

            BroadcastBanditSelection(playerID, selectedBanditType);
        }
        else
        {
            Debug.LogWarning("No bandit type selected.");
        }
    }
}
