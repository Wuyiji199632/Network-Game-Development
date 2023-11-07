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
#include <atomic>


// Global list of connected client sockets
std::vector<SOCKET> connectedClients;
std::mutex clientMutex;
unsigned short serverServicePort=0; // To keep track of the server's service port
std::string serverIP="";
SOCKET g_clientSocket = INVALID_SOCKET; //Global definition for the client socket

/*Server sockets*/
SOCKET listenSocket = INVALID_SOCKET;
SOCKET queryServiceSocket = INVALID_SOCKET;
 bool serverRunning = false;
// Helper function to get the first non-loopback IP address of this host.

const char* GetLocalIPAddress() {
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

    char* ipAddress = nullptr;
    for (p = info; p != NULL; p = p->ai_next) {
        if (p->ai_family == AF_INET) { // IPv4
            struct sockaddr_in* ipv4 = (struct sockaddr_in*)p->ai_addr;
            void* addr = &(ipv4->sin_addr);

            // Convert the IP to a string and store it in ipstr
            inet_ntop(p->ai_family, addr, ipstr, sizeof(ipstr));
            size_t ipstr_len = strlen(ipstr) + 1;
            ipAddress = new char[ipstr_len]; // Allocate memory for the IP address string
            strcpy_s(ipAddress, ipstr_len, ipstr); // Use strcpy_s for secure string copy
            break; // Only store the first IP address
        }
    }

    freeaddrinfo(info);

    if (ipAddress == nullptr) {
        const char* notFoundMsg = "Not found";
        size_t msgLen = strlen(notFoundMsg) + 1;
        ipAddress = new char[msgLen];
        strcpy_s(ipAddress, msgLen, notFoundMsg);
    }

    return ipAddress;
}

// Function to handle incoming queries
void HandleQueryService(SOCKET queryServiceSocket) {
    struct sockaddr_in clientAddr;
    int clientAddrSize = sizeof(clientAddr);

    while (true) {
        SOCKET querySocket = accept(queryServiceSocket, (struct sockaddr*)&clientAddr, &clientAddrSize);
        if (querySocket == INVALID_SOCKET) {
            int error = WSAGetLastError();
            if (error != WSAEWOULDBLOCK) {
                // Actual error handling
                logCallback("Accept failed on query service socket.");
                break;
            }
            // WSAEWOULDBLOCK just means there are no connections to accept right now
            // You might want to sleep here to prevent a busy loop
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            continue;
        }

        // Handle the accepted socket (querySocket) normally
        // Send the actual server service port to the client
        std::string portString = std::to_string(serverServicePort);
        int bytesSent = send(querySocket, portString.c_str(), portString.length(), 0);
        if (bytesSent == SOCKET_ERROR) {
            int sendError = WSAGetLastError();
            if (sendError != WSAEWOULDBLOCK) {
                // Handle send error
                logCallback("Send failed on query service socket.");
                closesocket(querySocket);
                continue;
            }
            // WSAEWOULDBLOCK just means the send would block, you can retry or handle as needed
        }
        closesocket(querySocket);
    }
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

    while (serverRunning) {
        // Accept incoming client connections
        SOCKET clientSocket = accept(listenSocket, NULL, NULL);

        if (clientSocket == INVALID_SOCKET) {
            int error = WSAGetLastError();
            if (error == WSAEWOULDBLOCK) {
                // No pending connections, just continue and try again.
                // Introduce some delay to prevent spinning too fast and consuming CPU.
                std::this_thread::sleep_for(std::chrono::milliseconds(100)); // 100 ms delay
                continue;
            }
            else {
                // An actual error occurred.
                if (logCallback != nullptr) {
                    logCallback("Accepting client failed.");
                    logCallback(("Accept failed with error: " + std::to_string(error)).c_str());
                }
                // Depending on the design, you might want to break out of the loop and terminate the server
                // or just continue after logging the error.
                continue;
            }
        }

        // At this point, clientSocket is valid
        std::string clientID = GenerateUniqueID();

        // Lock the mutex, add the client to the connected client list, then unlock.
        std::lock_guard<std::mutex> guard(clientMutex);
        connectedClients.push_back(clientSocket);
        // Client is now successfully added, log or process as needed.
       
    }
}

