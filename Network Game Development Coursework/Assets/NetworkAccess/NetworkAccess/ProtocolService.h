#pragma once
#include "pch.h"
#include <iostream>
#include <WinSock2.h>
#include <winsock.h>
#include <vector>
#include <string>



extern "C" {


    /*Server Code*/
    __declspec(dllexport) void InitializeServer();
   
    __declspec(dllexport) void AcceptClients(SOCKET listenSocket);
    

    __declspec(dllexport) void BroadcastBanditSelection(const char* playerID, const char* banditType);

    


    /*Client Code*/
    __declspec(dllexport) void InitializeClient(const char* queryServiceIP, int queryServicePort);
    __declspec(dllexport) void ConnectToServer();

    __declspec(dllexport) void CleanupClient();

    __declspec(dllexport) bool ReceiveMessagesFromServer(SOCKET clientSocket);

    __declspec(dllexport) SOCKET GetClientSocket();

    __declspec(dllexport) unsigned short QuerryServerPort();

    __declspec(dllexport) std::string QuerryServerIP();
    
}