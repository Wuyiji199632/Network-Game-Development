#include "pch.h"
#include "QuerryServer.h"
#include <ws2tcpip.h>
QueryServer::QueryServer(const std::string& discoveryServerIp, unsigned short discoveryServerPort)
{
    // Initialize Winsock and create a socket for querying the server
    WSADATA wsaData;
    int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (result != 0) {
        std::cerr << "WSAStartup failed with error: " << result << std::endl;
        // You may choose to throw an exception or return an error code
        throw std::runtime_error("WSAStartup failed");
    }

}

QueryServer::~QueryServer()
{
    CloseConnection();
    WSACleanup();
}

bool QueryServer::QueryPort(unsigned short& outServerPort)
{
    if (SetupConnection()) {

        // Send a request to the discovery server to get the port       
        const char* query = "QUERY_PORT";
        int sendResult = send(discoverySocket, query, strlen(query), 0);
        if (sendResult == SOCKET_ERROR) {
            std::cerr << "Send failed with error: " << WSAGetLastError() << std::endl;
            CloseConnection();
            return false;
        }

        // Wait for the response
        char buffer[1024];
        int bytesReceived = recv(discoverySocket, buffer, sizeof(buffer) - 1, 0);
        if (bytesReceived > 0) {

            buffer[bytesReceived] = '\0'; // Null-terminate the string
            try {
                // Convert the received string to a port number
                outServerPort = static_cast<unsigned short>(std::stoi(buffer));
            }
            catch (const std::invalid_argument& e) {
                std::cerr << "Invalid port received: " << buffer << std::endl;
                CloseConnection();
                return false;
            }
            catch (const std::out_of_range& e) {
                std::cerr << "Port out of range: " << buffer << std::endl;
                CloseConnection();
                return false;
            }

            CloseConnection();
            return true;
        }
        else if (bytesReceived == 0) {
            std::cerr << "Connection closed by server." << std::endl;
        }
        else {
            std::cerr << "Recv failed with error: " << WSAGetLastError() << std::endl;
        }
        // Handle error if receive failed
        CloseConnection();
    }

    return false;	
}

bool QueryServer::SetupConnection()
{
    discoverySocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (discoverySocket == INVALID_SOCKET) {
        std::cerr << "Socket creation failed with error: " << WSAGetLastError() << std::endl;
        return false;
    }

    sockaddr_in serverAddress = {}; // Zero-initialize structure
    serverAddress.sin_family = AF_INET;
    inet_pton(AF_INET, discoveryServerIp.c_str(), &serverAddress.sin_addr);
    serverAddress.sin_port = htons(discoveryServerPort);

    int connectResult = connect(discoverySocket, reinterpret_cast<sockaddr*>(&serverAddress), sizeof(serverAddress));
    if (connectResult == SOCKET_ERROR) {
        std::cerr << "Connect failed with error: " << WSAGetLastError() << std::endl;
        closesocket(discoverySocket);
        discoverySocket = INVALID_SOCKET;
        return false;
    }

    return true;
}

void QueryServer::CloseConnection()
{
    if (discoverySocket != INVALID_SOCKET) {
        closesocket(discoverySocket);
        discoverySocket = INVALID_SOCKET;
    }
}
