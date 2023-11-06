#include "pch.h"
#include "QuerryServer.h"
#include <WinSock2.h>
#include <random>
#include <vector>
#include <thread>
#include <mutex>
#include <string>
#include <map>
#include <unordered_map>
#include <random>
#include <sstream>
#include <iomanip>
#include <ws2tcpip.h>



// Global list of connected client sockets
std::vector<SOCKET> connectedClients;
std::mutex clientMutex;
unsigned short serverServicePort=0; // To keep track of the server's service port
std::string serverIP="";
SOCKET g_clientSocket = INVALID_SOCKET; //Global definition for the client socket


// Helper function to get the first non-loopback IP address of this host.
std::string GetLocalIPAddress() {
    struct addrinfo hints, * info, * p;
    int result;

    char hostname[1024];
    char ipstr[INET6_ADDRSTRLEN];
    hostname[1023] = '\0';
    gethostname(hostname, 1023);

    memset(&hints, 0, sizeof(hints));
    hints.ai_family = AF_INET; // AF_INET means IPv4 only addresses

    result = getaddrinfo(hostname, NULL, &hints, &info);
    if (result != 0) {
        fprintf(stderr, "getaddrinfo: %s\n", gai_strerror(result));
        exit(1);
    }

    std::string ipAddress;
    for (p = info; p != NULL; p = p->ai_next) {
        if (p->ai_family == AF_INET) { // IPv4
            struct sockaddr_in* ipv4 = (struct sockaddr_in*)p->ai_addr;
            void* addr = &(ipv4->sin_addr);

            // Convert the IP to a string and store it in ipstr
            inet_ntop(p->ai_family, addr, ipstr, sizeof(ipstr));
            ipAddress.assign(ipstr);
            
        }
    }

    freeaddrinfo(info);
    return ipAddress;
}




std::string GenerateUniqueID() {

    std::random_device rd;
    std::mt19937_64 gen(rd());
    std::uniform_int_distribution<uint64_t> dis;

    std::stringstream ss;
    for (int i = 0; i < 2; i++) {
        ss << std::hex << dis(gen);
    }

    return ss.str();

}
void NotifyClientJoined(const SOCKET& clientSocket)
{
    std::string message = "A new player joined the lobby!";

    //Iterate through all the joined clients and send the message to them.

    for (SOCKET socket : connectedClients) {

        if (socket != clientSocket) {

            send(socket, message.c_str(), message.length(), 0);
        }
    }

    // Optionally, log this on the server-side
    if (logCallback != nullptr) {

        logCallback(("Notified all clients that a new player has joined: " + message).c_str());
    }

}
void AcceptClients(SOCKET listenSocket) {

    while (true) {
        // Accept incoming client connections
        SOCKET clientSocket = INVALID_SOCKET;
        clientSocket = accept(listenSocket, NULL, NULL);
        std::string clientID = GenerateUniqueID();
        if (clientSocket == INVALID_SOCKET) {
            if (logCallback != nullptr) {
                logCallback("Accepting client failed.");
                logCallback(("Built with error: " + std::to_string(WSAGetLastError())).c_str());
            }
            
            continue; // Keep trying to accept new clients
        }

        // Lock the mutex, add a client to connected client list, and then unlock to make the operation thread-safe. Ensure only one thread handles accepting one client and add it to the client list
        std::lock_guard<std::mutex> guard(clientMutex);
        connectedClients.push_back(clientSocket);
        //If the number of clients is out of the scope, the lock will be automatically released
       
       
    }
}

int BindSocketWithRetry(SOCKET& socket, struct sockaddr_in& serviceAddr, int retries = 10) {
    std::random_device rd;
    std::mt19937 gen(rd());
    std::uniform_int_distribution<> distrib(39432, 69432);

    while (retries > 0) {
        int randomPort = distrib(gen);
        serviceAddr.sin_port = htons(randomPort);

        if (bind(socket, (struct sockaddr*)&serviceAddr, sizeof(serviceAddr)) == SOCKET_ERROR) {
            logCallback(("Bind failed on port: " + std::to_string(randomPort) + ", retrying...").c_str());
            --retries;
        }
        else {
            // Bind successful
            return randomPort; // Return the successful port number
        }
    }
    // If we get here, all retries failed
    closesocket(socket);
    WSACleanup();
    logCallback("Unable to bind socket after multiple retries, resources cleaned up.");
    return -1;
}

