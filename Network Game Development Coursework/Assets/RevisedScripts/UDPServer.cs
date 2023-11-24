using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
public class UDPServer : MonoBehaviour
{
    private UdpClient udpServer;
    private int listenPort = 8888;
    private Thread listenThread;
    private volatile bool isRunning = true; // Flag to control the listening loop

    void Start()
    {
        InitializeUDPServer();
    }

    private void InitializeUDPServer()
    {
        udpServer = new UdpClient(listenPort);
        listenThread = new Thread(ListenForMessages);
        listenThread.IsBackground = true;
        listenThread.Start();
        Debug.Log("UDP Server started, listening for messages.");
    }

    private void ListenForMessages()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpServer.Receive(ref remoteEndPoint);
                string text = Encoding.ASCII.GetString(data);
                Debug.Log($"Received message from {remoteEndPoint}: {text}");
            }
            catch (Exception e)
            {
                if (isRunning) // Log errors only if the server is supposed to be running
                {
                    Debug.LogError("UDP Server Receive Error: " + e.Message);
                }
            }
        }
    }

    private void OnApplicationQuit()
    {
        isRunning = false; // Stop the listening loop
        if (udpServer != null)
        {
            udpServer.Close(); // Close the UdpClient
        }
    }
}
