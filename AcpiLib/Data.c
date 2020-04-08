/*++

2008-2020  NickelS

Module Name:

    Data.c

Abstract:

    Save and load acpi object to\from file

Environment:

    User mode only.

--*/

#include "pch.h"
#include <tchar.h>
#include <assert.h>
#include "acpilib.h"
#include "service.h"
#include "AcpiLib.h"
#include "mdmap.h"

/******************Extenal Data Definition**********************/
extern PACPI_NAMESPACE	pLocalAcpiNS;
extern UINT				uLocalAcpiNSCount;
extern UINT				uLocalMethodOffset;

/******************File struct Definition***********************/
typedef struct {
	ULONG Signature;
	ULONG nSize;
}SAVED_ACPI_NAME;

/******************Refed external Function**********************/
VOID BuildAcpiNSData(
	UINT            MethodStartIndex,
	PACPI_NAMESPACE pRoot,
	UINT            uCount,
	PACPI_NAMESPACE pKernalParent,
	PACPI_NAMESPACE pActuallyParent
);

/******************Start Of Funtion Implementation**************/
void 
APIENTRY
SaveAcpiObjects(
	TCHAR *chFile
)
/*++

Routine Description:

	Save Acpi Ojbects from to file

Arguments:

	chFile    - Saved File Name

Return Value:

	NA

--*/
{
	if (pLocalAcpiNS == NULL || uLocalAcpiNSCount == 0) {
		return;
	}
	// file layout, 
	// ACPI_NAMESPACE[gAcpiNameSpaceCount]
	// All contained data, this only can be used for loading and parsing...
	//	
	ULONG   Index;
	DWORD   dwWrites;
	SAVED_ACPI_NAME saved;
	HANDLE  hFile;
	hFile = CreateFile(
		chFile,
		GENERIC_READ | GENERIC_WRITE,
		FILE_SHARE_READ,
		NULL,
		CREATE_ALWAYS,
		FILE_ATTRIBUTE_NORMAL,
		NULL
	);
	if (hFile != INVALID_HANDLE_VALUE) {
		saved.Signature = ACPI_SIGNATURE('S', 'A', 'G', 'P');
		saved.nSize = uLocalAcpiNSCount;
		WriteFile(hFile, &saved, sizeof(saved), &dwWrites, NULL);
		WriteFile(hFile, pLocalAcpiNS, uLocalAcpiNSCount * sizeof(ACPI_NAMESPACE), &dwWrites, NULL);
		// save user space data
		for (Index = 0; Index < uLocalAcpiNSCount; Index++) {
			if (pLocalAcpiNS[Index].pUserContain != NULL) {
				if (pLocalAcpiNS[Index].Length == 0)
				{
					WriteFile(hFile, pLocalAcpiNS[Index].pUserContain,
						sizeof(ACPI_OBJ), &dwWrites, NULL);
				}
				else {
					WriteFile(hFile, pLocalAcpiNS[Index].pUserContain,
						pLocalAcpiNS[Index].Length, &dwWrites, NULL);
				}
			}
		}
		CloseHandle(hFile);
	}
}

void 
APIENTRY
LoadAcpiObjects(
	TCHAR* chFile
)
/*++

Routine Description:

	Load Acpi Ojbects from saved file and build the data structure

Arguments:

	chFile    - Saved File Name

Return Value:

	NA

--*/
{
	DWORD   dwWrites;
	SAVED_ACPI_NAME saved;
	HANDLE  hFile;
	ULONG Index;
	hFile = CreateFile(
		chFile,
		GENERIC_READ | GENERIC_WRITE,
		FILE_SHARE_READ,
		NULL,
		OPEN_EXISTING,
		FILE_ATTRIBUTE_NORMAL,
		NULL
	);
	
	if (hFile != INVALID_HANDLE_VALUE) {
		if (!ReadFile(hFile, &saved, sizeof(saved), &dwWrites, NULL)) {
			goto CloseFileHandle;
		}

		if (saved.Signature != ACPI_SIGNATURE('S', 'A', 'G', 'P')) {
			goto CloseFileHandle;
		}

		// load the memory to the size....
		pLocalAcpiNS = malloc(saved.nSize * sizeof(ACPI_NAMESPACE));

		if (pLocalAcpiNS == NULL) {
			goto CloseFileHandle;
		}
		uLocalAcpiNSCount = saved.nSize;

		if (!ReadFile(hFile, pLocalAcpiNS, saved.nSize * sizeof(ACPI_NAMESPACE), &dwWrites, NULL)) {
			free(pLocalAcpiNS);
			pLocalAcpiNS = NULL;
			uLocalAcpiNSCount = 0;
			goto CloseFileHandle;
		}
		// load all the data now..
		// build the relationship for internal....		
		__try {
			for (Index = 0; Index < uLocalAcpiNSCount; Index++) {
				pLocalAcpiNS[Index].pNext = NULL;
				pLocalAcpiNS[Index].pPrev = NULL;
				pLocalAcpiNS[Index].pChild = NULL;
				if (pLocalAcpiNS[Index].Length == 0)
				{
					pLocalAcpiNS[Index].pUserContain = malloc(sizeof(ACPI_OBJ));
					if (pLocalAcpiNS[Index].pUserContain != NULL) {
						if (!ReadFile(hFile, pLocalAcpiNS[Index].pUserContain,
							sizeof(ACPI_OBJ), &dwWrites, NULL)) {
							// TODO: record error
						}
					}
				}
				else {
					pLocalAcpiNS[Index].pUserContain = malloc(pLocalAcpiNS[Index].Length);
					if (pLocalAcpiNS[Index].pUserContain != NULL) {
						if (!ReadFile(hFile, pLocalAcpiNS[Index].pUserContain,
							pLocalAcpiNS[Index].Length, &dwWrites, NULL)) {
							// TODO: record error
						}
					}
				}
			}
			// Build The relationship of data structure....
			BuildAcpiNSData(uLocalMethodOffset, pLocalAcpiNS, uLocalAcpiNSCount, NULL, NULL);			
		}
		__except (EXCEPTION_EXECUTE_HANDLER) {
			// for potentially memory corruption
			assert(false);
		}
	CloseFileHandle:
		CloseHandle(hFile);
	}
}
