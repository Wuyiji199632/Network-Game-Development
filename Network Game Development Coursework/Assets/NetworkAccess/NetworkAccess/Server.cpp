#include "pch.h"
#include "Server.h"
#include <WinSock2.h>
#include <winsock.h>




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
    serverService.sin_port = htons(55555); // Use port number 55555 (or any other port you prefer)

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
