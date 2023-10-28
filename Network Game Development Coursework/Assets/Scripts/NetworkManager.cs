using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using static UnityEngine.Application;

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


    // This delegate matches the C++ callback signature
    public delegate void LogCallback(string message);

    [AOT.MonoPInvokeCallback(typeof(LogCallback))]
    private static void LogFromDLL(string message)
    {
        Debug.Log(message);
    }
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
}
