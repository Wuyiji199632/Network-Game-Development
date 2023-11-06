#pragma once



#include <string>
#include <WinSock2.h>
#include <winsock.h>
#include "ProtocolService.h"


class QueryServer {
public:
    QueryServer(const std::string& discoveryServerIp, unsigned short discoveryServerPort);
    ~QueryServer();

    bool QueryPort(unsigned short& outServerPort);

public:
    std::string discoveryServerIp;
    unsigned short discoveryServerPort;
    SOCKET discoverySocket;

    
    bool SetupConnection();
    void CloseConnection();
};