extern "C" void InitializeServer()
{
    WSADATA wsaData;
    SOCKET listenSocket = INVALID_SOCKET;
    SOCKET queryServiceSocket = INVALID_SOCKET; // Socket for the query service
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

    // 3.Create a socket for the query service to listen to client queries
    queryServiceSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (queryServiceSocket == INVALID_SOCKET) {
        logCallback("Query service socket creation failed.");
        closesocket(listenSocket);
        WSACleanup();
        return;
    }

    
    // 4.Bind the query service socket to a well-known port
     
    struct sockaddr_in service;
    service.sin_family = AF_INET;
    service.sin_addr.s_addr = INADDR_ANY; 


    std::random_device rdn;
    std::mt19937 generator(rdn());
    std::uniform_int_distribution<> distributor(36358, 69543);//Generate a random port number 
    int randomPortNum = distributor(generator);//Generate a random port number for binding

    struct sockaddr_in queryServiceAddr = { 0 };
    queryServiceAddr.sin_family = AF_INET;
    queryServiceAddr.sin_addr.s_addr = htonl(INADDR_ANY);
    queryServiceAddr.sin_port = htons(randomPortNum); // bind the random port number

    if (bind(queryServiceSocket, (struct sockaddr*)&queryServiceAddr, sizeof(queryServiceAddr)) == SOCKET_ERROR) {
        /* logCallback("Bind for query service failed.");
        closesocket(listenSocket);
        closesocket(queryServiceSocket);
        WSACleanup();*/
        if (logCallback) {

            logCallback("Retry binding to query service port.");
        }
        BindSocketWithRetry(queryServiceSocket, queryServiceAddr, 10);
       
       
    }

    // 5.Start listening on the query service socket
    if (listen(queryServiceSocket, SOMAXCONN) == SOCKET_ERROR) {

        if (logCallback) {

            logCallback("Listen for query service failed.");
        }
       
        closesocket(listenSocket);
        closesocket(queryServiceSocket);
        WSACleanup();
        return;
    }

    // 6.Start a thread for the query service to handle incoming queries
    std::thread queryServiceThread([queryServiceSocket]() {
        struct sockaddr_in clientAddr;
        int clientAddrSize = sizeof(clientAddr);

        while (true) {

            SOCKET querySocket = accept(queryServiceSocket, (struct sockaddr*)&clientAddr, &clientAddrSize);
            if (querySocket != INVALID_SOCKET) {
                // Send the actual server service port to the client
                std::string portString = std::to_string(serverServicePort);
                send(querySocket, portString.c_str(), portString.length(), 0);
                closesocket(querySocket);
            }
        }
        });
    queryServiceThread.detach(); // Let the query service thread run independently

    

    // 7.Bind the Socket to an IP Address and Port generated randomly
    struct sockaddr_in serverService;
    serverService.sin_family = AF_INET;
    serverService.sin_addr.s_addr = INADDR_ANY; // Listen on all interfaces

    
    std::random_device rd; 
    std::mt19937 gen(rd()); 
    std::uniform_int_distribution<> distrib(49152, 65535);//Generate a random port number 

    int randomPort = distrib(gen);
    serverService.sin_port = htons(randomPort); //Bind the generated port number to the server socket

    //8.Bind to the server service socket
    if (bind(listenSocket, (SOCKADDR*)&serverService, sizeof(serverService)) == SOCKET_ERROR) {

        if (logCallback != nullptr) {
            logCallback("Bind failed.");
        }
        closesocket(listenSocket);
        WSACleanup();
        return;
    }
    else {
        // After binding, get the port number
        struct sockaddr_in sin;
        int addrlen = sizeof(sin);
        if (getsockname(listenSocket, (struct sockaddr*)&sin, &addrlen) == 0 && sin.sin_family == AF_INET && addrlen == sizeof(sin)) {

            serverServicePort = ntohs(sin.sin_port);
            serverIP = GetLocalIPAddress();
            if (logCallback != nullptr) {
                logCallback(("Server is listening on IP: " + serverIP + " Port: " + std::to_string(serverServicePort)).c_str());
            }
        }
        else {

            if (logCallback != nullptr) {
                logCallback("Failed to get socket name.");
            }
            closesocket(listenSocket);
            WSACleanup();
            return;
        }
    }
    //9.Check if listening is failed for the listen socket, if is succeeds, skip this if statement and accept new clients
    if (listen(listenSocket, SOMAXCONN) == SOCKET_ERROR) {
        if (logCallback != nullptr) {
            logCallback(("Listen failed with error: " + std::to_string(WSAGetLastError())).c_str());
        }
        closesocket(listenSocket);
        WSACleanup();
        return;
    }

   
    //10. Start a new thread to accept clients so that the process does not block that for binding port and listening for clients
    std::thread acceptThread(AcceptClients, listenSocket);
    acceptThread.detach();  // Let it run independently
    

	if (logCallback != nullptr) {

		logCallback("Server and query service are running.");
		
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

extern "C" void InitializeClient(const char* queryServiceIP, int queryServicePort)
{


    WSADATA wsaData;
    SOCKET querySocket = INVALID_SOCKET;
    struct sockaddr_in server;
    char buffer[1024] = { 0 };
    int serverPort = 0;

    // Initialize Winsock
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        logCallback("WSAStartup failed");
        return;
    }

    // Create Socket
    querySocket = socket(AF_INET, SOCK_STREAM, 0);
    if (querySocket == INVALID_SOCKET) {
        logCallback("Socket creation failed");
        WSACleanup();
        return;
    }

    // Setup server structure
    server.sin_family = AF_INET;
    server.sin_port = htons(queryServicePort);
    inet_pton(AF_INET, queryServiceIP, &server.sin_addr);

    // Connect to the Query Service
    if (connect(querySocket, (struct sockaddr*)&server, sizeof(server)) == SOCKET_ERROR) {
        logCallback("Connect failed");
        closesocket(querySocket);
        WSACleanup();
        return;
    }

    // Receive data from the Query Service
    if (recv(querySocket, buffer, sizeof(buffer), 0) <= 0) {
        logCallback("Receive failed or connection closed");
        closesocket(querySocket);
        WSACleanup();
        return;
    }

    // Convert received data to port number
    serverPort = atoi(buffer);
    if (serverPort <= 0) {
        logCallback("Invalid port received");
        closesocket(querySocket);
        WSACleanup();
        return;
    }

    // Connection to the query service succeeded and received the server port
    logCallback(("Query service provided server port: " + std::to_string(serverPort)).c_str());

    // Close the query socket as it's no longer needed
    closesocket(querySocket);
    // Cleanup Winsock
    WSACleanup();

}


unsigned short QuerryServerPort() {
  
    
    return serverServicePort;
}
std::string QuerryServerIP()
{
    return std::string();
}
bool ReceiveMessagesFromServer(SOCKET clientSocket)
{
    const int bufferSize = 1024;

    char buffer[bufferSize];


    //Initialise this buffer
    memset(buffer, 0, bufferSize);

    //Receive data from the server
    int bytesReceived = recv(clientSocket, buffer, bufferSize - 1, 0);

    if (bytesReceived > 0) {

        // Successfully received a message
        buffer[bytesReceived] = '\0'; // Null-terminate the received string
        std::string message(buffer);

        if (g_messageReceivedCallback != nullptr) {
            g_messageReceivedCallback(message.c_str());
        }

        return true;
    }
    else if (bytesReceived == 0) {

        // Connection has been gracefully closed by the server
        if (logCallback != nullptr) {
            logCallback("The server has closed the connection.");
        }
        return false;
    }
    else {

        // An error occurred when trying to receive data
        if (logCallback != nullptr) {

            logCallback(("recv failed with error: " + std::to_string(WSAGetLastError())).c_str());
        }
        return false;
    }

    return false;
}

SOCKET GetClientSocket()
{
    return g_clientSocket; //Return the global client socket
}


