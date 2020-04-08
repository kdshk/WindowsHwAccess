/*++

2008-2020  NickelS

Module Name:

    namestack.c

Abstract:

    ASL disassemble scope stack function

Environment:

    User mode only.

--*/
#ifdef UNICODE
#undef UNICODE
#endif

#include "NameStack.h"
#include <assert.h>
PACPI_NAME_SPACE_PATH gAcpiNameSpaceLink = NULL;
extern HANDLE          ghHeap;
VOID
FreeMem (LPVOID pMemory);

BOOL PushAcpiNameSpace (
    __in char                   *chAcpiNameSpace,
    __out PACPI_NAME_SPACE_PATH AcpiNameSpace
    ) 
{
    size_t                pcbLength;
    PACPI_NAME_SPACE_PATH pAcpiNameSpace;

    if (StringCbLength ((STRSAFE_LPCSTR)AcpiNameSpace, (size_t) MAX_NAME_SPACE_PATH, &pcbLength) != S_OK) {
        return FALSE;
        }

    if (pcbLength >= MAX_NAME_SPACE_PATH) {
        return FALSE;
        }
    
    pAcpiNameSpace = (PACPI_NAME_SPACE_PATH)  malloc (sizeof (ACPI_NAME_SPACE_PATH));

    if (pAcpiNameSpace == NULL) {
        return FALSE;
        }
    
    if (gAcpiNameSpaceLink == NULL) {
        gAcpiNameSpaceLink = pAcpiNameSpace;
        pAcpiNameSpace->pPrev = NULL;        
        }
    else {
        pAcpiNameSpace->pPrev = gAcpiNameSpaceLink;        
        gAcpiNameSpaceLink = pAcpiNameSpace;
        }
    StringCbPrintf (pAcpiNameSpace->chNameSpace, MAX_NAME_SPACE_PATH, chAcpiNameSpace);   
    if (AcpiNameSpace != NULL) {
        memcpy (AcpiNameSpace, pAcpiNameSpace, sizeof (ACPI_NAME_SPACE_PATH));
    }
    return TRUE;
}

BOOL PopAcpiNameSpace (
    __out PACPI_NAME_SPACE_PATH AcpiNameSpace 
    )
{
    PACPI_NAME_SPACE_PATH pAcpiNameSpace;

    if (AcpiNameSpace == NULL || gAcpiNameSpaceLink == NULL) {
        return FALSE;
        }
    
    pAcpiNameSpace = gAcpiNameSpaceLink;
    gAcpiNameSpaceLink = gAcpiNameSpaceLink->pPrev;
    memcpy (AcpiNameSpace, pAcpiNameSpace, sizeof (ACPI_NAME_SPACE_PATH));
    free (pAcpiNameSpace);
    return TRUE;
}

VOID RemoveStack ()
{
    ACPI_NAME_SPACE_PATH    dummy;
    while (PopAcpiNameSpace (&dummy));
}

ULONG	NameStack[256];
ULONG   NameStackPos = 0;
VOID ResetName()
{
	NameStackPos = 0;
}

VOID PushName(ULONG Name)
{
	assert(NameStackPos < 256);
	NameStack[NameStackPos] = Name;
	NameStackPos++;
}

BOOL PopName(CHAR *Name)
{
	//assert(NameStackPos == 0);
	if (NameStackPos == 0) {
		return FALSE;
	}
	NameStackPos--;
	if (Name != NULL) {
		Name[0] = (UCHAR)(NameStack[NameStackPos] & 0xFF);
		Name[1] = (UCHAR)((NameStack[NameStackPos] >> 8) & 0xFF);
		Name[2] = (UCHAR)((NameStack[NameStackPos] >> 16) & 0xFF);
		Name[3] = (UCHAR)((NameStack[NameStackPos] >> 24) & 0xFF);
	}
	
	return TRUE;
}
