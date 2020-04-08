/*++

2008-2020  NickelS

Module Name:

	AmlHandlerData.c

Abstract:

	Aml Handler Data structure

Environment:

	User mode only.

--*/
#include "pch.h"
#include <tchar.h>
#include <assert.h>
#include "acpilib.h"
#include "service.h"
#include "mdmap.h"
#include "namestack.h"
#include "AmlParser.h"
#include "../AcpiView/Acpi.h"

void NotUseOp(LPBYTE* lpAml)
{
	assert (0);
}
#define HandleNameStr HandleNameString 
#define HandleCData HandleComputationalData
AML_PARSER gAmlTable[] = {
	//0x00			0x01			0x02			0x03
	&HandleCData,	& HandleCData,	&NotUseOp,		&NotUseOp,
	//0x04			0x05			0x06			0x07
	&NotUseOp,		&NotUseOp,		&HandleAlias,	&NotUseOp,
	//0x08			0x09			0x0a			0x0b
	&HandleName,	&NotUseOp,		& HandleCData, & HandleCData,
	//0x0c			0x0d			0x0e			0x0f
	& HandleCData ,	&HandleString,	& HandleCData,	&NotUseOp,
	//0x10			0x11			0x12			0x13
	&HandleScope,	&HandleBuffer,	&HandlePackage, &HandleVarPackage,
	//0x14			0x15			0x16			0x17
	&HandleMethod,	&NotUseOp,		&NotUseOp,		&NotUseOp,	//TODO: 15 define external 
	//0x18			0x19			0x1a			0x1b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x1c			0x1d			0x1e			0x1f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x20			0x21			0x22			0x23
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x24			0x25			0x26			0x27
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x28			0x29			0x2a			0x2b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x2c			0x2d			0x2e			0x2f
	&NotUseOp,		&NotUseOp,		&HandleNamePath,&HandleNamePath,
	//0x30			0x31			0x32			0x33
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x34			0x35			0x36			0x37
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x38			0x39			0x3a			0x3b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x3c			0x3d			0x3e			0x3f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x40			0x41			0x42			0x43
	&NotUseOp,		&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,
	//0x44			0x45			0x46			0x47
	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,
	//0x48			0x49			0x4a			0x4b
	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,
	//0x4c			0x4d			0x4e			0x4f
	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,
	//0x50			0x51			0x52			0x53
	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,
	//0x54			0x55			0x56			0x57
	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,
	//0x58			0x59			0x5a			0x5b
	&HandleNameSeg,	&HandleNameSeg,	&HandleNameSeg,	&HandleExtendOP,
	//0x5c			0x5d			0x5e			0x5f
	& HandleNameStr,&NotUseOp,		&HandleNameStr,	&HandleNameSeg,
	//0x60			0x61			0x62			0x63
	&HandleLocalObj,&HandleLocalObj,&HandleLocalObj,&HandleLocalObj,
	//0x64			0x65			0x66			0x67
	&HandleLocalObj,&HandleLocalObj,&HandleLocalObj,&HandleLocalObj,
	//0x68			0x69			0x6a			0x6b
	&HandleArgObj,	& HandleArgObj,	& HandleArgObj,	& HandleArgObj,
	//0x6c			0x6d			0x6e			0x6f
	&HandleArgObj,	& HandleArgObj,	& HandleArgObj,	&NotUseOp,
	//0x70			0x71			0x72			0x73
	&HandleStore,	&HandleRefOf,	&HandleAdd,		&HandleConcat,
	//0x74			0x75			0x76			0x77
	&HandleSubstract,&HandleIncrement,&HandleDecrement,&HandleMultiply,
	//0x78			0x79			0x7a			0x7b
	&HandleDivide,	&HandleShiftLeft,&HandleShiftRight,	&HandleAnd,
	//0x7c			0x7d			0x7e			0x7f
	&HandleNAnd,	&HandleOr,		&HandleNOr,		&HandleXor,
	//0x80			0x81			0x82			0x83
	&HandleNot,		&HandleFindSetLeftBit,&HandleFindSetRightBit,&HandleDerefOf,
	//0x84			0x85			0x86			0x87
	&HandleConcatRes,&HandleMod,	&HandleNotify,	&HandleSizeOf,
	//0x88			0x89			0x8a			0x8b
	&HandleIndex,	&HandleMatch,	&HandleCreateField,	&HandleCreateField,
	//0x8c			0x8d			0x8e			0x8f
	&HandleCreateField,	&HandleCreateField,	&HandleObjectType,	&HandleCreateField,
	//0x90			0x91			0x92			0x93
	&HandleLAnd,	&HandleLOr,		&HandleLNot,	&HandleLEqual,
	//0x94			0x95			0x96			0x97
	&HandleLGreater,&HandleLLess,	&HandleToBuffer,&HandleToDecimalString,
	//0x98			0x99			0x9a			0x9b
	&HandleToHexString,&HandleToInteger,&NotUseOp,&NotUseOp,
	//0x9c			0x9d			0x9e			0x9f
	&HandleToString,&HandleCopyObject,&HandleMid,	&HandleContinue,
	//0xa0			0xa1			0xa2			0xa3
	&HandleIfElse,	& HandleElse,	&HandleWhile,	&HandleNoop,
	//0xa4			0xa5			0xa6			0xa7
	&HandleReturn,	&HandleBreak,	&NotUseOp,		&NotUseOp,
	//0xa8			0xa9			0xaa			0xab
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xac			0xad			0xae			0xaf
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xb0			0xb1			0xb2			0xb3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xb4			0xb5			0xb6			0xb7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xb8			0xb9			0xba			0xbb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xbc			0xbd			0xbe			0xbf
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xc0			0xc1			0xc2			0xc3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xc4			0xc5			0xc6			0xc7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xc8			0xc9			0xca			0xcb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xcc			0xcd			0xce			0xcf
	&HandleBreakPoint,&NotUseOp,	&NotUseOp,		&NotUseOp,
	//0xd0			0xd1			0xd2			0xd3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xd4			0xd5			0xd6			0xd7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xd8			0xd9			0xda			0xdb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xdc			0xdd			0xde			0xdf
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xe0			0xe1			0xe2			0xe3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xe4			0xe5			0xe6			0xe7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xe8			0xe9			0xea			0xeb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xec			0xed			0xee			0xef
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xf0			0xf1			0xf2			0xf3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xf4			0xf5			0xf6			0xf7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xf8			0xf9			0xfa			0xfb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xfc			0xfd			0xfe			0xff
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&HandleComputationalData
};


