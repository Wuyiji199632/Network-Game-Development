// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"
#include <iostream>
#include <string>
#include <WinSock2.h>
#include <winsock.h>

typedef void (*LogCallback)(const char* message);

extern LogCallback logCallback;

extern "C" {

	__declspec(dllexport) void SetLogCallback(LogCallback callback);
}



#endif //PCH_H
