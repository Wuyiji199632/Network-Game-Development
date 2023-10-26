#include "pch.h"
#include "Server.h"
#include "pch.h"

void InitializeServer()
{
	
	
	if (logCallback != nullptr) {

		logCallback("Server Initialized!");
		//SetLogCallback(logCallback);
	}
	
}
