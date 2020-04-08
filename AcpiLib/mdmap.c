/*++

2008-2020  NickelS

Module Name:

	mdmap.c

Abstract:

	Method map function

Environment:

	User mode only.

--*/
#include <windows.h>
#include <basetyps.h>
#include "../HwAcc/ioctl.h"
#include "mdmap.h"
#include <stdlib.h>
#include <stdio.h>
#include <assert.h>

ULONG nSize = 0;
ULONG nAdd = 0;
ULONG nRelease = 0;
ACPI_METHOD_MAP gAcpiMethodMap = { NULL, NULL, NULL, NULL, '\\___', 0, 0, NULL };	// Root

// NewMap

ACPI_METHOD_MAP* NewMap(ACPI_METHOD_MAP*  pLevel, UCHAR NameSeg, USHORT Args, ACPI_NAMESPACE* pAcpiNS, USHORT Level, ACPI_METHOD_MAP* pExist)
{
	ACPI_METHOD_MAP* pMap;
	//assert((pExist != NULL && Level != 4));
	if (pExist == NULL) {
		pMap = (ACPI_METHOD_MAP*)malloc(sizeof(ACPI_METHOD_MAP));
		nSize += sizeof(ACPI_METHOD_MAP);
		nAdd++;
	}
	else {
		pMap = (ACPI_METHOD_MAP*)malloc(sizeof(ACPI_METHOD_MAP) + pExist->Count * sizeof(PVOID));		
		nSize += sizeof(PVOID);
	}
	if (pMap == NULL) {
		return NULL;
	}
	if (pExist != NULL) {
		// copy the old method information to new
		memcpy(pMap, pExist, sizeof(ACPI_METHOD_MAP) + (pExist->Count - 1) * sizeof(PVOID));
		// assign the last method acpi_namespace address
		pMap->pAcpiNS[pExist->Count] = pAcpiNS;
		// put it in chain and remove the old one
		pMap->pChild = pExist->pChild;
		if (pExist->pPrev != NULL) {
			pExist->pPrev->pNext = pMap;
		}
		if (pExist->pNext != NULL) {
			pExist->pNext->pPrev = pMap;
		}
		if (pExist->pParent->pChild == pExist) {
			pExist->pParent->pChild = pMap;
		}		
		free(pExist);
		return pMap;
	}
	memset(pMap, 0, sizeof(ACPI_METHOD_MAP));
	
	pMap->Name[0] = NameSeg;
	pMap->Args = Level;
	if (Level == 4) {
		pMap->Args = Args;
		pMap->pAcpiNS[0] = pAcpiNS;
	}
	pMap->Count = 1;
	if (pLevel->pChild == NULL) {
		pLevel->pChild = pMap;
	}
	else {
		// insert at head
		pLevel->pChild->pPrev = pMap;
		pMap->pNext = pLevel->pChild;
		pLevel->pChild = pMap;
	}
	pMap->pParent = pLevel;
	return pMap;
}

ACPI_METHOD_MAP *FindMethodInMap(UCHAR* NameSeg, USHORT Args, ACPI_NAMESPACE* pAcpiNS)
{
	UNREFERENCED_PARAMETER(Args);
	UNREFERENCED_PARAMETER(pAcpiNS);
	ACPI_METHOD_MAP* pRoot;	// 37 roots
	ACPI_METHOD_MAP* pLevel = &gAcpiMethodMap;
	pRoot = gAcpiMethodMap.pChild;
	//if (pRoot)
	while (pRoot != NULL) {
		if (pRoot->Name[0] == NameSeg[0]) {
			break;
		}
		pRoot = pRoot->pNext;
	}
	if (pRoot == NULL) {
		pRoot = NewMap(pLevel, NameSeg[0], 0, pAcpiNS, 1, NULL);
	}
	else {
		pRoot->Count++;
	}
	pLevel = pRoot;
	pRoot = pRoot->pChild;
	while (pRoot != NULL) {
		if (pRoot->Name[0] == NameSeg[1]) {
			break;
		}
		pRoot = pRoot->pNext;
	}
	if (pRoot == NULL) {
		// insert the map at the level 2
		pRoot = NewMap(pLevel, NameSeg[1], 0, pAcpiNS, 2, NULL);
	}
	else {
		pRoot->Count++;
	}
	pLevel = pRoot;
	pRoot = pRoot->pChild;
	while (pRoot != NULL) {
		if (pRoot->Name[0] == NameSeg[2]) {
			break;
		}
		pRoot = pRoot->pNext;
	}
	if (pRoot == NULL) {
		pRoot = NewMap(pLevel, NameSeg[2], 0, pAcpiNS, 3, NULL);
		// insert the map at the level 3
	}
	else {
		pRoot->Count++;
	}
	pLevel = pRoot;
	pRoot = pRoot->pChild;
	while (pRoot != NULL) {
		if (pRoot->Name[0] == NameSeg[3]) {
			break;
		}
		pRoot = pRoot->pNext;
	}
	if (pRoot != NULL) {	
		// ok, I have extra value, need to know how many in total
		// has multiple head, need to adjust the size of header
		pRoot = NewMap(pLevel, NameSeg[3], Args, pAcpiNS, 4, pRoot);
		pRoot->Count++;
		return pRoot;
	}
	NewMap(pLevel,NameSeg[3], Args, pAcpiNS, 4, NULL);
	// insert the map at the level 3
	return NULL;
}

