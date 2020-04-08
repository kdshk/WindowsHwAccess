/*++

2008-2020  NickelS

Module Name:

    dllmain.c

Abstract:

    Dll entry

Environment:

    User mode only.

--*/
#include "pch.h"
#include "service.h"

void ReleaseAmlBuf();

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:        
        break;
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        ReleaseAmlBuf();
        CloseDll();
        break;
    }
    return TRUE;
}

