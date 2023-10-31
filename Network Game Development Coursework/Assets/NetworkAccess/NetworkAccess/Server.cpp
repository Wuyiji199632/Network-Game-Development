#include "pch.h"
#include "Server.h"
#include <WinSock2.h>
#include <winsock.h>
#include <random>



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

    if (bind(listenSocket, (SOCKADDR*)&serverService, sizeof(serverService)) == SOCKET_ERROR) {

        if (logCallback != nullptr) {
            logCallback("Bind failed.");
        }
        closesocket(listenSocket);
        WSACleanup();
        return;
    }

    // 4. Listen for Client Connections
    if (listen(listenSocket, SOMAXCONN) == SOCKET_ERROR) {
        if (logCallback != nullptr) {
            logCallback("Listen failed.");
        }
        closesocket(listenSocket);
        WSACleanup();
        return;
    }
	
	if (logCallback != nullptr) {

		logCallback("Server Initialized and Listening For Incoming Clients!");
		
	}
	
}

void BroadcastBanditSelection(const char* playerID, const char* banditType)
{

}
