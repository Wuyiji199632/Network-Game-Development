#pragma once
#include <iostream>
#include <WinSock2.h>
#include <winsock.h>
#include <vector>



extern "C" {

    __declspec(dllexport) void AcceptClients(SOCKET listenSocket);
    __declspec(dllexport) void InitializeServer();

    __declspec(dllexport) void BroadcastBanditSelection(const char* playerID, const char* banditType);
    
}