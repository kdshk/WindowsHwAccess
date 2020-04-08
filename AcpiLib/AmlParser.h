/*++

2008-2020  NickelS

Module Name:

	AmlParser.c

Abstract:

	Parse AML Code in to Asl Code head file

Environment:

	User mode only.

--*/
#pragma once
#ifndef _AML_PARSER_H
#define _AML_PARSER_H

typedef void (AML_HANDLER)(LPBYTE* lpAml);
typedef void (ACPI_OUTPUT_DATA_HANDLER)(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
typedef BOOLEAN(ACPI_BUFFER_PARSER_FUNC)(ULONG uName, ACPI_EVAL_OUTPUT_BUFFER* pBuf);
typedef VOID(ACPI_ARG_PARSER_FUNC)(ACPI_NAMESPACE* pNode, PACPI_METHOD_ARGUMENT pBuf);
typedef VOID(ACPI_DATA_PARSER_FUNC)(ACPI_NAMESPACE* pNode);


#define LARGE_RESOURCE_TYPE 1
#define SMALL_RESOURCE_TYPE 0

#define SMALL_ITEM_IRQ                  4
#define SMALL_ITEM_DMA                  5
#define SMALL_ITEM_START_DEP            6
#define SMALL_ITEM_END_DEP              7
#define SMALL_ITEM_IO                   8
#define SMALL_ITEM_FIXED_IO             9
#define SMALL_ITEM_VENDOR_DEF           0xE
#define SMALL_ITEM_END_TAG              0xF

#define LARGE_ITEM_24BIT_MEMORY         1
#define LARGE_ITEM_GENERIC_REG          2
#define LARGE_ITEM_RSVD                 3
#define LARGE_ITEM_VENDOR_DEF           4
#define LARGE_ITEM_32BIT_MEMORY         5
#define LARGE_ITEM_32BIT_FIXED_MEMORY   6
#define LARGE_ITEM_DWORD_ADDR_SPACE     7
#define LARGE_ITEM_WORD_ADDR_SPACE      8
#define LARGE_ITEM_EXT_IRQ              9
#define LARGE_ITEM_QWORD_ADDR_SPACE     0xA
#define LARGE_ITEM_EXT_ADDR_SPACE       0xB
#define MASK_VALUE(a, b, c) ((a) & (b)) == (c)
#define BIT_MASK_VALUE(a, b) ((a) & (b)) == (b)
#define NEXT_RESOURCE_DATA(a) \
    if ((a)->Tag == 0) { (a) = (PSMALL_RESOURCE_DATA_TYPE_HEADER) ((UINTN)(a) + 1 + (UINTN) ((a)->Length));} \
    else {(a) = (PSMALL_RESOURCE_DATA_TYPE_HEADER) ((UINTN)(a) + 3 + (UINTN) (((PLARGE_RESOURCE_DATA_TYPE_HEADER)(a))->Length));}

#pragma push()
#pragma pack(1)
typedef struct {
	UINT8 Length : 3;
	UINT8 ItemName : 4;
	UINT8 Tag : 1;
	UINT8 Data[1];
} SMALL_RESOURCE_DATA_TYPE_HEADER, * PSMALL_RESOURCE_DATA_TYPE_HEADER;

typedef struct {
	UINT8   ItemName : 7;
	UINT8   Tag : 1;
	UINT16  Length;
	UINT8   Data[1];
} LARGE_RESOURCE_DATA_TYPE_HEADER, * PLARGE_RESOURCE_DATA_TYPE_HEADER;

typedef struct {
	UINT8   ItemName;
	UINT16  Length;
	UINT8   ResourceType;
	UINT8   GeneralFlags;
	UINT8   TypeSpecificFlags;
	UINT32  Gra;
	UINT32  Gra1;
	UINT32  Min;
	UINT32  Min1;
	UINT32  Max;
	UINT32  Max1;
	UINT32  Tra;
	UINT32  Tra1;
	UINT32  Len;
	UINT32  Len1;
	UINT8   ResourceIdx;
	char    Name[1];
} QWORD_RESOURCE;

typedef struct {
	UINT8   ItemName;
	UINT16  Length;
	UINT8   ResourceType;
	UINT8   GeneralFlags;
	UINT8   TypeSpecificFlags;
	UINT8   Rev;
	UINT8   Rsvd;
	UINT32  Gra;
	UINT32  Gra1;
	UINT32  Min;
	UINT32  Min1;
	UINT32  Max;
	UINT32  Max1;
	UINT32  Tra;
	UINT32  Tra1;
	UINT32  Len;
	UINT32  Len1;
	UINT32  Att;
	UINT32  Att1;
	UINT8   ResourceIdx;
	char    Name[1];
} EXTENDED_ADDRESS_SPACE;

typedef struct {
	UINT8   ItemName;
	UINT16  Length;
	UINT8   ResourceType;
	UINT8   GeneralFlags;
	UINT8   TypeSpecificFlags;
	UINT32  Gra;
	UINT32  Min;
	UINT32  Max;
	UINT32  Tra;
	UINT32  Len;
	UINT8   ResourceIdx;
	char    Name[1];
} DWORD_RESOURCE;

typedef struct {
	UINT8   ItemName;
	UINT16  Length;
	UINT8   ResourceType;
	UINT8   GeneralFlags;
	UINT8   TypeSpecificFlags;
	UINT16  Gra;
	UINT16  Min;
	UINT16  Max;
	UINT16  Tra;
	UINT16  Len;
	UINT8   ResourceIdx;
	char    Name[1];
} WORD_RESOURCE;

#pragma pack()
#pragma pop()

typedef struct {
	AML_HANDLER	*AmlHandler;
} AML_PARSER;

typedef struct {
	UINT32					  uSig;
	ACPI_OUTPUT_DATA_HANDLER* AmlHandler;
} ACPI_OUTPUT_DATA_PARSER;

typedef struct _Acpi_Buffer_Parser {
	ULONG Signature;
	ACPI_BUFFER_PARSER_FUNC* Func;
} ACPI_BUFFER_PARSER, * PACPI_BUFFER_PARSER;

typedef struct _Acpi_Arg_Parser {
	ULONG id;
	ACPI_ARG_PARSER_FUNC* Func;
}ACPI_ARGUMENT_PARSER, * PACPI_ARGUMENT_PARSER;

typedef struct _Acpi_Data_Parser {
	ULONG id;
	ACPI_DATA_PARSER_FUNC* Func;
}ACPI_DATA_PARSER, * PACPI_DATA_PARSER;

#pragma pack(push)
#pragma pack(1)
#pragma warning(disable:4214)

typedef struct _Acpi_Method_Pkg1_Lead_ {
	UCHAR Rsvd[3];
	UCHAR MethodOp;
	UCHAR PkgLength;
	UCHAR uName[4];
	UCHAR Flags;
}ACPI_METHOD_PKG1;

typedef struct _Acpi_Method_Pkg2_Lead_ {
	UCHAR Rsvd[2];
	UCHAR MethodOp;
	UCHAR PkgLength[2];
	UCHAR uName[4];
	UCHAR Flags;
}ACPI_METHOD_PKG2;

typedef struct _Acpi_Method_Pkg3_Lead_ {
	UCHAR Rsvd;
	UCHAR MethodOp;
	UCHAR PkgLength[3];
	UCHAR uName[4];
	UCHAR Flags;
}ACPI_METHOD_PKG3;

typedef struct _Acpi_Method_Pkg4_Lead_ {
	UCHAR MethodOp;
	UCHAR PkgLength[4];
	UCHAR uName[4];
	UCHAR Flags;
}ACPI_METHOD_PKG4;

typedef struct _Acpi_Aml_Code_ {
	UCHAR	rsvd[0xC0];
	UCHAR	rsvd1;
	UCHAR	ArgCount : 3;
	UCHAR	SerializeFlag : 1;
	UCHAR	SyncLevel : 4;
	UCHAR	Code[ANYSIZE_ARRAY];
} ACPI_AML_CODE, * PACPI_AML_CODE;
#pragma pack(pop)
BOOLEAN
VerifyTss(
	ULONG uName,
	ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
);

BOOLEAN
VerifyPss(
	ULONG uName,
	ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
);

BOOLEAN
VerifyPtc(
	ULONG uName,
	ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
);

BOOLEAN
VerifyXsd(
	ULONG uName,
	ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
);

BOOLEAN
VerifyCst(
	ULONG uName,
	ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
);

BOOLEAN
VerifyXrs(
	ULONG uName,
	ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
);

LPSTR
GetResouceType(
	UINT8 Idx
);

BOOLEAN
VerifyGenericRegisterBuffer(
	PACPI_METHOD_ARGUMENT arg,
	LPSTR pName,
	BOOLEAN bPrint
);


VOID
AcpiOutputParser(
	ACPI_NAMESPACE* pNode,
	PACPI_EVAL_OUTPUT_BUFFER arg
);

VOID
PkgExArg(
	ACPI_NAMESPACE* pNode,
	PACPI_METHOD_ARGUMENT pArg
);

VOID
PkgArg(
	ACPI_NAMESPACE* pNode,
	PACPI_METHOD_ARGUMENT pArg
);

VOID
BufArg(
	ACPI_NAMESPACE* pNode,
	PACPI_METHOD_ARGUMENT pArg
);

VOID
StrArg(
	ACPI_NAMESPACE* pNode,
	PACPI_METHOD_ARGUMENT pArg
);

VOID
IntArg(
	ACPI_NAMESPACE* pNode,
	PACPI_METHOD_ARGUMENT pArg
);

void ArgParser(
	ACPI_NAMESPACE* pNode,
	PACPI_METHOD_ARGUMENT pArg
);
BOOL
DataParser(
	ACPI_NAMESPACE* pNode
);

VOID
PkgData(
	ACPI_NAMESPACE* pNode
);

VOID
BufData(
	ACPI_NAMESPACE* pNode
);

VOID
StrData(
	ACPI_NAMESPACE* pNode
);

VOID
IntData(
	ACPI_NAMESPACE* pNode
);

VOID
OpReg(
	ACPI_NAMESPACE* pNode
);

VOID AcpiOperationRegion(
	PACPI_OPERATION_REGION pAcpiRegion
);

VOID
Mutex(
	ACPI_NAMESPACE* pNode
);

VOID
Alias(
	ACPI_NAMESPACE* pNode
);

VOID
BankField(
	ACPI_NAMESPACE* pNode
);

VOID
SyncObj(
	ACPI_NAMESPACE* pNode
);

ACPI_NAMESPACE*
GetAcpiNsFromNsAddr(
	PVOID pVoid
);


ACPI_NAMESPACE*
GetAcpiNsFromObjData(
	PVOID pVoid
);
BOOL
GetAcpiRootPath(
	PACPI_NAMESPACE pAcpiName,
	char* chLine
);

VOID
FieldUnit(
	ACPI_NAMESPACE* pNode
);

VOID
BufField(
	ACPI_NAMESPACE* pNode
);

VOID
Method(
	ACPI_NAMESPACE* pNode
);

VOID
Device(
	ACPI_NAMESPACE* pAcpiNS
);

VOID
Field(
	ACPI_NAMESPACE* pNode
);

VOID HandleExtendOP(LPBYTE* lpAml);
VOID HandleAcquire(LPBYTE* lpAml);
VOID HandleAdd(LPBYTE* lpAml);
VOID HandleAnd(LPBYTE* lpAml);
VOID HandleConcat(LPBYTE* lpAml);
VOID HandleConcatRes(LPBYTE* lpAml);
VOID HandleCondRefOf(LPBYTE* lpAml);
VOID HandleDebug(LPBYTE* lpAml);
VOID HandleDerefOf(LPBYTE* lpAml);
VOID HandleEvent(LPBYTE* lpAml);
VOID HandleExtendOP(LPBYTE* lpAml);
VOID HandleIndex(LPBYTE* lpAml);
VOID HandleLoad(LPBYTE* lpAml);
VOID HandleLoadTable(LPBYTE* lpAml);
VOID HandleMutex(LPBYTE* lpAml);
VOID HandleNamePath(LPBYTE* lpAml);
VOID HandleNameSeg(LPBYTE* lpAml);
VOID HandleNameString(LPBYTE* lpAml);
VOID HandleRefOf(LPBYTE* lpAml);
VOID HandleRevision(LPBYTE* lpAml);
VOID HandleSimpleName(LPBYTE* lpAml);
VOID HandleStall(LPBYTE* lpAml);
VOID HandleSuperName(LPBYTE* lpAml);
VOID HandleType6Opcodes(LPBYTE* lpAml);
VOID HandleUnload(LPBYTE* lpAml);
VOID HandleWhile(LPBYTE* lpAml);
VOID HandleTarget(LPBYTE* lpAml);
VOID HandleTermArg(LPBYTE* lpAml);
VOID HandleLocalObj(LPBYTE* lpAml);
VOID HandleArgObj(LPBYTE* lpAml);
VOID HandleType2Opcodes(LPBYTE* lpAml);
VOID HandleType1Opcodes(LPBYTE* lpAml);
VOID HandleDataObject(LPBYTE* lpAml);
VOID HandleDataRefObject(LPBYTE* lpAml);
VOID HandleVarPackage(LPBYTE* lpAml);
VOID HandlePackage(LPBYTE* lpAml);
VOID HandleComputationalData(LPBYTE* lpAml);
BOOL HandleUserTermObj(LPBYTE* lpAml);
VOID HandleTermObj(LPBYTE* lpAml);
VOID HandleLOr(LPBYTE* lpAml);
VOID HandleByteData(LPBYTE* lpAml);
VOID HandleWordData(LPBYTE* lpAml);
VOID HandleDWordData(LPBYTE* lpAml);
VOID HandleQWordData(LPBYTE* lpAml);
VOID HandleMatch(LPBYTE* lpAml);
VOID HandleMid(LPBYTE* lpAml);
VOID HandleMod(LPBYTE* lpAml);
VOID HandleMultiply(LPBYTE* lpAml);
VOID HandleCreateField(LPBYTE* lpAml);
VOID HandleWait(LPBYTE* lpAml);
VOID HandleSignal(LPBYTE* lpAml);
VOID HandleReset(LPBYTE* lpAml);
VOID HandleRelease(LPBYTE* lpAml);
VOID HandleSleep(LPBYTE* lpAml);
VOID HandleFromBCD(LPBYTE* lpAml);
VOID HandleToBCD(LPBYTE* lpAml);
VOID HandleFatal(LPBYTE* lpAml);
VOID HandleOpRegion(LPBYTE* lpAml);
VOID HandleTimer(LPBYTE* lpAml);
VOID HandleField(LPBYTE* lpAml);
VOID HandleDevice(LPBYTE* lpAml);
VOID HandleProcessor(LPBYTE* lpAml);
VOID HandlePowerRes(LPBYTE* lpAml);
VOID HandleThermalZone(LPBYTE* lpAml);
VOID HandleContinue(LPBYTE* lpAml);
VOID HandleBreakPoint(LPBYTE* lpAml);
VOID HandleBreak(LPBYTE* lpAml);
VOID HandleIfElse(LPBYTE* lpAml);
VOID HandleElse(LPBYTE* lpAml);
VOID HandleNotify(LPBYTE* lpAml);
VOID HandleNoop(LPBYTE* lpAml);
VOID HandleReturn(LPBYTE* lpAml);
VOID HandleCopyObject(LPBYTE* lpAml);
VOID HandleDecrement(LPBYTE* lpAml);
VOID HandleDivide(LPBYTE* lpAml);
VOID HandleFindSetLeftBit(LPBYTE* lpAml);
VOID HandleFindSetRightBit(LPBYTE* lpAml);
VOID HandleIncrement(LPBYTE* lpAml);
VOID HandleLAnd(LPBYTE* lpAml);
VOID HandleLEqual(LPBYTE* lpAml);
VOID HandleLGreater(LPBYTE* lpAml);
VOID HandleLGreaterEqual(LPBYTE* lpAml);
VOID HandleLLess(LPBYTE* lpAml);
VOID HandleLLessEqual(LPBYTE* lpAml);
VOID HandleLNot(LPBYTE* lpAml);
VOID HandleLNotEqual(LPBYTE* lpAml);
VOID HandleNAnd(LPBYTE* lpAml);
VOID HandleNOr(LPBYTE* lpAml);
VOID HandleNot(LPBYTE* lpAml);
VOID HandleOr(LPBYTE* lpAml);
VOID HandleObjectType(LPBYTE* lpAml);
VOID HandleShiftLeft(LPBYTE* lpAml);
VOID HandleShiftRight(LPBYTE* lpAml);
VOID HandleSizeOf(LPBYTE* lpAml);
VOID HandleStore(LPBYTE* lpAml);
VOID HandleSubstract(LPBYTE* lpAml);
VOID HandleToBuffer(LPBYTE* lpAml);
VOID HandleToDecimalString(LPBYTE* lpAml);
VOID HandleToHexString(LPBYTE* lpAml);
VOID HandleToInteger(LPBYTE* lpAml);
VOID HandleToString(LPBYTE* lpAml);
VOID HandleXor(LPBYTE* lpAml);
VOID HandleInteger(LPBYTE* lpAml, UINT64* pInteger);
VOID HandleNameSpaceModifierObj(LPBYTE* lpAml);
VOID HandleNamedObject(LPBYTE* lpAml);
VOID HandleBankValue(LPBYTE* lpAml);
VOID HandleField(LPBYTE* lpAml);
VOID HandleFieldList(LPBYTE lpAml, LPBYTE lpAmlEnd);
VOID HandleObject(LPBYTE* lpAml);
VOID HandleName(LPBYTE* lpAml);
VOID HandleNameSpace(LPBYTE* lpAml);
VOID HandleMethod(LPBYTE* lpAml);
VOID HandleFieldFlags(LPBYTE* lpAml);
VOID HandleAlias(LPBYTE* lpAml);
VOID HandleScope(LPBYTE* lpAml);
VOID HandleTableHead(LPBYTE* lpAml);
VOID HandleString(LPBYTE* lpAml);
VOID HandleBuffer(LPBYTE* lpAml);
BOOL VerifyValidPssReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL VerifyValidPtcReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL VerifyValidTssReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL OutputDataParser(UINT32 NameSeg, PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL VerifyValidXsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData, UINT Type);
BOOL VerifyValidPsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL VerifyValidTsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL VerifyValidCsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL VerifyValidUsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
BOOL VerifyValidCstReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData);
VOID ParseResourceTemplate(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader);
VOID ParseResourceTemplateData(PACPI_EVAL_OUTPUT_BUFFER);
#endif
