#include "pch.h"
#include "Server.h"
#include <WinSock2.h>
#include <winsock.h>
#include <random>
#include <vector>
#include <thread>
#include <mutex>
#include <string>
// Global list of connected client sockets
std::vector<SOCKET> connectedClients;
std::mutex clientMutex;


void AcceptClients(SOCKET listenSocket) {

    while (true) {
        // Accept incoming client connections
        SOCKET clientSocket = INVALID_SOCKET;
        clientSocket = accept(listenSocket, NULL, NULL);

        if (clientSocket == INVALID_SOCKET) {
            if (logCallback != nullptr) {
                logCallback("Accepting client failed.");
                logCallback(("Built with error: " + std::to_string(WSAGetLastError())).c_str());
            }
            
            continue; // Keep trying to accept new clients
        }

        // Lock the mutex, add client to connected client list, and then unlock
        clientMutex.lock();
        connectedClients.push_back(clientSocket);
        clientMutex.unlock();
    }
}



void InitializeServer()
{
    WSADATA wsaData;
    SOCKET listenSocket = INVALID_SOCKET;
    // 1. Initialize Winsock
    int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (result != 0) {
        if (logCallback != nullptr) {
            logCallback("WSAStartup failed with error.");
        }
        return;
    }

    // 2. Create a Socket
    listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listenSocket == INVALID_SOCKET) {
        if (logCallback != nullptr) {
            logCallback("Socket creation failed.");
        }
        WSACleanup();
        return;
    }

    // 3. Bind the Socket to an IP Address and Port
    struct sockaddr_in serverService;
    serverService.sin_family = AF_INET;
    serverService.sin_addr.s_addr = INADDR_ANY; // Listen on all interfaces with randomized IP address

    
    std::random_device rd; 
    std::mt19937 gen(rd()); 
    std::uniform_int_distribution<> distrib(49152, 65535);//Generate a random port number 

    int randomPort = distrib(gen);
    serverService.sin_port = htons(randomPort); //Bind the generated port number to the server socket

    //Bind to the server service socket
    if (bind(listenSocket, (SOCKADDR*)&serverService, sizeof(serverService)) == SOCKET_ERROR) {

        if (logCallback != nullptr) {
            logCallback("Bind failed.");
        }
        closesocket(listenSocket);
        WSACleanup();
        return;
    }
    //Check if listening is failed for the listen socket, if is succeeds, skip this if statement and accept new clients
    if (listen(listenSocket, SOMAXCONN) == SOCKET_ERROR) {
        if (logCallback != nullptr) {
            logCallback(("Listen failed with error: " + std::to_string(WSAGetLastError())).c_str());
        }
        closesocket(listenSocket);
        WSACleanup();
        return;
    }

   
    // Start a new thread to accept clients
    std::thread acceptThread(AcceptClients, listenSocket);
    acceptThread.detach();  // Let it run independently

	if (logCallback != nullptr) {

		logCallback("Server Initialized and Listening For Incoming Clients!");
		
	}
	
}

void BroadcastBanditSelection(const char* playerID, const char* banditType)
{
    // Create a message to send to clients
    std::string message = std::string(playerID) + " selected " + banditType;

    // Iterate through all connected clients and send them the message
    for (SOCKET clientSocket : connectedClients) {
        send(clientSocket, message.c_str(), message.length(), 0);
    }

    if (logCallback != nullptr) {
        std::string logMessage = "Broadcasted bandit selection to all clients: " + message;
        logCallback(logMessage.c_str());
        
    }
}
