#pragma once
#include "pch.h"
#include <iostream>
#include <WinSock2.h>
#include <winsock.h>
#include <vector>
#include <string>
#include <string>
#include <unordered_map>


extern "C" {


    /*Server Code*/
    __declspec(dllexport) void InitializeServer(const char* password, const char* sessionID);

    __declspec(dllexport) void CleanUpServer();
   
    __declspec(dllexport) bool StoreSessionCredentials(const char* sessionID, const char* password);
   

    __declspec(dllexport) bool ValidateSessionIDAndPassword(const char* sessionId, const char* password);

    __declspec(dllexport) void SendSessionInfo(const char* sessionID, const char* sessionPassword, SOCKET clientSocket);
    __declspec(dllexport) void BroadcastSessionInfo(const char* sessionID, const char* sessionPassword);
    __declspec(dllexport) void SendClientMessage(const char* message);
   
   
    __declspec(dllexport) void AcceptClients(SOCKET listenSocket);
    

    __declspec(dllexport) void BroadcastBanditSelection(const char* playerID, const char* banditType);

    


    /*Client Code*/
    __declspec(dllexport) void InitializeClient(const char* queryServiceIP, unsigned short queryServicePort);
    __declspec(dllexport) void ConnectToServer();

    //__declspec(dllexport) void CleanupClient();

    __declspec(dllexport) bool ReceiveMessagesFromServer(SOCKET clientSocket);
    __declspec(dllexport) bool SendSessionCredentials(const char* sessionID, const char* password);
    __declspec(dllexport) SOCKET GetClientSocket();

    __declspec(dllexport) unsigned short QuerryServerPort();

    //__declspec(dllexport) std::string QuerryServerIP();

    __declspec(dllexport) const char* QuerryServerIP();
    
}