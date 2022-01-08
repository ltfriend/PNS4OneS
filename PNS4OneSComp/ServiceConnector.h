#ifndef __SERVICECONNECTOR_H__
#define __SERVICECONNECTOR_H__

#include "include/AddInDefBase.h"

bool StartListenService(
	const char* hostname,
	const char* port,
	const char* appId,
	const char* ibId,
	const char* userId,
	const char* clientKey,
	const WCHAR_T* userGroup,
	IAddInDefBaseEx* piConnect
);
void StopListenService();

#endif