void AddMethodMap(UCHAR* NameSeg, USHORT Args, ACPI_NAMESPACE* pAcpiNS)
{
	// find in map...
	ACPI_METHOD_MAP* pMap;
	pMap = FindMethodInMap(NameSeg, Args, pAcpiNS);
}

void DebugMehodMap() {
	printf("nsize %d\n", nSize);
	printf("nAdd %d\n", nAdd);
	printf("nRelease %d\n", nRelease);
}

void ReleaseMethodMapSub(ACPI_METHOD_MAP* pRoot, int Level)
{
	ACPI_METHOD_MAP* pRelease;	
	ACPI_METHOD_MAP* pMap = pRoot;
	   	
	//pMap = pRoot->pChild;
	if (Level == 4) {
		while (pMap->pPrev != NULL) {
			pMap = pMap->pPrev;	// move to header
		}
		do {
			pRelease = pMap;
			pMap = pMap->pNext;
			nRelease++;
			__try {
				free(pRelease);
				pRelease = NULL;
			}
			__except (EXCEPTION_EXECUTE_HANDLER) {

			}
			if (pMap != NULL) {
				pMap->pPrev = NULL;
			}
		} while (pMap != NULL);
		//free(pMap);
		return;
	}
	if (pMap == NULL) {
		return;
	}
	ACPI_METHOD_MAP* pLocal = NULL;
	__try {
		while (pMap->pPrev != NULL) {
			pLocal = pMap;
			pMap = pMap->pPrev;	// move to header
		}
	}
	__except (EXCEPTION_EXECUTE_HANDLER) {
		return;
	}
	do {
		pRelease = pMap;
		if (pMap->pChild != NULL) {
			ReleaseMethodMapSub(pMap->pChild, Level + 1);
		}		
		pMap = pMap->pNext;	
		nRelease++;
		__try {
			free(pRelease);
			pRelease = NULL;
		}
		__except (EXCEPTION_EXECUTE_HANDLER) {

		}
		if (pMap != NULL) {
			pMap->pPrev = NULL;
		}
	} while (pMap != NULL);
	//nRelease++;
	//free(pMap);
}

void ReleaseMethodMap()
{
	ACPI_METHOD_MAP* pMap;
	pMap = gAcpiMethodMap.pChild;
	ReleaseMethodMapSub(pMap, 1);
	gAcpiMethodMap.pChild = NULL;
	//DebugMehodMap();
}

ACPI_METHOD_MAP* GetMethodMap(UINT32 NameSeg)
{
	UCHAR Name[4];
	ACPI_METHOD_MAP* pMap = gAcpiMethodMap.pChild;
	Name[0] = (UCHAR)NameSeg & 0xFF;
	Name[1] = (UCHAR)(NameSeg >> 8)& 0xFF;
	Name[2] = (UCHAR)(NameSeg >> 16) & 0xFF;
	Name[3] = (UCHAR)(NameSeg >> 24);

	// Find Method in Level 1
	if (pMap == NULL) {
		return NULL;
	}

	while (pMap != NULL) {
		if (pMap->Name[0] == Name[0]) {
			break;
		}
		pMap = pMap->pNext;
	}

	// Find Method in Level 2
	if (pMap == NULL) {
		return NULL;
	}
	pMap = pMap->pChild;	
	while (pMap != NULL) {
		if (pMap->Name[0] == Name[1]) {
			break;
		}
		pMap = pMap->pNext;
	}

	// Find Method in Level 3
	if (pMap == NULL) {
		return NULL;
	}
	pMap = pMap->pChild;
	while (pMap != NULL) {
		if (pMap->Name[0] == Name[2]) {
			break;
		}
		pMap = pMap->pNext;
	}

	// Find Method in Level 4
	if (pMap == NULL) {
		return NULL;
	}
	pMap = pMap->pChild;
	while (pMap != NULL) {
		if (pMap->Name[0] == Name[3]) {
			return pMap;
		}
		pMap = pMap->pNext;
	}
	return NULL;
}