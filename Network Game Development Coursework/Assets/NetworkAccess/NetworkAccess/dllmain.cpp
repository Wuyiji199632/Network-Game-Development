// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "ProtocolService.h"
#include "QuerryServer.h"


extern "C" {

    __declspec(dllexport) void InitializeServer();

    __declspec(dllexport) void CleanUpServer();

    //__declspec(dllexport) void InitializeClient(const char* queryServiceIP, int queryServicePort);

    __declspec(dllexport) void ConnectToServer();

    //__declspec(dllexport) void CleanupClient();

    __declspec(dllexport) bool ReceiveMessagesFromServer(SOCKET clientSocket);

    __declspec(dllexport) SOCKET GetClientSocket();

    __declspec(dllexport) unsigned short QuerryServerPort();

    //__declspec(dllexport) std::string QuerryServerIP();

    __declspec(dllexport) const char* QuerryServerIP();

}





BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}


