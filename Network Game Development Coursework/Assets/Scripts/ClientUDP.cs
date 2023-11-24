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
    private Thread receiveThread;
    private string serverIP = "127.0.0.1"; // Replace with your server IP
    private int serverPort = 8888;
    void Start()
    {
        InitializeUDP();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void InitializeUDP()
    {
        // Create a new UdpClient for reading incoming data
        udpClient = new UdpClient(serverPort);

        // Start background thread for receiving data
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveData()
    {
        while (true)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remoteEndPoint);

                // Process the received data
                string receivedText = Encoding.ASCII.GetString(data);

                // Assuming the received data is a string representing position in the format "x,y,z"
                // You'll need to adapt this parsing to fit the data format you've chosen
                string[] splitData = receivedText.Split(',');
                if (splitData.Length == 3)
                {
                    float x = float.Parse(splitData[0]);
                    float y = float.Parse(splitData[1]);
                    float z = float.Parse(splitData[2]);

                    // Assuming you have a method to update your character's position
                    UpdateCharacterPosition(new Vector3(x, y, z));
                }

                Debug.Log($"Receiving data!");
            }
            catch (Exception e)
            {
                Debug.LogError($"UDP Receive Error: {e.Message}");
                break;
            }
        }
    }

    private void UpdateCharacterPosition(Vector3 newPosition)
    {
        // Update your character's position
        // Note: Since Unity's update to game objects must be done on the main thread, 
        // you might need to use a thread-safe method like UnityMainThreadDispatcher to update the position.

        Debug.Log($"Updating player positions!");
    }

}
