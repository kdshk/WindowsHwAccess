/*++

Copyright (c) 2004 - 2007, Intel Corporation                                                         
All rights reserved. This program and the accompanying materials                          
are licensed and made available under the terms and conditions of the BSD License         
which accompanies this distribution.  The full text of the license may be found at        
http://opensource.org/licenses/bsd-license.php                                            
                                                                                          
THE PROGRAM IS DISTRIBUTED UNDER THE BSD LICENSE ON AN "AS IS" BASIS,                     
WITHOUT WARRANTIES OR REPRESENTATIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED.             

Module Name:

  Acpi.h

Abstract:

  This file contains some basic ACPI definitions that are consumed by drivers
  that do not care about ACPI versions.

--*/

#ifndef _ACPI_H_
#define _ACPI_H_
#include <windows.h>
#include <basetyps.h>
#include <Stdio.h>
#include <commctrl.h>
#include "AcpiCommon.h"
#include "Acpi1_0.h"
#include "Acpi2_0.h"
#include "Acpi3_0.h"
#ifndef UINTN
#ifdef AMD64
    #define UINTN UINT64
#else
    #define UINTN UINT32
#endif
#endif
#pragma warning(disable:4214)
#define NULLNAME			0x00
#define ZERO_OP				0x00
#define ONE_OP				0x01
#define ALIAS_OP			0x06
#define NAME_OP				0x08
#define BYTE_PREFIX			0x0A
#define WORD_PREFIX			0x0B
#define DWORD_PREFIX		0x0C
#define STRING_PREFIX		0x0D
#define QWORD_PREFIX		0x0E
#define SCOPE_OP			0x10
#define BUFFER_OP			0x11
#define PACKAGE_OP			0x12
#define VAR_PACKAGE_OP		0x13
#define	METHOD_OP			0x14
#define	EXTERNAL_OP			0x15
#define DUAL_NAME_PREFIX	0x2E
#define MULTI_NAME_PREFIX	0x2F
#define EXT_OP				0x5B
#define		MUTEX_OP			0x01
#define		EVENT_OP			0x02
#define		COND_REFOF_OP		0x12
#define		CREATE_BIT_FIELD	0x13
#define		LOAD_TABLE_OP		0x1F
#define		LOAD_OP				0x20
#define		STALL_OP			0x21
#define		SLEEP_OP			0x22
#define		ACQUIRE_OP			0x23
#define		SIGNAL_OP			0x24
#define		WAIT_OP				0x25
#define		RESET_OP			0x26
#define		RELEASE_OP			0x27
#define		FROM_BCD_OP			0x28
#define		TO_BCD_OP			0x29
#define		UNLOAD_OP			0x2A
#define		REVISION_OP			0x30
#define		DEBUG_OP			0x31
#define		FATAL_OP			0x32
#define		TIMER_OP			0x33
#define		OPREGION_OP			0x80
#define		FIELD_OP			0x81
#define		DEVICE_OP			0x82
#define		PROCESSOR_OP		0x83
#define		POWER_RES_OP		0x84
#define		THERMAL_ZONE_OP		0x85
#define		INDEX_FIELD_OP		0x86
#define		BANK_FIELD_OP		0x87
#define		DATA_REGION_OP		0x88
#define	ROOT_CHAR			0x5C
#define	PARENT_PREFIX_CHAR	0x5E
#define	NAME_CHAR			0x5F
#define	LOCAL0_OP			0x60
#define	LOCAL1_OP			0x61
#define	LOCAL2_OP			0x62
#define	LOCAL3_OP			0x63
#define	LOCAL4_OP			0x64
#define	LOCAL5_OP			0x65
#define	LOCAL6_OP			0x66
#define	LOCAL7_OP			0x67
#define ARG0_OP				0x68
#define ARG1_OP				0x69
#define ARG2_OP				0x6A
#define ARG3_OP				0x6B
#define ARG4_OP				0x6C
#define ARG5_OP				0x6D
#define ARG6_OP				0x6E
#define STORE_OP			0x70
#define REFOF_OP			0x71
#define ADD_OP				0x72
#define CONCAT_OP			0x73
#define SUBSTRACT_OP		0x74
#define INCREMENT_OP		0x75
#define DECREMENT_OP		0x76
#define MULTIPLY_OP			0x77
#define DIVIDE_OP			0x78
#define SHIFT_LEFT_OP		0x79
#define SHIFT_RIGHT_OP		0x7A
#define AND_OP				0x7B
#define NAND_OP				0x7C
#define OR_OP				0x7D
#define NOR_OP				0x7E
#define XOR_OP				0x7F
#define NOT_OP				0x80
#define FIND_SET_LEFT_BIT_OP	0x81
#define FIND_SET_RIGHT_BIT_OP	0x82
#define DEREFOF_OP			0x83
#define CONCAT_RES_OP		0x84
#define MOD_OP				0x85
#define NOTIFY_OP			0x86
#define SIZE_OF_OP			0x87
#define INDEX_OP			0x88
#define MATCH_OP			0x89
#define CREATE_DWORD_FIELD_OP	0x8A
#define CREATE_WORD_FIELD_OP	0x8B
#define CREATE_BYTE_FIELD_OP	0x8C
#define CREATE_BIT_FIELD_OP	0x8D
#define OBJECT_TYPE_OP		0x8E
#define CREATE_QWORD_FIELD_OP	0x8F
#define LAND_OP				0x90
#define LOR_OP				0x91
#define LNOT_OP				0x92
#define LEQUAL_OP			0x93
#define LGREATER_OP			0x94
#define LLESS_OP			0x95
#define LNOT_EQUAL_OP		0x93
#define LLESS_EQUAL_OP		0x94
#define LGREATER_EQUAL_OP	0x95
#define TO_BUFFER_OP		0x96
#define TO_DECIMAL_STRING_OP	0x97
#define TO_HEX_STRING_OP		0x98

