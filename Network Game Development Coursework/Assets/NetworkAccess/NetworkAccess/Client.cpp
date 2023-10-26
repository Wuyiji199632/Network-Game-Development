#include "pch.h"
#include "Client.h"
#include <iostream>

void InitializeClient()
{
	//cout << "Client Initialized!" << endl;

	if (logCallback != nullptr) {

		logCallback("Client Initialized");
		//SetLogCallback(logCallback);
	}
}

void ConnectToServer()
{
	//cout << "Client connected to server!" << endl;
	if (logCallback != nullptr) {

		logCallback("Connected To Server!");
		//SetLogCallback(logCallback);
	}
}

