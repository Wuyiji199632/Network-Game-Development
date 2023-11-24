using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
public class ClientUDP : MonoBehaviour
{
    private UdpClient udpClient;
    private string serverIp = "127.0.0.1";
    private int serverPort = 8888;

    void Start()
    {
        InitializeUDP();
    }

    private void InitializeUDP()
    {
        udpClient = new UdpClient();
        try
        {
            udpClient.Connect(serverIp, serverPort);
            Debug.Log("Connected to UDP server.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to connect to UDP server: " + e.Message);
        }
    }

    public void SendMessage(string message)
    {
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            udpClient.Send(data, data.Length);
            Debug.Log("Message sent to UDP server: " + message);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to send message: " + e.Message);
        }
    }

    private void OnApplicationQuit()
    {
        if (udpClient != null)
        {
            udpClient.Close(); // Close the UdpClient
        }
    }

}
