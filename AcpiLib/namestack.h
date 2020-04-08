/*++

2008-2020  NickelS

Module Name:

    namestack.h

Abstract:

    ASL disassemble scope stack function definition file

Environment:

    User mode only.

--*/

#ifndef _NAME_STACK_H_
#define _NAME_STACK_H_
#pragma once
#include <windows.h>
#include <basetyps.h>
#include <windowsx.h>
#include <Strsafe.h>
#include <stdio.h>
#include <stdlib.h>
#define MAX_NAME_SPACE_PATH 256     // Max 64 level
#define MAX_NAME_LENGTH MAX_NAME_SPACE_PATH* 5 + 1     
#pragma pack (1)
typedef struct _ACPI_NAME_SPACE_PATH{
    struct _ACPI_NAME_SPACE_PATH *pPrev;
    char                         chNameSpace[MAX_NAME_SPACE_PATH];
} ACPI_NAME_SPACE_PATH, *PACPI_NAME_SPACE_PATH;

#pragma pack ()

BOOL PushAcpiNameSpace (
    __in char    *chAcpiNameSpace,
    __out PACPI_NAME_SPACE_PATH AcpiNameSpace
    );

BOOL PopAcpiNameSpace (
    __out PACPI_NAME_SPACE_PATH AcpiNameSpace 
    );

VOID RemoveStack ();

VOID ResetName();

VOID PushName(ULONG Name);

BOOL PopName(CHAR *Name);
#endif