#define TO_INTEGER_OP			0x99
#define TO_STRING_OP			0x9C
#define COPY_OBEJECT_OP			0x9D
#define MID_OP				0x9E
#define CONTINUE_OP			0x9F
#define IF_OP				0xA0
#define ELSE_OP				0xA1
#define WHILE_OP			0xA2
#define NOOP_OP				0xA3
#define RETURN_OP			0xA4
#define BREAK_OP			0xA5
#define BREAK_POINT_OP		0xCC
#define ONES_OP				0xFF

#pragma pack (1)
typedef struct {
	UINT8	PkgLen:4;
	UINT8	PkgLen1:2;
	UINT8	DataCnt:2;
	UINT8	PkgData1;
	UINT8	PkgData2;
	UINT8	PkgData3;
} PKG_LEAD_BYTE;

typedef struct {
	UINT8	AccessType:4;
	UINT8	LockRule:1;
	UINT8	UpdateRule:2;
	UINT8	Reserved:1;
} FIELD_FLAG;

typedef struct {
	fpos_t	start;
	fpos_t	end;
} ASL_CODE_POS;

typedef struct {
	UINT32	Low;
	UINT32	High;
} ASL_UINT64;

typedef struct {
	LPBYTE		msgBuf;
	DWORD		msgBufLen;
	LPBYTE		rawBuf;
	DWORD		rawBufLen;
} ACPI_CODE, *PACPI_CODE;

typedef struct {
	PACPI_CODE	pAcpiCode;
	HTREEITEM	hTreeItem;
} TREE_VIEW_ITEM, *PTREE_VIEW_ITEM;

typedef struct {
	UINT8		LargerItemName:7;
	UINT8		Type:1;
	UINT16		Length;
	UINT8		AddressSpaceId;
	UINT8		RegisterBitWidth;
	UINT8		RegisterBitOffset;
	UINT8		AddressSize;
	UINT64		RegisterAddress;
} GENERIC_REGISTER_DESCIPTOR, *PGENERIC_REGISTER_DESCIPTOR;

typedef struct {
	UINT8		Length:3;
	UINT8		TagBits:4;
	UINT8		Type:1;
} ACPI_SMALL_RESOURCE_DATA;

typedef struct {
	UINT8		TagBit:7;
	UINT8		Type:1;
} ACPI_LARGE_RESOURCE_DATA;

typedef union {
	UINT8						Type;
	ACPI_SMALL_RESOURCE_DATA	srd;
	ACPI_LARGE_RESOURCE_DATA	lrd;
} ACPI_RESOURCE_DATA_HEADER, *PACPI_RESOURCE_DATA_HEADER;

//
// Tag = 04
// 
typedef struct {
	ACPI_RESOURCE_DATA_HEADER	Header;
	UINT16						IrqMask;
	UINT8						IrqInfo;
} ACPI_IRQ_DESCRIPTOR;

//
// Tag = 05
// 
typedef struct {
	ACPI_RESOURCE_DATA_HEADER	Header;
	UINT8						ChannelMask;
	UINT8						_SIZ:2;
	UINT8						_BM:1;
	UINT8						Ignored:2;
	UINT8						_TYP:2;
	UINT8						Rsvd:1;
} ACPI_DMA_DESCRIPTOR;

//
// Tag = 06
// 
typedef struct {
	ACPI_RESOURCE_DATA_HEADER	Header;
} ACPI_START_DEPENDENT_FUNCTIONS_DESCRIPTOR;

/*
typedef struct {
	union {
		UINT8						Type;
		ACPI_SMALL_RESOURCE_DATA	srd;
		ACPI_LARGE_RESOURCE_DATA	lrd;
	}	
} ACPI_RESOURCE_DESCRIPTION, *PACPI_RESOURCE_DESCRIPTION;
*/
#pragma pack ()

#endif