AML_PARSER gAmlExtTable[] = {
	//0x00			0x01			0x02			0x03
	&NotUseOp,		& HandleMutex,	& HandleEvent,	&NotUseOp,
	//0x04			0x05			0x06			0x07
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x08			0x09			0x0a			0x0b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x0c			0x0d			0x0e			0x0f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x10			0x11			0x12			0x13
	&NotUseOp,		&NotUseOp,		&HandleCondRefOf,&HandleCreateField,
	//0x14			0x15			0x16			0x17
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x18			0x19			0x1a			0x1b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x1c			0x1d			0x1e			0x1f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&HandleLoadTable,
	//0x20			0x21			0x22			0x23
	&HandleLoad,	&HandleStall,	&HandleSleep,	&HandleAcquire,
	//0x24			0x25			0x26			0x27
	&HandleSignal,	&HandleWait,	&HandleReset,	&HandleRelease,
	//0x28			0x29			0x2a			0x2b
	&HandleFromBCD,	&HandleToBCD,	&HandleUnload,	&NotUseOp,
	//0x2c			0x2d			0x2e			0x2f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x30			0x31			0x32			0x33
	&HandleRevision,&HandleDebug,	&HandleFatal,	&HandleTimer,
	//0x34			0x35			0x36			0x37
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x38			0x39			0x3a			0x3b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x3c			0x3d			0x3e			0x3f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x40			0x41			0x42			0x43
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x44			0x45			0x46			0x47
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x48			0x49			0x4a			0x4b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x4c			0x4d			0x4e			0x4f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x50			0x51			0x52			0x53
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x54			0x55			0x56			0x57
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x58			0x59			0x5a			0x5b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x5c			0x5d			0x5e			0x5f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x60			0x61			0x62			0x63
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x64			0x65			0x66			0x67
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x68			0x69			0x6a			0x6b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x6c			0x6d			0x6e			0x6f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x70			0x71			0x72			0x73
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x74			0x75			0x76			0x77
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x78			0x79			0x7a			0x7b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x7c			0x7d			0x7e			0x7f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x80			0x81			0x82			0x83
	&HandleOpRegion,&HandleField,	&HandleDevice,	&HandleProcessor,
	//0x84			0x85			0x86			0x87
	&HandlePowerRes,&HandleThermalZone,&HandleField,&HandleField,
	//0x88			0x89			0x8a			0x8b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x8c			0x8d			0x8e			0x8f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x90			0x91			0x92			0x93
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x94			0x95			0x96			0x97
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x98			0x99			0x9a			0x9b
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0x9c			0x9d			0x9e			0x9f
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xa0			0xa1			0xa2			0xa3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xa4			0xa5			0xa6			0xa7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xa8			0xa9			0xaa			0xab
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xac			0xad			0xae			0xaf
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xb0			0xb1			0xb2			0xb3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xb4			0xb5			0xb6			0xb7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xb8			0xb9			0xba			0xbb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xbc			0xbd			0xbe			0xbf
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xc0			0xc1			0xc2			0xc3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xc4			0xc5			0xc6			0xc7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xc8			0xc9			0xca			0xcb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xcc			0xcd			0xce			0xcf
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xd0			0xd1			0xd2			0xd3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xd4			0xd5			0xd6			0xd7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xd8			0xd9			0xda			0xdb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xdc			0xdd			0xde			0xdf
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xe0			0xe1			0xe2			0xe3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xe4			0xe5			0xe6			0xe7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xe8			0xe9			0xea			0xeb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xec			0xed			0xee			0xef
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xf0			0xf1			0xf2			0xf3
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xf4			0xf5			0xf6			0xf7
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xf8			0xf9			0xfa			0xfb
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
	//0xfc			0xfd			0xfe			0xff
	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
};

