// pch.cpp: source file corresponding to the pre-compiled header

#include "pch.h"

// When you are using pre-compiled headers, this source file is necessary for compilation to succeed.
LogCallback logCallback = nullptr;
MessageReceivedCallback g_messageReceivedCallback = nullptr;
void SetLogCallback(LogCallback callback)
{
	logCallback = callback;
}

void SetMessageReceivedCallback(MessageReceivedCallback callback)
{
	g_messageReceivedCallback = callback;
}