int BindSocketWithRetry(SOCKET& socket, struct sockaddr_in& serviceAddr, int retries = 10, int retryDelayMilliseconds = 100) {
    std::random_device rd;
    std::mt19937 gen(rd());
    std::uniform_int_distribution<> distrib(39432, 69432);

    while (retries > 0) {
        int randomPort = distrib(gen);
        serviceAddr.sin_port = htons(randomPort);

        if (bind(socket, (struct sockaddr*)&serviceAddr, sizeof(serviceAddr)) == SOCKET_ERROR) {
            int lastError = WSAGetLastError();
            logCallback(("Bind failed on port: " + std::to_string(randomPort) + " with error: " + std::to_string(lastError) + ", retrying...").c_str());
            --retries;
            if (retries > 0) {
                // If not the last retry, wait a bit before retrying
                std::this_thread::sleep_for(std::chrono::milliseconds(retryDelayMilliseconds));
            }
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
    serverRunning = true;
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

    // Set non-blocking mode for the listening socket
    unsigned long mode = 1;
    result = ioctlsocket(listenSocket, FIONBIO, &mode);
    if (result != NO_ERROR) {
        if (logCallback != nullptr) {
            logCallback("Failed to set listenSocket to non-blocking mode.");
        }
        closesocket(listenSocket);
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

    // Set non-blocking mode for the query service socket
    result = ioctlsocket(queryServiceSocket, FIONBIO, &mode);
    if (result != NO_ERROR) {
        if (logCallback) {
            logCallback("Failed to set queryServiceSocket to non-blocking mode.");
        }
        closesocket(listenSocket);
        closesocket(queryServiceSocket);
        WSACleanup();
        return;
    }
    
    
    // 4.Bind the query service socket to a well-known port or a randomly generated port depending on the application's settings
     
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
    queryServiceAddr.sin_port = htons(5555); // bind the random port number

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
    std::thread queryServiceThread(HandleQueryService, queryServiceSocket);
    if (queryServiceThread.joinable()) {

        queryServiceThread.detach(); // Let the query service thread run independently
    }
    else {

        if (logCallback) {

            logCallback("Could not create query service thread.");
            // Cleanup code
        }
       
    }

    // 7.Bind the Socket to an IP Address and Port 
    struct sockaddr_in serverService;
    serverService.sin_family = AF_INET;
    serverService.sin_addr.s_addr = INADDR_ANY; // Listen on all interfaces

    
    std::random_device rd; 
    std::mt19937 gen(rd()); 
    std::uniform_int_distribution<> distrib(39412, 69853);//Generate a random port number 

    int randomPort = distrib(gen);
    serverService.sin_port = htons(8888); //Bind the generated port number to the server socket

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
    if (acceptThread.joinable()) {
        acceptThread.detach();  // Let it run independently
    }
    else {
        if(logCallback){

            logCallback("Could not create accept thread.");
                  
        }      
    }
    
	if (logCallback != nullptr) {

		logCallback("Server and query service are running.");
		
	}	
}

extern "C" void CleanUpServer()
{
    serverRunning = false;
    // Close the query service socket if it's valid
    if (queryServiceSocket != INVALID_SOCKET) {
        closesocket(queryServiceSocket);
        queryServiceSocket = INVALID_SOCKET;
    }

    // Close the listen socket if it's valid
    if (listenSocket != INVALID_SOCKET) {
        closesocket(listenSocket);
        listenSocket = INVALID_SOCKET;
    }

    // Perform cleanup on all client sockets
    for (auto& socket : connectedClients) {
        if (socket != INVALID_SOCKET) {
            closesocket(socket);
        }
    }
    connectedClients.clear();
    if (logCallback) {

        logCallback("Sockets are closed.");
    }
    // Clean up Winsock
    WSACleanup();
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
/*extern "C" void InitializeClient(const char* queryServiceIP, int queryServicePort)
{


    WSADATA wsaData;
    SOCKET clientSocket = INVALID_SOCKET;
    struct sockaddr_in server;
    char buffer[1024] = { 0 };

    // Initialize Winsock
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        logCallback("WSAStartup failed");
        return;
    }

    // Create Socket
    clientSocket = socket(AF_INET, SOCK_STREAM, 0);
    if (clientSocket == INVALID_SOCKET) {
        logCallback("Socket creation failed");
        WSACleanup();
        return;
    }

    // Setup server structure
    server.sin_family = AF_INET;
    server.sin_port = htons(queryServicePort);
    inet_pton(AF_INET, queryServiceIP, &server.sin_addr);

    // Connect to the Query Service
    if (connect(clientSocket, (struct sockaddr*)&server, sizeof(server)) == SOCKET_ERROR) {
        logCallback("Connect failed");
        closesocket(clientSocket);
        WSACleanup();
        return;
    }

    // Notify the server that a new client has joined
    const char* joinMsg = "New Client Joined";
    if (send(clientSocket, joinMsg, strlen(joinMsg), 0) == SOCKET_ERROR) {
        logCallback("Failed to notify server of new client");
        closesocket(clientSocket);
        WSACleanup();
        return;
    }

    // Listen for messages from the server
    while (true) {
        int bytesReceived = recv(clientSocket, buffer, sizeof(buffer), 0);
        if (bytesReceived > 0) {
            // Process the message received
            logCallback("Message from server: ");
            logCallback(buffer);
        }
        else if (bytesReceived == 0) {
            logCallback("Server closed the connection");
            break;
        }
        else {
            logCallback("Receive failed");
            break;
        }
    }

    // Close the client socket
    closesocket(clientSocket);
    // Cleanup Winsock
    WSACleanup();

}

extern "C" void CleanupClient() {

    
}*/

unsigned short QuerryServerPort() {
  
    
    return serverServicePort;
}
const char* QuerryServerIP()
{
    return GetLocalIPAddress();
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