// default data table for modify
//AML_PARSER gAmlDefault[] = {
//	//0x00			0x01			0x02			0x03
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x04			0x05			0x06			0x07
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x08			0x09			0x0a			0x0b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x0c			0x0d			0x0e			0x0f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x10			0x11			0x12			0x13
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x14			0x15			0x16			0x17
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x18			0x19			0x1a			0x1b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x1c			0x1d			0x1e			0x1f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x20			0x21			0x22			0x23
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x24			0x25			0x26			0x27
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x28			0x29			0x2a			0x2b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x2c			0x2d			0x2e			0x2f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x30			0x31			0x32			0x33
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x34			0x35			0x36			0x37
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x38			0x39			0x3a			0x3b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x3c			0x3d			0x3e			0x3f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x40			0x41			0x42			0x43
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x44			0x45			0x46			0x47
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x48			0x49			0x4a			0x4b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x4c			0x4d			0x4e			0x4f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x50			0x51			0x52			0x53
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x54			0x55			0x56			0x57
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x58			0x59			0x5a			0x5b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x5c			0x5d			0x5e			0x5f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x60			0x61			0x62			0x63
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x64			0x65			0x66			0x67
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x68			0x69			0x6a			0x6b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x6c			0x6d			0x6e			0x6f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x70			0x71			0x72			0x73
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x74			0x75			0x76			0x77
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x78			0x79			0x7a			0x7b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x7c			0x7d			0x7e			0x7f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x80			0x81			0x82			0x83
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x84			0x85			0x86			0x87
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x88			0x89			0x8a			0x8b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x8c			0x8d			0x8e			0x8f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x90			0x91			0x92			0x93
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x94			0x95			0x96			0x97
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x98			0x99			0x9a			0x9b
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0x9c			0x9d			0x9e			0x9f
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xa0			0xa1			0xa2			0xa3
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xa4			0xa5			0xa6			0xa7
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xa8			0xa9			0xaa			0xab
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xac			0xad			0xae			0xaf
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xb0			0xb1			0xb2			0xb3
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xb4			0xb5			0xb6			0xb7
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xb8			0xb9			0xba			0xbb
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xbc			0xbd			0xbe			0xbf
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xc0			0xc1			0xc2			0xc3
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xc4			0xc5			0xc6			0xc7
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xc8			0xc9			0xca			0xcb
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xcc			0xcd			0xce			0xcf
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xd0			0xd1			0xd2			0xd3
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xd4			0xd5			0xd6			0xd7
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xd8			0xd9			0xda			0xdb
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xdc			0xdd			0xde			0xdf
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xe0			0xe1			0xe2			0xe3
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xe4			0xe5			0xe6			0xe7
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xe8			0xe9			0xea			0xeb
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xec			0xed			0xee			0xef
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xf0			0xf1			0xf2			0xf3
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xf4			0xf5			0xf6			0xf7
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xf8			0xf9			0xfa			0xfb
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//	//0xfc			0xfd			0xfe			0xff
//	&NotUseOp,		&NotUseOp,		&NotUseOp,		&NotUseOp,
//};


// ACPI Pre Defined Data Parser
ACPI_OUTPUT_DATA_PARSER	gDataParser[] = {
	//{'_PSS',VerifyValidPssReturnData}
	{'SSP_',VerifyValidPssReturnData},
	{'TCP_',VerifyValidPtcReturnData},
	{'CTP_',VerifyValidPtcReturnData},
	{'SST_',VerifyValidTssReturnData},
	{'DSP_',VerifyValidPsdReturnData},
	{'DST_',VerifyValidTsdReturnData},
	{'DSC_',VerifyValidCsdReturnData},
	{'TSC_',VerifyValidCstReturnData},
	{'SRC_',ParseResourceTemplateData},
	{'SRP_',ParseResourceTemplateData}
};

int gDataParserLength = sizeof(gDataParser) / sizeof(ACPI_OUTPUT_DATA_PARSER);