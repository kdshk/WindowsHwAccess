/*++

2008-2020  NickelS

Module Name:

	mdmap.c

Abstract:

	Method map function head file

Environment:

	User mode only.
--*/
#pragma once
#ifndef  _MDMAP_H_
#define _MDMAP_H_

typedef struct _ACPI_METHOD_MAP_ {
	struct _ACPI_METHOD_MAP_* pParent;
	struct _ACPI_METHOD_MAP_* pChild;
	struct _ACPI_METHOD_MAP_* pNext;
	struct _ACPI_METHOD_MAP_* pPrev;
	union {
		UINT32			NameSeg;
		UCHAR			Name[4];
	};
	USHORT			Args;		// number of same name
	USHORT			Count;
	ACPI_NAMESPACE	*pAcpiNS[ANYSIZE_ARRAY];	// point to acpi name space area		
} ACPI_METHOD_MAP, *PACPI_METHOD_MAP;

void AddMethodMap(UCHAR* NameSeg, USHORT Args, ACPI_NAMESPACE* pAcpiNS);

void DebugMehodMap();

void ReleaseMethodMap();

ACPI_METHOD_MAP* GetMethodMap(UINT32 NameSeg);
#endif 