// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "ProtocolService.h"
#include "QuerryServer.h"
#include <string>
#include <unordered_map>

extern "C" {

    __declspec(dllexport) void InitializeServer(const char* password,const char* sessionID);

    __declspec(dllexport) void CleanUpServer();

   
    __declspec(dllexport) bool StoreSessionCredentials(const char* sessionID, const char* password);

    
    __declspec(dllexport) bool ValidateSessionIDAndPassword(const char* sessionId, const char* password);

    __declspec(dllexport) void SendSessionInfo(const char* sessionID, const char* sessionPassword, SOCKET clientSocket);
    __declspec(dllexport) void BroadcastSessionInfo(const char* sessionID, const char* sessionPassword);
    __declspec(dllexport) void InitializeClient(const char* queryServiceIP, unsigned short queryServicePort);
    __declspec(dllexport) void SendClientMessage(const char* message);

    //__declspec(dllexport) void InitializeClient(const char* queryServiceIP, int queryServicePort);

    __declspec(dllexport) void ConnectToServer();

    //__declspec(dllexport) void CleanupClient();

    __declspec(dllexport) bool ReceiveMessagesFromServer(SOCKET clientSocket);
    __declspec(dllexport) bool SendSessionCredentials(const char* sessionID, const char* password);
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


