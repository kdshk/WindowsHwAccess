/*++

2008-2020  NickelS

Module Name:

    AcpiLib.h

Abstract:

    Acpi lib definition file

Environment:

    User mode only.

--*/

#ifndef _ACPI_LIB_H_
#define _ACPI_LIB_H_
#define METHOD_START_INDEX  0xC1
#include <ctype.h>
#include <windows.h>
#include <tchar.h>
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strsafe.h>
#include <Setupapi.h>
#include <devioctl.h>
#include "..//HwAcc/ioctl.h"
#include "mdmap.h"

//#define FIELD_OFFSET(type, field) (ULONG_PTR)(&((type *)0)->field)

//#define EXPORT_DLL #ifdef ACPILIB_EXPORTS __declspec(dllexport) #else __declspec(dllimport) #endif
#ifdef ACPILIB_EXPORTS
#define ACPI_LIB_FUNCTION __declspec(dllexport)
#else
#define ACPI_LIB_FUNCTION __declspec(dllimport)
#endif

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
VOID
APIENTRY
CloseAcpiService(
    HANDLE hDriver
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
HANDLE
APIENTRY
OpenAcpiService(
    );

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOLEAN
APIENTRY
OpenAcpiDevice(
    __in HANDLE hDriver
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOL
APIENTRY
LoadNotifiyMethod(
    HANDLE      hDriver,
    AML_SETUP* pAmlSetup,
    ULONG       uSize
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
void
APIENTRY
UnloadNotifiyMethod(
    HANDLE      hDriver
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOL
APIENTRY
EvalAcpiNS(
    HANDLE          hDriver,
    ACPI_NAMESPACE* pAcpiName,
    PVOID* pReturnData,
    ULONG* puLength
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOL
APIENTRY
EvalAcpiNSArg(
    HANDLE          hDriver,
    PACPI_METHOD_ARG_COMPLEX pComplexData,
    PVOID* pReturnData,
    UINT Size
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
PVOID
APIENTRY
ReadAcpiMemory(
    HANDLE hDriver,
    PVOID  Address,
    ULONG  Size
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOLEAN
APIENTRY
QueryAcpiNS(
    HANDLE          hDriver,
    ACPI_NS_DATA*   pAcpiNsData,
    UINT            MethodStartIndex
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
ACPI_METHOD_MAP*
APIENTRY
GetMethod(
    UINT32 NameSeg
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
void
APIENTRY
ReleaseAcpiNS(
);


#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOLEAN
APIENTRY
QueryAcpiNSInLib(
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
void
APIENTRY
SaveAcpiObjects(
    TCHAR* chFile
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
void
APIENTRY
LoadAcpiObjects(
    TCHAR* chFile
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
UINT
APIENTRY
GetNamePath(
    UINT32* puParent,
    BYTE* pChild
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
int
APIENTRY
GetNamePathFromPath(
    TCHAR* puParent,
    TCHAR* puChild
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
USHORT
APIENTRY
GetNameType(
    TCHAR* pParent
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOLEAN
APIENTRY
GetNameIntValue(
    TCHAR* pParent,
    ULONG64* uLong64
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
int
APIENTRY
GetNameStringValue(
    TCHAR* pParent,
    TCHAR* pString
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
int
APIENTRY
GetNameAddrFromPath(
    TCHAR* pParent,
    PVOID* pChild
);


#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
PVOID
APIENTRY
GetNameAddr(
    TCHAR* pParent
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
void
APIENTRY
GetNameFromAddr(
    ACPI_NAMESPACE* pAcpiNS,
    TCHAR* pName
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
UINT64
APIENTRY
AslFromPath(
    TCHAR* pPath,
    TCHAR* pAsl
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
UINT64
APIENTRY
EvalAcpiNSAndParse(
    TCHAR* pPath,
    TCHAR* pAsl
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
BOOLEAN
APIENTRY
GetArgsCount(
    TCHAR* pParent,
    ULONG64* uLong64
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
PVOID
PutBuffArg(
    PVOID   pArgs,
    UINT    Length,
    UCHAR* pBuf
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
PVOID
PutStringArg(
    PVOID   pArgs,
    UINT    Length,
    TCHAR*  pString
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
PVOID
PutIntArg(
    PVOID   pArgs,
    UINT64  value
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
UINT64
APIENTRY
EvalAcpiNSArgAndParse(
    TCHAR* pPath,
    ACPI_EVAL_INPUT_BUFFER_COMPLEX *pComplexInput,
    TCHAR* pAsl
);

#ifdef ACPILIB_EXPORTS
__declspec(dllexport)
#else
__declspec(dllimport)
#endif
void
APIENTRY
FreeMemory(
    PVOID pMem
);

ACPI_LIB_FUNCTION
BOOLEAN
APIENTRY
NotifyDevice(
	TCHAR* pchPath,
	ULONG	ulCode
);

ACPI_LIB_FUNCTION
int
APIENTRY
GetNSType(
    TCHAR* pchPath
);

ACPI_LIB_FUNCTION
VOID *
APIENTRY
GetNSValue(
    TCHAR* pchPath,
    USHORT *pulLength
);

ACPI_LIB_FUNCTION
VOID*
APIENTRY
GetRawData(
    TCHAR* pchPath,
    USHORT* puType,
    ULONG* puLength
);

#endif
