#pragma once
#include <iostream>
#include <WinSock2.h>
#include <winsock.h>




extern "C" {

    
    __declspec(dllexport) void InitializeServer();

    __declspec(dllexport) void BroadcastBanditSelection(const char* playerID, const char* banditType);
    
}