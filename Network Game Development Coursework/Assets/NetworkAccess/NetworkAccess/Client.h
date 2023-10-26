#pragma once
#include <iostream>
#include <WinSock2.h>
#include <winsock.h>





extern "C" {

    

    __declspec(dllexport) void InitializeClient();

    __declspec(dllexport) void ConnectToServer();

   

}