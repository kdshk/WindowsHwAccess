/*++

2008-2020  NickelS

Module Name:

    AmlParser.c

Abstract:

    Parse AML Code in to Asl Code

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
#include "Acpi.h"

#define NEXT_AML {(*lpAml) ++;}
#define AML_PTR (*lpAml)
#define AML_BYTE (*lpAml)[0]
#define INC_AML(a) {(*lpAml) += (a);} 
#define HandleObjectList	HandleObject	
#define HandleTermArgList	HandleTermArg
#define HandleTermList		HandleTermObj

extern AML_PARSER gAmlTable[];
extern int gDataParserLength;
extern ACPI_OUTPUT_DATA_PARSER	gDataParser[];
extern PACPI_NAMESPACE pLocalAcpiNS;
extern UINT uLocalAcpiNSCount;
extern AML_PARSER gAmlExtTable[];
#define gOffset m_nTab
#define SetupOffset SetupTab
static char OpRegionType[][20] = {
    { "SystemMemory" },
    { "SystemIO" },
    { "PCI_Config" },
    { "EmbededControl" },
    { "SMBus" },
    { "SystemCMOS" },
    { "PCIBarTarget" },
    { "IPMI" },
    { "GeneralPurposeIO" },
    { "GenericSerialBus" },
    { "PCC" },
    { "FFixedHW" },
    { "Reserved" }
};

static char strFieldType[][10] = {
    { "AnyAcc" },
    { "ByteAcc" },
    { "WordAcc" },
    { "DWordAcc" },
    { "QWordAcc" },
    { "BufferAcc" },
    { "Reserved" } };

const ULONG OpRegionTypeLen = sizeof(OpRegionType) / (sizeof(char) * 20);
const ULONG strFieldTypeLen = sizeof(strFieldType) / (sizeof(char) * 10);

int m_nTab = 0;
ACPI_NAMESPACE* gAcpiNSActive;
ACPI_NAMESPACE* m_pParserNS;


#define PRINT_MEM_BLOCK_SIZE    64*1024
PUCHAR  pPrintMem = NULL;
size_t  uPrintMemLen = 0;
size_t  uPrintMemUsed = 0;
UINT64
CopyAslCode(
    TCHAR  *pAsl
)
{
    size_t cbLength;
    if (FAILED(StringCbLengthA(pPrintMem, uPrintMemLen, &cbLength))) {
        return 0;
    }
    if (pAsl != NULL) {
        //StringCbPrintfA(pAsl, cbLength, "%s", pPrintMem);
        MultiByteToWideChar(CP_UTF8, MB_COMPOSITE, pPrintMem, (int)cbLength + 1, pAsl, (int)cbLength * 2 + 2);
    }
    return cbLength;
        //return (UINT64) StringCbPrintf
}

void ResetAmlPrintMem()
{
    if (pPrintMem != NULL) {
        free(pPrintMem);
        pPrintMem = NULL;
        uPrintMemLen = 0;
    }
}

void
AmlAppend(
    _In_z_ _Printf_format_string_ char const* const _Format,
    ...)
{    
    size_t remained;
    size_t used;
    va_list _ArgList;
    __crt_va_start(_ArgList, _Format);
    if (pPrintMem == NULL) {
        uPrintMemLen = PRINT_MEM_BLOCK_SIZE;
        pPrintMem = malloc(uPrintMemLen);
        if (pPrintMem == NULL) {
            assert(0);
            return;
        }
		uPrintMemUsed = 0;
        memset(pPrintMem, 0, uPrintMemLen);
    }

    used = uPrintMemUsed;
    remained = uPrintMemLen - used;

    if (remained < 200)
    {
        PUCHAR pBuf = malloc(uPrintMemLen + PRINT_MEM_BLOCK_SIZE);
        if (pBuf == NULL) {
            assert(0);
            return;
        }
        memset(pBuf, 0, uPrintMemLen + PRINT_MEM_BLOCK_SIZE);
        memcpy(pBuf, pPrintMem, uPrintMemLen);
        free(pPrintMem);
        uPrintMemLen += PRINT_MEM_BLOCK_SIZE;
        pPrintMem = pBuf;
		//uPrintMemUsed
        remained = uPrintMemLen - used;
		used = _vsnprintf_s(&pPrintMem[used], remained, remained, _Format, _ArgList);
		if (used == 0 && _Format[0] != 0)
		{
            assert(0);
        }
		uPrintMemUsed += used;
    }
    else {
		used = _vsnprintf_s(&pPrintMem[used], remained, remained, _Format, _ArgList);
		if (used == 0 && _Format[0] != 0)
		{
			assert(0);
		}
		uPrintMemUsed += used;
    }    
    __crt_va_end(_ArgList);

}



void ReleaseAmlBuf()
{
    if (pPrintMem != NULL) {
        free(pPrintMem);
    }
}

LPSTR
GetResouceType(UINT8 Idx)
{
    if (Idx < 10) {
        return OpRegionType[Idx];
    }
    if (Idx == 0x7F)
        return OpRegionType[11];
    else
        return OpRegionType[12];
}

VOID
PrintAscIIInComments(char* Buf, int cnt)
{
    int idx;
    AmlAppend("/*");
    for (idx = 0; idx < cnt; idx++) {
        if (Buf[idx] >= 0x20 && Buf[idx] <= 0x7E) {
            AmlAppend("%c", Buf[idx]);
        }
        else {
            AmlAppend(".");
        }
    }
    AmlAppend("*/");
}

void SetupTab(void)
{
    int            Index;
    for (Index = 0; Index < m_nTab; Index++)
    {
        AmlAppend("    ");
    }
}

UINT GetPkgLength(LPBYTE* lpAml)
{
    UINT            PkgLength;
    PKG_LEAD_BYTE   PkgLead;
    UINT8   PkgByte = (*lpAml)[0];

    memcpy(&PkgLead, &PkgByte, sizeof(UINT8));

    PkgLength = PkgLead.DataCnt + 1;

    memset(&PkgLead, 0, sizeof(PkgLead));

    memcpy(&PkgLead, *lpAml, PkgLength);

    (*lpAml) += PkgLength;

    memcpy(&PkgLength, &PkgLead, 4);

    if (PkgLength < 0x100) {
        PkgLength = PkgLength & 0x3F;
    }
    else {
        PkgLength = (PkgLength & 0xFFFFFF00) >> 4;
        PkgLength += PkgLead.PkgLen;
    }

    return PkgLength;
}

BOOL IsLeadChar(LPBYTE lpAml)
{
    if ((lpAml[0] >= 'A') && (lpAml[0] <= 'Z')) {
        return TRUE;
    }
    if (lpAml[0] == '_') {
        return TRUE;
    }
    if (lpAml[0] == '\\') {
        return TRUE;
    }
    return FALSE;
}

BOOL IsDebugObject(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == EXT_OP && Name[1] == DEBUG_OP) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsType6Opcodes(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == REFOF_OP) {
        return TRUE;
    }
    if (Name[0] == INDEX_OP) {
        return TRUE;
    }
    if (Name[0] == DEREFOF_OP) {
        return TRUE;
    }
    //
    // Search the method namespace to find the NameString, and the parameter number
    //
    return FALSE;
}

BOOL IsDigitChar(LPBYTE lpAml)
{
    if ((lpAml[0] >= '0') && (lpAml[0] <= '9')) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsNameChar(LPBYTE lpAml)
{
    if (IsLeadChar(lpAml) == TRUE) {
        return TRUE;
    }
    if (IsDigitChar(lpAml) == TRUE) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsRootChar(LPBYTE lpAml)
{
    if (lpAml[0] == '\\') {
        return TRUE;
    }
    return FALSE;
}


BOOL IsParentPrefixChar(LPBYTE lpAml)
{
    if (lpAml[0] == '^') {
        return TRUE;
    }
    return FALSE;
}

BOOL IsUserDefinedMethod(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (IsNameChar(lpAml) || IsRootChar(lpAml) || IsParentPrefixChar(lpAml)) {
        return TRUE;
    }
    if (Name[0] == DUAL_NAME_PREFIX || Name[0] == MULTI_NAME_PREFIX) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsLocalObject(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] >= LOCAL0_OP && Name[0] <= LOCAL7_OP) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsIntegerObject(LPBYTE lpAml)
{
    LPBYTE          Name = lpAml;
    switch (Name[0]) {
    case BYTE_PREFIX:
        return TRUE;
    case WORD_PREFIX:
        return TRUE;
    case DWORD_PREFIX:
        return TRUE;
    case QWORD_PREFIX:
        return TRUE;
    case ZERO_OP:
        return TRUE;
    case ONE_OP:
        return TRUE;
    case ONES_OP:
        return TRUE;
    default:
        return FALSE;
    }
}

BOOL IsNull(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == 0) {
        return TRUE;
    }
    return FALSE;
}
BOOL IsArgObject(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] >= ARG0_OP && Name[0] <= ARG6_OP) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsNameObject(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (IsNameChar(lpAml) || IsRootChar(lpAml) || IsParentPrefixChar(lpAml)) {
        return TRUE;
    }
    if (Name[0] == DUAL_NAME_PREFIX || Name[0] == MULTI_NAME_PREFIX) {
        return TRUE;
    }
    if (IsLocalObject(lpAml)) {
        return TRUE;
    }
    if (IsArgObject(lpAml)) {
        return TRUE;
    }
    if (IsDebugObject(lpAml)) {
        return TRUE;
    }
    //
    // Type 6 OpCode
    //
    if (IsType6Opcodes(lpAml)) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsTarget(LPBYTE lpAml)
{
    if (IsNameObject(lpAml)) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsDataObject(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == BYTE_PREFIX || Name[0] == WORD_PREFIX || Name[0] == DWORD_PREFIX ||
        Name[0] == STRING_PREFIX || Name[0] == QWORD_PREFIX || Name[0] == ZERO_OP ||
        Name[0] == ONE_OP || Name[0] == ONES_OP || Name[0] == ZERO_OP) {
        return TRUE;
    }
    if ((Name[0] == EXT_OP) && (Name[1] == REVISION_OP)) {
        return TRUE;
    }
    if ((Name[0] == BUFFER_OP) || (Name[0] == PACKAGE_OP) || (Name[0] == VAR_PACKAGE_OP)) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsNameSpaceModifierObject(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == ALIAS_OP || Name[0] == NAME_OP || Name[0] == SCOPE_OP) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsNamedObject(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == EXT_OP && Name[1] == BANK_FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == CREATE_BIT_FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == CREATE_BIT_FIELD) {
        return TRUE;
    }
    if (Name[0] == CREATE_BYTE_FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == CREATE_DWORD_FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == CREATE_QWORD_FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == CREATE_WORD_FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == METHOD_OP) {
        return TRUE;
    }
    if (Name[0] == EXTERNAL_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == DATA_REGION_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == DEVICE_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == EVENT_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == INDEX_FIELD_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == MUTEX_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == OPREGION_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == POWER_RES_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == PROCESSOR_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == THERMAL_ZONE_OP) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsType1Opcodes(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == BREAK_OP) {
        return TRUE;
    }
    if (Name[0] == BREAK_POINT_OP) {
        return TRUE;
    }
    if (Name[0] == CONTINUE_OP) {
        return TRUE;
    }
    if (Name[0] == ELSE_OP) {
        return TRUE;
    }
    if (Name[0] == IF_OP) {
        return TRUE;
    }
    if (Name[0] == NOOP_OP) {
        return TRUE;
    }
    if (Name[0] == NOTIFY_OP) {
        return TRUE;
    }
    if (Name[0] == RETURN_OP) {
        return TRUE;
    }
    if (Name[0] == WHILE_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == STALL_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == UNLOAD_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == SLEEP_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == SIGNAL_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == RESET_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == RELEASE_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == FATAL_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == LOAD_OP) {
        return TRUE;
    }
    return FALSE;
}
BOOL IsType2Opcodes(LPBYTE lpAml)
{
    LPBYTE  Name = lpAml;
    if (Name[0] == ADD_OP) {
        return TRUE;
    }
    if (Name[0] == AND_OP) {
        return TRUE;
    }
    if (Name[0] == BUFFER_OP) {
        return TRUE;
    }
    if (Name[0] == CONCAT_OP) {
        return TRUE;
    }
    if (Name[0] == CONCAT_RES_OP) {
        return TRUE;
    }
    if (Name[0] == COPY_OBEJECT_OP) {
        return TRUE;
    }
    if (Name[0] == DECREMENT_OP) {
        return TRUE;
    }
    if (Name[0] == DEREFOF_OP) {
        return TRUE;
    }
    if (Name[0] == DIVIDE_OP) {
        return TRUE;
    }
    if (Name[0] == FIND_SET_LEFT_BIT_OP) {
        return TRUE;
    }
    if (Name[0] == FIND_SET_RIGHT_BIT_OP) {
        return TRUE;
    }
    if (Name[0] == INCREMENT_OP) {
        return TRUE;
    }
    if (Name[0] == INDEX_OP) {
        return TRUE;
    }
    if (Name[0] == LAND_OP) {
        return TRUE;
    }
    if (Name[0] == LEQUAL_OP) {
        return TRUE;
    }
    if (Name[0] == LGREATER_OP) {
        return TRUE;
    }
    if (Name[0] == LLESS_OP) {
        return TRUE;
    }
    if (Name[0] == LNOT_OP) {
        return TRUE;
    }
    if (Name[0] == LOR_OP) {
        return TRUE;
    }
    if (Name[0] == MATCH_OP) {
        return TRUE;
    }
    if (Name[0] == MID_OP) {
        return TRUE;
    }
    if (Name[0] == MOD_OP) {
        return TRUE;
    }
    if (Name[0] == MULTIPLY_OP) {
        return TRUE;
    }
    if (Name[0] == NAND_OP) {
        return TRUE;
    }
    if (Name[0] == NOR_OP) {
        return TRUE;
    }
    if (Name[0] == NOT_OP) {
        return TRUE;
    }
    if (Name[0] == OBJECT_TYPE_OP) {
        return TRUE;
    }
    if (Name[0] == OR_OP) {
        return TRUE;
    }
    if (Name[0] == PACKAGE_OP) {
        return TRUE;
    }
    if (Name[0] == VAR_PACKAGE_OP) {
        return TRUE;
    }
    if (Name[0] == REFOF_OP) {
        return TRUE;
    }
    if (Name[0] == SHIFT_LEFT_OP) {
        return TRUE;
    }
    if (Name[0] == SHIFT_RIGHT_OP) {
        return TRUE;
    }
    if (Name[0] == SIZE_OF_OP) {
        return TRUE;
    }
    if (Name[0] == STORE_OP) {
        return TRUE;
    }
    if (Name[0] == SUBSTRACT_OP) {
        return TRUE;
    }
    if (Name[0] == TO_BUFFER_OP) {
        return TRUE;
    }
    if (Name[0] == TO_DECIMAL_STRING_OP) {
        return TRUE;
    }
    if (Name[0] == TO_HEX_STRING_OP) {
        return TRUE;
    }
    if (Name[0] == TO_INTEGER_OP) {
        return TRUE;
    }
    if (Name[0] == TO_STRING_OP) {
        return TRUE;
    }
    if (Name[0] == XOR_OP) {
        return TRUE;
    }

    //
    // >=
    //
    if (Name[0] == LNOT_OP && Name[1] == LLESS_OP) {
        return TRUE;
    }
    //
    // !=
    //
    if (Name[0] == LNOT_OP && Name[1] == LEQUAL_OP) {
        return TRUE;
    }
    //
    // <=
    //
    if (Name[0] == LNOT_OP && Name[1] == LGREATER_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == WAIT_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == TO_BCD_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == TIMER_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == LOAD_TABLE_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == FROM_BCD_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == COND_REFOF_OP) {
        return TRUE;
    }
    if (Name[0] == EXT_OP && Name[1] == ACQUIRE_OP) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsTermObject(LPBYTE lpAml)
{
    if (IsNameSpaceModifierObject(lpAml)) {
        return TRUE;
    }
    if (IsNamedObject(lpAml)) {
        return TRUE;
    }
    if (IsType1Opcodes(lpAml)) {
        return TRUE;
    }
    if (IsType2Opcodes(lpAml)) {
        return TRUE;
    }
    return FALSE;
}

BOOL IsObject(LPBYTE lpAml)
{
    if (IsNameSpaceModifierObject(lpAml)) {
        return TRUE;
    }
    if (IsNamedObject(lpAml)) {
        return TRUE;
    }
    return FALSE;
}

VOID HandleNameSeg(LPBYTE* lpAml)
{

    LPBYTE          NameSeg = *lpAml;

    if (!IsNameChar(NameSeg) || !IsNameChar(NameSeg + 1) || !IsNameChar(NameSeg + 2) || !IsNameChar(NameSeg + 3))
    {
        return;
    }
    AmlAppend("%c%c%c%c", NameSeg[0], NameSeg[1], NameSeg[2], NameSeg[3]);
    (*lpAml) += 4;
}

VOID HandleNameString(LPBYTE* lpAml)
{
    LPBYTE          NameSegString = *lpAml;
    switch (NameSegString[0])
    {
    case ROOT_CHAR:
        AmlAppend("%c", NameSegString[0]);
        NEXT_AML;
        HandleNamePath(lpAml);
        break;
    case PARENT_PREFIX_CHAR:
        AmlAppend("%c", NameSegString[0]);
        NEXT_AML;
        HandleNamePath(lpAml);
        break;
    default:
        HandleNamePath(lpAml);
        break;
    }
}

VOID GetNameSeg(UINT32* uName, LPBYTE lpAml)
{
    UINT32* uNameFromAml = (UINT32*)lpAml;
    (*uName) = (*uNameFromAml);
}


VOID HandleGetNamePath(UINT32* chName, LPBYTE lpAml)
{
    LPBYTE          NameSegPath = lpAml;
    UINT8           SegCount;
    UINT            Index = 0;
    switch (NameSegPath[0])
    {
    case DUAL_NAME_PREFIX:
        GetNameSeg(chName, &lpAml[1]);
        GetNameSeg(&chName[1], &lpAml[5]);
        break;
    case MULTI_NAME_PREFIX:
        SegCount = NameSegPath[1];
        while (SegCount) {
            GetNameSeg(&chName[Index], &lpAml[2 + 4 * Index]);
            SegCount--;
            Index++;
        }
        break;
    case PARENT_PREFIX_CHAR:
        GetNamePath(chName, &lpAml[1]);
        break;
    case 0:
        //
        // NULL NAME
        //        
        break;
    default:
        GetNameSeg(chName, lpAml);
        break;
    }
    return;
}

VOID HandleNamePath(LPBYTE* lpAml)
{

    LPBYTE          NameSegPath = *lpAml;
    UINT8           SegCount;

    switch (NameSegPath[0])
    {
    case DUAL_NAME_PREFIX:
        NEXT_AML;
        HandleNameSeg(lpAml);
        AmlAppend(".");
        HandleNameSeg(lpAml);
        break;
    case MULTI_NAME_PREFIX:
        SegCount = NameSegPath[1];
        (*lpAml) += 2;
        while (SegCount) {
            HandleNameSeg(lpAml);
            SegCount--;
            if (SegCount != 0) {
                AmlAppend(".");
            }
            else {
                break;
            }
        }
        break;
    case PARENT_PREFIX_CHAR:
        AmlAppend("%c", NameSegPath[0]);
        NEXT_AML;
        HandleNamePath(lpAml);
        break;
    case 0:
        //
        // NULL NAME
        //
        NEXT_AML;
        break;
    default:
        HandleNameSeg(lpAml);
        break;
    }
    return;
}

VOID ChGetNameSeg(char* chName, LPBYTE lpAml)
{
    memcpy(chName, lpAml, 4);
}

VOID ChGetNamePath(char* chName, LPBYTE lpAml)
{
    LPBYTE          NameSegPath = lpAml;
    UINT8           SegCount;
    UINT            Index = 0;
    switch (NameSegPath[0])
    {
    case DUAL_NAME_PREFIX:
        ChGetNameSeg(chName, &lpAml[1]);
        chName[4] = '.';
        ChGetNameSeg(&chName[5], &lpAml[5]);
        break;
    case MULTI_NAME_PREFIX:
        SegCount = NameSegPath[1];
        NameSegPath += 2;
        while (SegCount) {
            ChGetNameSeg(&chName[Index], NameSegPath);
            SegCount--;
            Index += 4;
            NameSegPath += 4;
            if (SegCount != 0) {
                chName[Index] = '.';
                Index++;
            }
        }
        break;
    case PARENT_PREFIX_CHAR:
        chName[0] = '^';
        ChGetNamePath(&chName[1], &lpAml[1]);
        break;
    case 0:
        break;
    default:
        ChGetNameSeg(chName, lpAml);
        break;
    }
    return;
}

VOID
ChGetNameString(char* chName, LPBYTE lpAml)
{
    LPBYTE          NameSegString = lpAml;
    memset(chName, 0, MAX_NAME_SPACE_PATH);

    switch (NameSegString[0])
    {
    case ROOT_CHAR:
        chName[0] = NameSegString[0];
        ChGetNamePath(&chName[1], &lpAml[1]);
        break;
    case PARENT_PREFIX_CHAR:
        chName[0] = NameSegString[0];
        ChGetNamePath(&chName[1], &lpAml[1]);
        break;
    default:
        ChGetNamePath(chName, lpAml);
        break;
    }
}

UINT32
GetNameString(UINT32* chName, LPBYTE lpAml)
{
    LPBYTE          NameSegString = lpAml;
    UINT32* LocalName, Index, Index1;

    for (Index = 0; Index < MAX_NAME_SPACE_PATH / 4; Index++) {
        if (chName[Index] == 0) {
            break;
        }
    }
    if (Index == MAX_NAME_SPACE_PATH / 4) {
        assert(0);
        return 0;
    }
    LocalName = &chName[Index];

    switch (NameSegString[0])
    {
    case ROOT_CHAR:
        GetNamePath(LocalName, &lpAml[1]);
        break;
    case PARENT_PREFIX_CHAR:
        GetNamePath(LocalName, &lpAml[1]);
        break;
    default:
        GetNamePath(LocalName, lpAml);
        break;
    }
    for (Index1 = 0; Index1 < MAX_NAME_SPACE_PATH / 4; Index1++) {
        if (chName[Index1] == 0) {
            break;
        }
    }
    if (Index1 == MAX_NAME_SPACE_PATH / 4) {
        assert(0);
        //MessageBox (NULL, "NAME SPACE Path is full!", "Error", MB_ICONERROR);        
        return 0;
    }
  
    return (Index1 - Index) * 4;
}

VOID HandleSimpleName(LPBYTE* lpAml)
{
    LPBYTE          SimpleName = *lpAml;
    if ((SimpleName[0] >= ARG0_OP) && (SimpleName[0] <= ARG6_OP)) {
        AmlAppend("Arg%d", SimpleName[0] - ARG0_OP);
        (*lpAml)++;
    }
    else if ((SimpleName[0] >= LOCAL0_OP) && (SimpleName[0] <= LOCAL7_OP)) {
        AmlAppend("Local%d", SimpleName[0] - LOCAL0_OP);
        (*lpAml)++;
    }
    else {
        HandleNameString(lpAml);
    }
}

VOID HandleSuperName(LPBYTE* lpAml)
{
    LPBYTE          SuperName = *lpAml;
    switch (SuperName[0])
    {
    case EXT_OP:
        //
        // Debug Object and Type6Opcode
        //
        //NEXT_AML;
        HandleExtendOP(lpAml);
        break;
    case REFOF_OP:
    case DEREFOF_OP:
    case INDEX_OP:
        HandleType6Opcodes(lpAml);
        break;
    default:
        if (TRUE) {
            HandleSimpleName(lpAml);
        }
        break;
    }
}
VOID HandleEvent(LPBYTE* lpAml)
{
    //SetupTab ();    
    NEXT_AML;
    AmlAppend("Event (");
    HandleNameString(lpAml);
    AmlAppend(")");
    //NEXT_AML;
}

VOID HandleDebug(LPBYTE* lpAml)
{
    //SetupTab ();          
    AmlAppend("Debug");
    NEXT_AML;
}

VOID HandleMutex(LPBYTE* lpAml)
{
    //SetupTab ();
    AmlAppend("Mutex(");
    NEXT_AML;
    HandleNameString(lpAml);
    AmlAppend(",0x%02X)", (*lpAml)[0]);
    NEXT_AML;
}

VOID HandleRevision(LPBYTE* lpAml)
{
    AmlAppend("REV");
    NEXT_AML;
}

VOID HandleCondRefOf(LPBYTE* lpAml)
{

    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("CondRefOf (");

    HandleSuperName(lpAml);
    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    //AmlAppend( ", ");
    //HandleTarget (lpAml);
    AmlAppend(")");
}

VOID HandleTarget(LPBYTE* lpAml)
{
    LPBYTE          SuperName = *lpAml;

    if (SuperName[0] != 0)
    {
        HandleSuperName(lpAml);
    }
    else {
        (*lpAml)++;
    }
}

VOID HandleDataObject(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;

    switch (Name[0]) {
    case PACKAGE_OP:
        HandlePackage(lpAml);
        break;
    case VAR_PACKAGE_OP:
        HandleVarPackage(lpAml);
        break;
    default:
        HandleComputationalData(lpAml);
        break;
    }
}

VOID HandleDataRefObject(LPBYTE* lpAml)
{
    PBYTE           Name = *lpAml;

    if ((Name[0] >= LOCAL0_OP) && (Name[0] <= LOCAL7_OP)) {
        AmlAppend("Local%d", Name[0] - LOCAL0_OP);
        (*lpAml)++;
    }
    else if ((Name[0] >= ARG0_OP) && (Name[0] <= ARG6_OP)) {
        AmlAppend("Arg%d", Name[0] - ARG0_OP);
        (*lpAml)++;
    }
    else {
        HandleDataObject(lpAml);
    }
}

VOID HandleArgObj(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    AmlAppend("Arg%d", Name[0] - ARG0_OP);
    NEXT_AML;

}
VOID HandleLocalObj(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    AmlAppend("Local%d", Name[0] - LOCAL0_OP);
    NEXT_AML;
}

VOID HandleTermArg(LPBYTE* lpAml)
{
	if (IsTarget(*lpAml)) {
		if (IsUserDefinedMethod(*lpAml)) {
			if (!HandleUserTermObj(lpAml)) {
				HandleTarget(lpAml);
			}
		}
		else {
			HandleTarget(lpAml);
		}
	}
	else {
		gAmlTable[*(*lpAml)].AmlHandler(lpAml);
	}

}

VOID HandleLoadTable(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("LoadTable (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleRefOf(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("RefOf (");
    HandleSuperName(lpAml);
    AmlAppend(")");
}

VOID HandleDerefOf(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("DerefOf (");
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleIndex(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("Index (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);

    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    AmlAppend(")");

    //AmlAppend( ",");
    //HandleTarget (lpAml);
    //AmlAppend( ")");
}


VOID HandleType6Opcodes(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;

    //SetupTab ();  
    switch (Name[0]) {
    case REFOF_OP:
        HandleRefOf(lpAml);
        break;
    case DEREFOF_OP:
        HandleDerefOf(lpAml);
        break;
    case INDEX_OP:
        HandleIndex(lpAml);
        break;
    default:
        assert(0);
        break;
    }
}

VOID HandleLoad(LPBYTE* lpAml)
{
    NEXT_AML;
    //SetupTab ();  
    AmlAppend("Load (");
    HandleNameString(lpAml);
    AmlAppend(", ");
    HandleSuperName(lpAml);
    AmlAppend(")");
}

VOID HandleStall(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Stall (");
    HandleTermArg(lpAml);
    AmlAppend(")\n");
}

VOID HandleUnload(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Unload (");
    HandleSuperName(lpAml); // Nks
    AmlAppend(")");
}

VOID HandleWhile(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    UINT            PkgLength;

    if (Name[0] == WHILE_OP) {
        NEXT_AML;
        //SetupTab ();  
        AmlAppend("While (");
        PkgLength = GetPkgLength(lpAml);
        HandleTermArg(lpAml);
        AmlAppend(")\n");
        SetupTab();
        AmlAppend("{\n");
        m_nTab++;

        //
        // Disamble internal name space
        //  
        Name += PkgLength;
        while (Name >= (*lpAml)) {
            SetupTab();
            HandleTermList(lpAml);
            AmlAppend("\n");
        }
        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }
}

VOID HandleAcquire(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Acquire (");
    HandleSuperName(lpAml);
    AmlAppend(",");
    HandleWordData(lpAml);
    AmlAppend(")");
}

VOID HandleAdd(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("Add (");

    //HandleDataRefObject (lpAml);
    HandleTermArg(lpAml);
    AmlAppend(", ");
    //HandleDataRefObject (lpAml);
    HandleTermArg(lpAml);

    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    AmlAppend(")");
}

VOID HandleAnd(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("And (");

    //HandleDataRefObject (lpAml);
    HandleTermArg(lpAml);
    AmlAppend(", ");
    //HandleDataRefObject (lpAml);
    HandleTermArg(lpAml);

    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    //AmlAppend(  ", ");
    //HandleTarget (lpAml);
    AmlAppend(")");

}

VOID HandleConcat(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;

    AmlAppend("Concatenate (");

    //HandleComputationalData(lpAml);
    HandleTermArg(lpAml);
    AmlAppend(", ");
    //HandleComputationalData (lpAml);
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleConcatRes(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("ConcatenateResTemplate (");

    //HandleComputationalData(lpAml);
    HandleTermArg(lpAml);
    AmlAppend(", ");
    //HandleComputationalData(lpAml);
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleExtendOP(LPBYTE* lpAml)
{
    BYTE    op = *lpAml[0];
	NEXT_AML;
	op = *lpAml[0];
	gAmlExtTable[op].AmlHandler(lpAml);   
}

VOID HandleBuffer(LPBYTE* lpAml)
{
    UINT            PkgLength;
    LPBYTE          Name = *lpAml;
    UINT64          Integer, Index;
    UINT            Length;
    
	//
    // Define a buffer
    //
    (*lpAml)++;
    PkgLength = GetPkgLength(lpAml);
    //
    // Get Buffer Size;
    //          
    Name += PkgLength;

    AmlAppend("Buffer (");

    if (IsIntegerObject(*lpAml)) {
        HandleInteger(lpAml, &Integer);
    }
    else {
        HandleTermObj(lpAml);
        AmlAppend(") {");
        AmlAppend("}");
        return;
    }

    //ParseSmallReourceData (lpAml, (UINT) Integer);

    if ((*lpAml) > Name) {
        AmlAppend(") {");
        AmlAppend("}");
        return;
    }

    AmlAppend(")\n");
    //
    // Byte List
    //
    SetupTab();
    AmlAppend("{\n");

    m_nTab++;
    SetupTab();
    //
    // Print out the offset
    //

    Length = (UINT)((UINTN)Name - (UINTN)(*lpAml) + 1);
    for (Index = 0; (*lpAml) <= Name; Index++) {
        if (Index != 0) {
            AmlAppend(", ");
        }
        if (Index % 8 == 0 && Length > 8) {
            if (Index != 0) {
                AmlAppend("\n");
                SetupTab();
            }
            AmlAppend("/* %04X */    ", Index);
        }
        AmlAppend("0x%02X", (*lpAml)[0]);
        (*lpAml)++;
    }
    AmlAppend("\n");
    SetupTab();
    m_nTab--;
    AmlAppend("}");
}


VOID HandleShiftLeft(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();
    AmlAppend("ShiftLeft (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    //AmlAppend(  ", ");    
    //HandleTarget (lpAml);       
    AmlAppend(")");
}

VOID HandlePackage(LPBYTE* lpAml)
{
    UINT            PkgLength;
    UINT8           NumberOfElement;
    UINT8           Index;
    LPBYTE          Name = *lpAml;

    if (Name[0] != PACKAGE_OP) {
        //__asm int 3;
        //MessageBox (NULL, "HandlePackage", "Error", MB_ICONERROR);
        assert(0);
    }
    (*lpAml)++;

    //Name = *lpAml;
    PkgLength = GetPkgLength(lpAml);
    NumberOfElement = (*lpAml)[0];

    //
    // 
    //
    Name += PkgLength;
    if (Name == (*lpAml)) {
        (*lpAml)++;
        AmlAppend("Package (0x%X) {}", NumberOfElement);
        return;
    }
    AmlAppend("Package (0x%X)\n", NumberOfElement);
    SetupTab();


    AmlAppend("{\n");
    (*lpAml)++;
    m_nTab++;
    SetupTab();

    for (Index = 0; (*lpAml) <= Name; Index++) {
        if (Index != 0) {
            AmlAppend(", ");
            AmlAppend("\n");
            SetupTab();
        }
        if (IsNameObject(*lpAml)) {
            HandleNameString(lpAml);
        }
        else {
            HandleDataRefObject(lpAml);
        }

    }
    m_nTab--;
    AmlAppend("\n");
    SetupTab();
    AmlAppend("}");
}

VOID HandleVarPackage(LPBYTE* lpAml)
{
    UINT            PkgLength;
    UINT64          NumberOfElement;
    UINT64          Index;
    LPBYTE          Name = *lpAml;

    if (Name[0] != PACKAGE_OP) {
        assert(0);
        //__asm int 3;
        //MessageBox (NULL, "HandleVarPackage", "Error", MB_ICONERROR);
    }
    (*lpAml)++;

    PkgLength = GetPkgLength(lpAml);
    HandleInteger(lpAml, &NumberOfElement);

    AmlAppend("{");
    Name += PkgLength;
    for (Index = 0; (*lpAml) < Name; Index++) {
        HandleDataRefObject(lpAml);
        AmlAppend(", ");
    }
    //HandleDataRefObject (lpAml);
    AmlAppend("}");
}

VOID HandleComputationalData(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    ASL_UINT64      Data;

    memcpy(&Data, Name + 1, sizeof(ASL_UINT64));
    switch (Name[0]) {
    case ZERO_OP:
    case ONE_OP:
    case ONES_OP:
    case BYTE_PREFIX:
    case WORD_PREFIX:
    case DWORD_PREFIX:
    case QWORD_PREFIX:
        HandleInteger(lpAml, NULL);
        break;
    case STRING_PREFIX:
        HandleString(lpAml);
        break;
    case EXT_OP:
        switch (Name[1]) {
        case REVISION_OP:
            //AmlAppend( "REV");
            //(*lpAml) ++;
            HandleExtendOP(lpAml);
            break;
        default:
            assert(0);
            break;
        }
        break;
    case BUFFER_OP:
        HandleBuffer(lpAml);
        break;
    default:
        //
        // User Define
        //
        if (IsNameChar(*lpAml) || IsRootChar(*lpAml) || IsParentPrefixChar(*lpAml)) {
            //
            // Possible Invoke a function
            //
            if (!HandleUserTermObj(lpAml)) {
                assert(0);
            }
            break;
        }
        assert(0);
        break;
    }
}

VOID HandleTermObj(LPBYTE* lpAml)
{
	if (IsTarget(*lpAml)) {
		if (IsUserDefinedMethod(*lpAml)) {
			if (!HandleUserTermObj(lpAml)) {
				HandleTarget(lpAml);
			}
		}
		else {
			HandleTarget(lpAml);
		}
	}
	else {
		gAmlTable[*(*lpAml)].AmlHandler(lpAml);
	}

}


VOID HandleLOr(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("LOr (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");

}

VOID HandleByteData(LPBYTE* lpAml)
{
    AmlAppend("0x%02X", AML_BYTE);
    NEXT_AML;

}

VOID HandleWordData(LPBYTE* lpAml)
{

    LPWORD          Name = (LPWORD)(*lpAml);
    AmlAppend("0x%04X", Name[0]);
    INC_AML(2);
}

VOID HandleDWordData(LPBYTE* lpAml)
{
    LPDWORD         Name = (LPDWORD)(*lpAml);
    LPBYTE          bytedata = (LPBYTE)(*lpAml);
    UINT16          Sig = (UINT16)Name[0];
    if (Sig == 0xD041) {
        AmlAppend("EisaId(\"PNP%02X%02X\")",
            bytedata[2], bytedata[3]);
    }
    else {
        AmlAppend("0x%08X", Name[0]);
    }
    INC_AML(4);
}

VOID HandleQWordData(LPBYTE* lpAml)
{
    LPDWORD         Name = (LPDWORD)(*lpAml);
    AmlAppend("0x%08X%08X", Name[1], Name[0]);
    INC_AML(8);
}
VOID HandleMatch(LPBYTE* lpAml)
{
    static char strMatchOp[][5] = {
            "MTR",
            "MEQ",
            "MLE",
            "MLT",
            "MGE",
            "MGT"
    };
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("Match (");
    HandleTermArg(lpAml);
    //
    // HandleByteData
    //
    AmlAppend(",");
    //HandleByteData (lpAml);
    AmlAppend(strMatchOp[*(*lpAml)]);
    NEXT_AML;
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(",");
    //HandleByteData (lpAml);
    AmlAppend(strMatchOp[*(*lpAml)]);
    NEXT_AML;
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleMid(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("Mid (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleMod(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("Mod (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleMultiply(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("Multiply (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    AmlAppend(")");

}

VOID HandleExternal(LPBYTE* lpAml)
{
    BYTE opType;
    static CHAR chiExtType[][20] = {
    "UnknownObj","IntObj","StrObj","BuffObj","PkgObj","FieldUnitObj","DeviceObj","EventObj","MethodObj","MutexObj","OpRegionObj","PowerResObj","ProcessorObj","ThermalZoneObj","BuffFieldObj","DDBHandleObj" };

    AmlAppend("External (");
    NEXT_AML;
    HandleNameString(lpAml);
    opType = *(*lpAml);
    AmlAppend(", %s", chiExtType[*(*lpAml)]);
    NEXT_AML;
    if (opType == ACPI_TYPE_METHOD) {
        AmlAppend(")"); //AmlAppend(", %d)", *(*lpAml));
    }
    else {
        AmlAppend(")");
    }
    NEXT_AML;

}

VOID HandleCreateField(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;

    //SetupTab ();
    switch (Name[0])
    {
    case CREATE_BIT_FIELD_OP:
        AmlAppend("CreateBitField (");
        break;
    case CREATE_BYTE_FIELD_OP:
        AmlAppend("CreateByteField (");
        break;
    case CREATE_WORD_FIELD_OP:
        AmlAppend("CreateWordField (");
        break;
    case CREATE_DWORD_FIELD_OP:
        AmlAppend("CreateDWordField (");
        break;
    case CREATE_QWORD_FIELD_OP:
        AmlAppend("CreateQWordField (");
        break;
    case CREATE_BIT_FIELD:
        AmlAppend("CreateField (");
        break;
    }
    NEXT_AML;
    HandleTermArg(lpAml);
    AmlAppend(", ");
    // HandleInteger (lpAml, NULL);
    HandleTermArg(lpAml);
    AmlAppend(", ");
    if (Name[0] == CREATE_BIT_FIELD) {
        if (IsIntegerObject(*lpAml)) {
            HandleInteger(lpAml, NULL);
        }
        else {
            HandleTermArg(lpAml);
        }
        AmlAppend(", ");
    }
    HandleNameString(lpAml);
    AmlAppend(")");
}

VOID HandleSleep(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Sleep (");
    //HandleInteger (lpAml, NULL);
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleSignal(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Signal (");
    HandleSuperName(lpAml); // Nks
    AmlAppend(")");
}

VOID HandleWait(LPBYTE* lpAml)
{
    //SetupTab ();
    NEXT_AML;
    AmlAppend("Wait (");
    HandleSuperName(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(")");
}


VOID HandleRelease(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Release (");
    HandleSuperName(lpAml);
    AmlAppend(")");

}

VOID HandleReset(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Reset (");
    HandleSuperName(lpAml); // Nks
    AmlAppend(")");
}

VOID HandleFromBCD(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("FromBCD (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTarget(lpAml);
    AmlAppend(")");
}

VOID HandleToBCD(LPBYTE* lpAml)
{
    //SetupTab ();
    NEXT_AML;
    AmlAppend("ToBCD (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");
}

VOID HandleFatal(LPBYTE* lpAml)
{
    UINT8           FatalType;
    UINT32          FatalCode;

    NEXT_AML;
    //SetupTab ();
    AmlAppend("Fatal (");
    FatalType = AML_BYTE;
    AmlAppend("0x%X", FatalType);
    NEXT_AML;
    memcpy(&FatalCode, AML_PTR, sizeof(UINT32));
    INC_AML(4);
    AmlAppend(", 0x%08X, ", FatalCode);
    //HandleInteger (lpAml, NULL);
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleTimer(LPBYTE* lpAml)
{
    //SetupTab ();
    AmlAppend("Timer");
    NEXT_AML;
}

VOID HandleOpRegion(LPBYTE* lpAml)
{

    UINT8   RegionSpace;
    //
    //SetupTab ();
    AmlAppend("OperationRegion (");
    (*lpAml)++;
    HandleNameString(lpAml);
    RegionSpace = (*lpAml)[0];

    AmlAppend(", ");
    {
        static char dslOpRegionType[][20] = {
            { "SystemMemory" },
            { "SystemIO" },
            { "PCI_Config" },
            { "EmbeddedControl" },
            { "SMBus" },
            { "SystemCMOS" },
            { "PCIBarTarget" },
            { "IPMI" },
            { "GeneralPurposeIO" },
            { "GenericSerialBus" },
            { "PCC" }
        };
        if (RegionSpace > 10) {
            AmlAppend("Reserved");
        }
        else {
            AmlAppend(dslOpRegionType[RegionSpace]);
        }
    }
    /*switch (RegionSpace)
    {
    case 0:
        AmlAppend(  "SystemMemory");
        break;
    case 1:
        AmlAppend(  "SystemIO");
        break;
    case 2:
        AmlAppend(  "PCI_Config");
        break;
    case 3:
        AmlAppend(  "EmbededControl");
        break;
    case 4:
        AmlAppend(  "SMBus");
        break;
    case 5:
        AmlAppend(  "SystemCMOS");
        break;
    case 6:
        AmlAppend(  "PCIBarTarget");
        break;
    default:
        AmlAppend(  "User Defined");
        break;
    }*/
    AmlAppend(", ");

    (*lpAml)++;
    /*
    if (IsNameChar (*lpAml)) {
        HandleNameString (lpAml);
    } else {
        HandleInteger (lpAml, NULL);
    }
    */
    HandleTermArg(lpAml);

    AmlAppend(", ");

    //HandleInteger (lpAml, NULL);
    HandleTermArg(lpAml);

    AmlAppend(")");
}

VOID HandleDevice(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    LPBYTE          lpScopeEnd = Name;
    UINT            PkgLength;


    NEXT_AML;
    //SetupTab ();
    PkgLength = GetPkgLength(lpAml);
    AmlAppend("Device (");


    Name = *lpAml;
    HandleNameString(lpAml);

    /*GetNameString((UINT32*)gchName, Name);
    ChGetNameString(chName, Name);

    lStart = StartIndex;
    lEnd = EndIndex;
    bStacked = PushAcpiNameSpace(gchName, &gAcpiNameStack);*/

    AmlAppend(")\n");   \
        SetupTab();
    AmlAppend("{\n");
    m_nTab++;

    //
    // Handle the ObjectList
    //  
    lpScopeEnd += PkgLength;
    while (lpScopeEnd >= *lpAml) {
        SetupTab();
        //HandleObjectList (lpAml);
        HandleTermList(lpAml);
        AmlAppend("\n");
    }
    m_nTab--;
    SetupTab();
    AmlAppend("}");
   /* if (bStacked)
        PopAcpiNameSpace(&gAcpiNameStack);

    for (DeviceNameLength = lStart * 4; DeviceNameLength < lEnd * 4; DeviceNameLength++) {
        gchName[DeviceNameLength] = 0;
    }*/
    //__asm int 3;
}


VOID HandleProcessor(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    LPBYTE          lpScopeEnd = Name;
    UINT8           ProcId, PblkLen;
    UINT32          PblkAddr;
    UINT            PkgLength;

    //__asm int 3;
    //SetupTab ();
    AmlAppend("Processor (");
    NEXT_AML;


    PkgLength = GetPkgLength(lpAml);
    //HandleNameString (lpAml);

    Name = *lpAml;
    HandleNameString(lpAml);
    /*GetNameString((UINT32*)gchName, Name);
    lStart = StartIndex;
    lEnd = EndIndex;
    bStacked = PushAcpiNameSpace(gchName, &gAcpiNameStack);*/

    ProcId = (*lpAml)[0];
    AmlAppend(", 0x%X", ProcId);
    NEXT_AML;

    memcpy(&PblkAddr, AML_PTR, sizeof(UINT32));

    AmlAppend(", 0x%X", PblkAddr);

    INC_AML(4);

    PblkLen = AML_BYTE;
    AmlAppend(", 0x%X)\n", PblkLen);
    NEXT_AML;
    SetupTab();
    AmlAppend("{\n");


    m_nTab++;
    //
    // Handle the ObjectList
    //  
    lpScopeEnd += PkgLength;
    while (lpScopeEnd >= *lpAml) {
        SetupTab();
        HandleObjectList(lpAml);
        AmlAppend("\n");
    }
    m_nTab--;
    SetupTab();
    AmlAppend("}");
    /*if (bStacked)
        PopAcpiNameSpace(&gAcpiNameStack);

    for (DeviceNameLength = lStart * 4; DeviceNameLength < lEnd * 4; DeviceNameLength++) {
        gchName[DeviceNameLength] = 0;
    }*/

}

VOID HandlePowerRes(LPBYTE* lpAml)
{

    UINT            PkgLength;
    LPBYTE          Name = *lpAml;
    LPBYTE          lpScopeEnd = Name;
    UINT8           ByteData;
    UINT16          WordData;

    NEXT_AML;
    //SetupTab ();
    PkgLength = GetPkgLength(lpAml);
    Name = *lpAml;
    AmlAppend("PowerResource (");
    HandleNameString(lpAml);
    /*GetNameString((UINT32*)gchName, Name);
    lStart = StartIndex;
    lEnd = EndIndex;
    bStacked = PushAcpiNameSpace(gchName, &gAcpiNameStack);*/

    ByteData = AML_BYTE;
    AmlAppend(" ,0x%X, ", ByteData);
    NEXT_AML;
    memcpy(&WordData, AML_PTR, sizeof(UINT16));
    AmlAppend("0x%X)\n", WordData);
    INC_AML(2);


    SetupTab();
    AmlAppend("{\n");

    m_nTab++;

    //
    // Handle the ObjectList
    //  
    lpScopeEnd += PkgLength;
    while (lpScopeEnd >= *lpAml) {
        SetupTab();
        HandleObjectList(lpAml);
        AmlAppend("\n");
    }
    m_nTab--;
    SetupTab();
    AmlAppend("}");

    /*if (bStacked)
        PopAcpiNameSpace(&gAcpiNameStack);

    for (DeviceNameLength = lStart * 4; DeviceNameLength < lEnd * 4; DeviceNameLength++) {
        gchName[DeviceNameLength] = 0;
    }*/
}

VOID HandleThermalZone(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    LPBYTE          lpScopeEnd = Name;
    UINT            PkgLength;

    NEXT_AML;
    //SetupTab ();
    PkgLength = GetPkgLength(lpAml);
    lpScopeEnd += PkgLength + 1;
    AmlAppend("ThermalZone (");
    Name = *lpAml;
    HandleNameString(lpAml);
   /* GetNameString((UINT32*)gchName, Name);
    lStart = StartIndex;
    lEnd = EndIndex;
    bStacked = PushAcpiNameSpace(gchName, &gAcpiNameStack);*/
    AmlAppend(")\n");   \
        SetupTab();
    AmlAppend("{\n");
    m_nTab++;

    //
    // Handle the ObjectList
    //      
    while (lpScopeEnd > * lpAml) {
        SetupTab();
        HandleObjectList(lpAml);
        AmlAppend("\n");
    }

    m_nTab--;
    SetupTab();
    AmlAppend("}");

    /*if (bStacked)
        PopAcpiNameSpace(&gAcpiNameStack);

    for (DeviceNameLength = lStart * 4; DeviceNameLength < lEnd * 4; DeviceNameLength++) {
        gchName[DeviceNameLength] = 0;
    }*/
}

VOID HandleBreak(LPBYTE* lpAml)
{
    NEXT_AML;
    //SetupTab ();
    AmlAppend("Break");

}

VOID HandleBreakPoint(LPBYTE* lpAml)
{
    NEXT_AML;
    //SetupTab ();
    AmlAppend("BreakPoint");

}

VOID HandleContinue(LPBYTE* lpAml)
{
    NEXT_AML;
    //SetupTab ();
    AmlAppend("Continue");

}

VOID HandleElse(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    UINT            PkgLength;


    if (Name[0] == ELSE_OP) {
        NEXT_AML;
        //SetupTab ();  
        AmlAppend("Else\n");
        SetupTab();
        AmlAppend("{\n");
        m_nTab++;
        PkgLength = GetPkgLength(lpAml);
        //
        // Disamble internal name space
        //  
        Name += PkgLength;
        while (Name >= (*lpAml)) {
            SetupTab();
            HandleTermList(lpAml);
            AmlAppend("\n");
        }
        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }

}

VOID HandleIfElse(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    UINT            PkgLength;

    if (Name[0] == IF_OP) {
        NEXT_AML;
        //SetupTab ();  
        AmlAppend("If (");
        PkgLength = GetPkgLength(lpAml);

        HandleTermArg(lpAml);
        AmlAppend(")\n");
        SetupTab();
        AmlAppend("{\n");
        m_nTab++;
        //
        // Disamble internal name space
        //  
        Name += PkgLength;
        while (Name >= (*lpAml)) {
            SetupTab();
            HandleTermList(lpAml);
            AmlAppend("\n");
        }
        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }

}

VOID HandleNoop(LPBYTE* lpAml)
{
    NEXT_AML;
    //SetupTab ();  
    AmlAppend("Noop");

}

VOID HandleNotify(LPBYTE* lpAml)
{
    NEXT_AML;
    //SetupTab ();  
    AmlAppend("Notify (");
    HandleSuperName(lpAml);
    AmlAppend(", ");
    //HandleInteger (lpAml, NULL);
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleReturn(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();  
    AmlAppend("Return (");
    //HandleDataRefObject (lpAml);
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleCopyObject(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("Copy (");

    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleSimpleName(lpAml);
    AmlAppend(")");

}

VOID HandleDecrement(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("Decrement (");
    HandleSuperName(lpAml);
    AmlAppend(")");
}

VOID HandleDivide(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("Divide (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);


    AmlAppend(",");
    if (IsNull(*lpAml)) {
        NEXT_AML;
        //AmlAppend(  ")"); 
        //return;
    }
    else {
        HandleTarget(lpAml);
    }

    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    AmlAppend(")");
}

VOID HandleFindSetLeftBit(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("FindSetLeftBit (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTarget(lpAml);
    AmlAppend(")");
}

VOID HandleFindSetRightBit(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("FindSetRightBit (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTarget(lpAml);
    AmlAppend(")");
}

VOID HandleIncrement(LPBYTE* lpAml)
{
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("Increment (");
    HandleSuperName(lpAml);
    AmlAppend(")");
}

VOID HandleLAnd(LPBYTE* lpAml)
{
    //ASL_CODE_POS    pos;
    //SetupTab ();  
    (*lpAml)++;
    AmlAppend("LAnd (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");
}

VOID HandleLEqual(LPBYTE* lpAml)
{
    NEXT_AML;
    AmlAppend("LEqual (");


    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    AmlAppend(")");
}

VOID HandleLGreater(LPBYTE* lpAml)
{
    NEXT_AML;
    AmlAppend("LGreater (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");
}

VOID HandleLGreaterEqual(LPBYTE* lpAml)
{
    NEXT_AML;
    AmlAppend("LGreaterEqual (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");
}

VOID HandleLLess(LPBYTE* lpAml)
{
    NEXT_AML;
    AmlAppend("LLess (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");
}

VOID HandleLLessEqual(LPBYTE* lpAml)
{
    NEXT_AML;
    AmlAppend("LLessEqual (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");
}

VOID HandleLNot(LPBYTE* lpAml)
{
    // not 
    LPBYTE Name = *lpAml;
    switch (Name[1]) {
    case LGREATER_OP:
        NEXT_AML;
        HandleLLessEqual(lpAml);
        break;
    case LEQUAL_OP:
        NEXT_AML;
        HandleLNotEqual(lpAml);
        break;
    case LLESS_OP:
        NEXT_AML;
        HandleLGreaterEqual(lpAml);
        break;
    default:
        NEXT_AML;
        AmlAppend("LNot (");
        HandleTermArg(lpAml);
        //AmlAppend(  ")\n");
        AmlAppend(")");
        break;
    }    
}

VOID HandleLNotEqual(LPBYTE* lpAml)
{
    NEXT_AML;
    AmlAppend("LNotEqual (");
    HandleTermArg(lpAml);
    AmlAppend(",");
    HandleTermArg(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");
     
    
}

VOID HandleNAnd(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("NAnd (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleNOr(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("NOr (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");
}

VOID HandleNot(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();
    AmlAppend("Not (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");
}

VOID HandleObjectType(LPBYTE* lpAml)
{
    (*lpAml)++;
    //SetupTab ();
    AmlAppend("ObjectType (");
    HandleSuperName(lpAml);
    AmlAppend(")");
}

VOID HandleOr(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("Or (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    AmlAppend(")");
}



VOID HandleShiftRight(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("ShiftRight (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleSizeOf(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("SizeOf (");
    HandleSuperName(lpAml);
    AmlAppend(")");

}

VOID HandleStore(LPBYTE* lpAml)
{

    (*lpAml)++;
    AmlAppend("Store (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleSuperName(lpAml);
    AmlAppend(")");

}

VOID HandleSubstract(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("Subtract (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    if (IsNull(*lpAml)) {
        NEXT_AML;
        AmlAppend(")");
        return;
    }
    if (IsTarget(*lpAml)) {
        AmlAppend(", ");
        HandleTarget(lpAml);
    }
    //AmlAppend(  ", ");    
    //HandleTarget (lpAml);       
    AmlAppend(")");

}

VOID HandleToBuffer(LPBYTE* lpAml)
{

    NEXT_AML;
    AmlAppend("ToBuffer (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleToDecimalString(LPBYTE* lpAml)
{
    //SetupTab ();
    NEXT_AML;
    AmlAppend("ToDecimalString (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleToHexString(LPBYTE* lpAml)
{
    //SetupTab ();
    NEXT_AML;
    AmlAppend("ToHexString (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleToInteger(LPBYTE* lpAml)
{
    //SetupTab ();
    NEXT_AML;
    AmlAppend("ToInteger (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");
}

VOID HandleToString(LPBYTE* lpAml)
{
    //SetupTab ();
    NEXT_AML;
    AmlAppend("ToString (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
}

VOID HandleXor(LPBYTE* lpAml)
{

    (*lpAml)++;
    //SetupTab ();
    AmlAppend("Xor (");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTermArg(lpAml);
    AmlAppend(", ");
    HandleTarget(lpAml);
    AmlAppend(")");

}

VOID HandleString(LPBYTE* lpAml)
{
    //
    //
    (*lpAml)++;
    AmlAppend("\"");

    while ((*lpAml)[0] != 0) {
        AmlAppend("%c", (*lpAml)[0]);
        if ((*lpAml)[0] == '\\') {
            AmlAppend("\\");
        }
        (*lpAml)++;
    }
    AmlAppend("\"");
    (*lpAml)++;
}

VOID HandleInteger(LPBYTE* lpAml, UINT64* pInteger)
{

    LPBYTE          Name = *lpAml;
    ASL_UINT64      Data;
    UINT64          Integer = 0;

    memcpy(&Data, Name + 1, sizeof(ASL_UINT64));

    switch (Name[0]) {
    case BYTE_PREFIX:
        (*lpAml)++;
        //(*lpAml) ++;
        //AmlAppend(  "0x%02X", Name[1]);
        HandleByteData(lpAml);
        Integer = (UINT64)Name[1];
        break;
    case WORD_PREFIX:
        (*lpAml)++;
        //(*lpAml) += 2;
        //AmlAppend(  "0x%04X", Data.Low & 0xFFFF);
        HandleWordData(lpAml);
        Integer = (UINT64)(Data.Low & 0xFFFF);
        break;
    case DWORD_PREFIX:
        (*lpAml)++;
        HandleDWordData(lpAml);
        //(*lpAml) += 4;
        //if ((Data.Low & 0xFFFF) == 0xD041) {
        //    __asm int 3;
        //    }
        //AmlAppend(  "0x%08X", Data.Low);
        Integer = (UINT64)(Data.Low);
        break;
    case QWORD_PREFIX:
        (*lpAml)++;
        //(*lpAml) += 8;
        //AmlAppend(  "0x%08X %08X", Data.High, Data.Low);
        HandleQWordData(lpAml);
        memcpy(&Integer, &Data, sizeof(UINT64));
        break;
    case ZERO_OP:
        AmlAppend("Zero");
        (*lpAml)++;
        Integer = 0;
        break;
    case ONE_OP:
        AmlAppend("One");
        (*lpAml)++;
        Integer = 1;
        break;
    case ONES_OP:
        AmlAppend("Ones");
        (*lpAml)++;
        Integer = 0xFFFFFFFFFFFFFFFF;
        break;
    default:
        assert(0);
        //__asm int 3;
        //MessageBox (NULL, "HandleInteger", "Error", MB_ICONERROR);
        break;
    }

    if (pInteger != NULL) {
        *pInteger = Integer;
    }

}

VOID HandleNameSpaceModifierObj(LPBYTE* lpAml)
{
    HandleNameSpace(lpAml);
}

VOID HandleNamedObject(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    //
    // Function Term?
    // 
    switch (Name[0]) {
    case CREATE_BIT_FIELD_OP:
        HandleCreateField(lpAml);
        break;
    case  CREATE_BYTE_FIELD_OP:
        HandleCreateField(lpAml);
        break;
    case  CREATE_DWORD_FIELD_OP:
        HandleCreateField(lpAml);
        break;
    case CREATE_QWORD_FIELD_OP:
        HandleCreateField(lpAml);
        break;
    case  CREATE_WORD_FIELD_OP:
        HandleCreateField(lpAml);
        break;
    case METHOD_OP:
        //gMethodLevel++;
        HandleMethod(lpAml);
        //gMethodLevel--;
        break;
    case EXTERNAL_OP:
        HandleExternal(lpAml);
        break;
    case EXT_OP:
        //NEXT_AML;
        HandleExtendOP(lpAml);
        break;
        /*
        switch (Name[1]) {
        case BANK_FIELD_OP:
            NEXT_AML;
            HandleField (lpAml);
            break;
        case  CREATE_BIT_FIELD) {
        NEXT_AML;
        HandleCreateField (lpAml);
    }
        case  DATA_REGION_OP) {
        NEXT_AML;
        HandleDataRegion (lpAml);
    }
        case  DEVICE_OP) {
        NEXT_AML;
        HandleDevice (lpAml);
    }
        case  EVENT_OP) {
        NEXT_AML;
        HandleEvent (lpAml);
    }
        case  FIELD_OP) {
        NEXT_AML;
        HandleField (lpAml);
    }
        case INDEX_FIELD_OP) {
        NEXT_AML;
        HandleField (lpAml);
    }
        case  MUTEX_OP) {
        NEXT_AML;
        HandleMutex (lpAml);
    }
         case  OPREGION_OP) {
        NEXT_AML;
        HandleOpRegion (lpAml);
    }
        case  POWER_RES_OP) {
        NEXT_AML;
        HandlePowerRes (lpAml);
    }
        case  PROCESSOR_OP) {
        NEXT_AML;
        HandleProcessor (lpAml);
    }
        case  THERMAL_ZONE_OP) {
        NEXT_AML;
        HandleThermalZone (lpAml);
    }
        }
        */
    default:
        break;
    }

}

VOID HandleBankValue(LPBYTE* lpAml)
{
    HandleTermArg(lpAml);
}

VOID HandleField(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    UINT            PkgLength;

    NEXT_AML;
    PkgLength = GetPkgLength(lpAml);
    //SetupTab ();
    switch (Name[0])
    {
    case FIELD_OP:
        AmlAppend("Field (");
        HandleNameString(lpAml);
        break;
    case INDEX_FIELD_OP:
        AmlAppend("IndexField (");
        HandleNameString(lpAml);
        AmlAppend(", ");
        HandleNameString(lpAml);
        break;
    case BANK_FIELD_OP:
        AmlAppend("BankField (");
        HandleNameString(lpAml);
        AmlAppend(", ");
        HandleNameString(lpAml);
        AmlAppend(", ");
        HandleBankValue(lpAml);
        break;
    default:
        //__asm int 3;
        //MessageBox (NULL, "HandleField", "Error", MB_ICONERROR);
        assert(0);
    }
    HandleFieldFlags(lpAml);
    Name += PkgLength;
    AmlAppend("\n");
    SetupTab();
    AmlAppend("{\n");
    HandleFieldList((*lpAml), Name);
    SetupTab();
    AmlAppend("}");
    (*lpAml) = Name;
    NEXT_AML;
}

VOID HandleFieldFlags(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    FIELD_FLAG      Flags;

    memcpy(&Flags, Name, sizeof(UINT8));
    if (Flags.AccessType >= 6) {
        AmlAppend(", Reserved");
    }
    else {
        AmlAppend(", %s", strFieldType[Flags.AccessType]);
    }

    if (Flags.LockRule == 1) {
        AmlAppend(", Lock");
    }
    else {
        AmlAppend(", NoLock");
    }
    if (Flags.UpdateRule == 0) {
        AmlAppend(", Preserve)");
    }
    else if (Flags.UpdateRule == 1) {
        AmlAppend(", WriteAsOnes)");
    }
    else if (Flags.UpdateRule == 2) {
        AmlAppend(", WriteAsZeros)");
    }
    else {
        //__asm int 3;
        //MessageBox (NULL, "HandleFieldFlags", "Error", MB_ICONERROR);
        assert(0);
    }
    NEXT_AML;

}

VOID HandleFieldList(LPBYTE lpAml, LPBYTE lpAmlEnd)
{
    UINT            PkgLengh;
    UINT8           AccessType;
    UINT8           AccessAttrib;
    LPBYTE          lpFieldList = lpAml;
    UINT            Offset;

    Offset = 0;

    while (lpAmlEnd > lpFieldList) {
        SetupTab();
        if (lpFieldList[0] == 0) {
            //
            // Rervesed Field 
            //
            lpFieldList++;
            PkgLengh = GetPkgLength(&lpFieldList);

            Offset += PkgLengh;

            if ((Offset % 8) == 0) {
                AmlAppend("    Offset(0x%x)", Offset / 8);
            }
            else {
                AmlAppend("    , %d", PkgLengh);
            }
        }
        else if (lpFieldList[0] == 1) {
            lpFieldList++;
            AccessType = lpFieldList[0];
            lpFieldList++;
            AccessAttrib = lpFieldList[0];
            lpFieldList++;
            //PkgLengh = GetPkgLength (&lpFieldList);
            //Offset += PkgLengh;
            if (AccessType < 6) {
                AmlAppend("    AccessAs(%s), 0x%X", strFieldType[AccessType], AccessAttrib);
            }
        }
        else {
            AmlAppend("    ");
            //HandleNameString (&lpFieldList);
            HandleNameSeg(&lpFieldList);
            PkgLengh = GetPkgLength(&lpFieldList);
            Offset += PkgLengh;
            AmlAppend(", %d", PkgLengh);
        }
        if (lpAmlEnd > lpFieldList) {
            AmlAppend(",\n");
        }
        else {
            AmlAppend("\n");
        }
    }


}
int 
PathLengh(PACPI_NAMESPACE pAcpiNS) {
    int Length = 0;
    while (pAcpiNS->pParent != NULL) {
        pAcpiNS = pAcpiNS->pParent;
        Length++;
        if (Length > 256) {
            return -1;
        }
    }
    return Length;
}

void
ToPathName(PACPI_NAMESPACE pAcpiNSMethod, char *pName) {
    PACPI_NAMESPACE pAcpiNS = pAcpiNSMethod;
    int Length = 0;
    while (pAcpiNS->pParent != NULL) {
        pAcpiNS = pAcpiNS->pParent;
        Length++;
        if (Length > 256) {
            return;
        }
    }
    if (Length == 0) {
        pName[1] = 0;
    }
    pName[Length * 5] = 0;
    pAcpiNS = pAcpiNSMethod;
    while (pAcpiNS->pParent != NULL) {
        pName[Length * 5 - 4] = pAcpiNS->MethodName[0];
        pName[Length * 5 - 3] = pAcpiNS->MethodName[1];
        pName[Length * 5 - 2] = pAcpiNS->MethodName[2];
        pName[Length * 5 - 1] = pAcpiNS->MethodName[3];
        pAcpiNS = pAcpiNS->pParent;  
        if (pAcpiNS->pParent != NULL) {
            pName[Length * 5 - 5] = '.';
        }
        Length--;

    }
    pName[0] = '\\'; 
    return;
}

BOOL MethodMatch(ACPI_NAMESPACE * pAcpiNS,char *pName)
{
    ACPI_NAMESPACE* pRootNS;
    char       pMethodName[MAX_NAME_SPACE_PATH * 5 + 1];
    ToPathName(pAcpiNS, pMethodName);
    BOOLEAN bMethod = FALSE;
    if (pName[0] == '\\') {
        if (strcmp(pName, pMethodName) == 0) {
            // this is the method
            bMethod = TRUE;
        }
    }
    else {
        char pCurrentPath[MAX_NAME_SPACE_PATH * 5 + 1];
        char pFullPath[MAX_NAME_SPACE_PATH * 5 + 1];
        // build the whole path...
        ToPathName(m_pParserNS, pCurrentPath);
        if (FAILED(StringCbPrintfA(pFullPath, MAX_NAME_LENGTH, "%s.%s", pCurrentPath, pName))) {
            assert(0);
        }
        if (strcmp(pFullPath, pMethodName) == 0) {
            // this is the method
            bMethod = TRUE;
        }
        else {
            pRootNS = m_pParserNS->pParent;
            // try to find the most match method to avoid multi match, if only 1 method always matching easily, mutiple need to get the closest
            // could be simplify by compare the root of sub
            while (pRootNS != NULL) {
                ToPathName(pRootNS, pCurrentPath);
                if (pCurrentPath[1] != 0) {
                    if (FAILED(StringCbPrintfA(pFullPath, MAX_NAME_LENGTH, "%s.%s", pCurrentPath, pName))) {
                        assert(0);
                    }
                }
                else {
                    if (FAILED(StringCbPrintfA(pFullPath, MAX_NAME_LENGTH, "%s%s", pCurrentPath, pName))) {
                        assert(0);
                    }
                }
                if (strcmp(pFullPath, pMethodName) == 0) {
                    // this is the method
                    bMethod = TRUE;
                    break;
                }
                pRootNS = pRootNS->pParent;
            }
        }

    }
    return bMethod;
}


BOOL HandleUserTermObj(LPBYTE* lpAml)
{
    UCHAR   ArgCount;
    char    pName[MAX_NAME_SPACE_PATH * 5 + 1];
     //ACPI_NAME_SPACE_PATH* pAnsp = &gAcpiNameStack;
    LPBYTE  Name = *lpAml;
    size_t  NameLength;
    ACPI_METHOD_MAP* pMethodMap;
    ChGetNameString(pName, Name);

    NameLength = strlen(pName);
    pMethodMap = GetMethodMap(*((UINT32 *)&pName[NameLength - 4]));
    if (pMethodMap == NULL) {
        return FALSE;
    }
    if (pMethodMap->Count == 1) {
        // check path matching.... Build the name path and comapre the name path
        
       HandleNameString(lpAml);
        if (MethodMatch(pMethodMap->pAcpiNS[0], pName)) {
            ArgCount = pMethodMap->pAcpiNS[0]->ArgCount & 0x7;
            AmlAppend(" (");
            for (int Idx = 0; Idx < (int)ArgCount; Idx++) {
                if (Idx != 0) {
                    AmlAppend(", ");
                }
                HandleTermArgList(lpAml);
            }
            AmlAppend(")");
		}
		else {
			NameLength = strlen(pName);
		}
        //return TRUE;
    }
    else {
        // check path matching??        
        HandleNameString(lpAml);
        for (USHORT uIndex = 0; uIndex < pMethodMap->Count; uIndex++) {
            if (MethodMatch(pMethodMap->pAcpiNS[uIndex], pName)) {
                ArgCount = pMethodMap->pAcpiNS[uIndex]->ArgCount & 0x7;
                AmlAppend(" (");
                for (int Idx = 0; Idx < (int)ArgCount; Idx++) {
                    if (Idx != 0) {
                        AmlAppend(", ");
                    }
                    HandleTermArgList(lpAml);
                }
                AmlAppend(")");
				break;
            }
			else {
				NameLength = strlen(pName);
			}
        }
        //assert(0);
    }
    //assert(0);
    return TRUE;
}

VOID HandleObject(LPBYTE* lpAml)
{
    if (IsNameSpaceModifierObject(*lpAml)) {
        HandleNameSpaceModifierObj(lpAml);
    }
    else if (IsNamedObject(*lpAml)) {
        HandleNamedObject(lpAml);
    }
    else {
        assert(0);
    }
}

VOID HandleName(LPBYTE* lpAml)
{
    //SetupTab ();
    AmlAppend("Name (");
    (*lpAml)++;
    HandleNameString(lpAml);
    AmlAppend(", ");
    HandleDataRefObject(lpAml);
    AmlAppend(")");
}

VOID HandleAlias(LPBYTE* lpAml)
{
    //SetupTab ();
    AmlAppend("Alias (");
    NEXT_AML;
    HandleNameString(lpAml);
    AmlAppend(", ");
    HandleNameString(lpAml);
    //AmlAppend(  ")\n");
    AmlAppend(")");

}

VOID HandleScope(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    UINT            PkgLengh;
    LPBYTE          lpScopeEnd;

    lpScopeEnd = *lpAml;
    //SetupTab ();
    AmlAppend("Scope (");
    (*lpAml)++;
    PkgLengh = GetPkgLength(lpAml);

    Name = *lpAml;
    HandleNameString(lpAml);
    /*bStacked = PushAcpiNameSpace(gchName, &gAcpiNameStack);
    memset(gchName, 0, MAX_NAME_SPACE_PATH);
    GetNameString((UINT32*)gchName, Name);
    lStart = StartIndex;
    lEnd = EndIndex;
    if (gAcpiNameStack.chNameSpace[0] != 0) {
        StringCbPrintf(pRoot, MAX_NAME_SPACE_PATH, gAcpiNameStack.chNameSpace);
        ChGetNameString(pName, Name);
        if (MakeNamePath(pName, pRoot)) {
            memset(gchName, 0, MAX_NAME_SPACE_PATH);
            memcpy(gchName, pRoot, strlen(pRoot));
        }
    }*/
    AmlAppend(")\n");
    SetupTab();
    AmlAppend("{\n");
    m_nTab++;

    lpScopeEnd += PkgLengh;
    while (lpScopeEnd >= *lpAml) {
        SetupTab();
        HandleTermObj(lpAml);
        AmlAppend("\n");
    }

    m_nTab--;
    SetupTab();
    AmlAppend("}");
    //if (bStacked) {
    //    PopAcpiNameSpace(&gAcpiNameStack);
    //    //
    //    // Restore the the scope
    //    // 
    //    memset(gchName, 0, MAX_NAME_SPACE_PATH);
    //    StringCbPrintf(gchName, MAX_NAME_SPACE_PATH, gAcpiNameStack.chNameSpace);
    //}
    (*lpAml) = lpScopeEnd;
    (*lpAml)++;
}


VOID HandleNameSpace(LPBYTE* lpAml)
{
    LPBYTE          Name = *lpAml;
    switch (Name[0]) {
    case NAME_OP:
        HandleName(lpAml);
        break;
    case ALIAS_OP:
        HandleAlias(lpAml);
        break;
    case SCOPE_OP:
        HandleScope(lpAml);
        break;
    }
}

VOID HandleMethod(LPBYTE* lpAml)
{

    UINT            PkgLengh;
    UINT8           Flags;
    LPBYTE          lpMethodEnd;
    //BOOL            bNamed;

    //SetupTab ();
    lpMethodEnd = *lpAml;
    AmlAppend("Method (");
    (*lpAml)++;
    PkgLengh = GetPkgLength(lpAml);

    HandleNameString(lpAml);
    Flags = AML_BYTE;
    AmlAppend(", %d", Flags & 0x07);
    if (Flags & 0x08) {
        AmlAppend(", Serialized)\n");
    }
    else {
        AmlAppend(", NotSerialized)\n");
    }

    //
    // Skip the Method Flag
    //
    (*lpAml)++;

    //
    // Disamble internal name space
    //  
    lpMethodEnd += PkgLengh;
    SetupTab();
    AmlAppend("{\n");
    m_nTab++;
    while (lpMethodEnd >= (*lpAml)) {
        SetupTab();
        HandleTermList(lpAml);
        AmlAppend("\n");
    }

    m_nTab--;
    SetupTab();
    AmlAppend("}");
    (*lpAml) = lpMethodEnd;

    (*lpAml)++;

}

VOID HandleTableHead(LPBYTE* lpAml)
{

    PACPI_DESCRIPTION_HEADER Header;
    Header = (PACPI_DESCRIPTION_HEADER)(*lpAml);

    AmlAppend(
        "\r\nDefinitionBlock (\"\",\"%c%c%c%c\"",
        (UINT8)Header->Signature,
        (UINT8)(Header->Signature >> 8),
        (UINT8)(Header->Signature >> 16),
        (UINT8)(Header->Signature >> 24)
    );
    AmlAppend(", %d", Header->Revision);
    AmlAppend(", \"%c%c%c%c%c%c\"",
        Header->OemId[0],
        Header->OemId[1],
        Header->OemId[2],
        Header->OemId[3],
        Header->OemId[4],
        Header->OemId[5]
    );
    AmlAppend(", \"%c%c%c%c%c%c%c%c\"",
        (UINT8)Header->OemTableId,
        (UINT8)(Header->OemTableId >> 8),
        (UINT8)(Header->OemTableId >> 16),
        (UINT8)(Header->OemTableId >> 24),
        (UINT8)(Header->OemTableId >> 32),
        (UINT8)(Header->OemTableId >> 40),
        (UINT8)(Header->OemTableId >> 48),
        (UINT8)(Header->OemTableId >> 56)
    );
    AmlAppend(" ,0x08%X", Header->OemRevision);
    /*
    AmlAppend( " ,\"%c%c%c%c\"",
        (UINT8) Header->CreatorId,
        (UINT8) (Header->CreatorId >> 8),
        (UINT8) (Header->CreatorId >> 16),
        (UINT8) (Header->CreatorId >> 24)
        );
    //AmlAppend( " ,0x%X", Header->CreatorRevision);
    */
    AmlAppend(")\n");

    (*lpAml) += sizeof(ACPI_DESCRIPTION_HEADER);
}


BOOLEAN
VerifyGenericRegisterBuffer(
    PACPI_METHOD_ARGUMENT arg,
    LPSTR pName,
    BOOLEAN bPrint)
{
    ULONG* pUlong;

    if (arg->Type != ACPI_METHOD_ARGUMENT_BUFFER) {
        return FALSE;
    }
    //
    // Verify number of DWORD/Integer Data
    //
    if (arg->DataLength != 0x11) {
        return FALSE;
    }

    if (arg->Data[0] != 0x82) {
        return FALSE;
    }

    if (arg->Data[1] != 0x0C) {
        return FALSE;
    }
    if (bPrint) {
        pUlong = (PULONG)&arg->Data[7];
        AmlAppend("%s (%s, %d, %d",
            pName,
            GetResouceType(arg->Data[3]),
            arg->Data[4],
            arg->Data[5]
        );
        if (pUlong[1] == 0) {
            AmlAppend(", %X)", pUlong[0]);
        }
        else {
            AmlAppend(", %08X%08X)", pUlong[1], pUlong[0]);
        }
    }
    return TRUE;
}


ACPI_NAMESPACE* GetAcpiNsFromObjData(PVOID pVoid)
{
    ULONG Index;
    if (pLocalAcpiNS == NULL || uLocalAcpiNSCount == 0) {
        return NULL;
    }

    for (Index = 0; Index < uLocalAcpiNSCount; Index++) {
        if (pLocalAcpiNS[Index].Contain == pVoid) {
            return &pLocalAcpiNS[Index];
        }
    }
    return NULL;
}


ACPI_NAMESPACE* GetAcpiNsFromNsAddr(PVOID pVoid)
{
    ULONG Index;
    if (pLocalAcpiNS == NULL || uLocalAcpiNSCount == 0) {
        return NULL;
    }

    for (Index = 0; Index < uLocalAcpiNSCount; Index++) {
        if (pLocalAcpiNS[Index].pKernelAddr == pVoid) {
            return &pLocalAcpiNS[Index];
        }
    }
    return NULL;
}

VOID
AmlParser(
    PACPI_NAMESPACE pNode,
    char* Scope
)
{
    
    UNREFERENCED_PARAMETER(Scope);
    //ACPI_OBJ* pAcpiObj;
    m_pParserNS = pNode;
    if (pNode == NULL) {
        return;
    }
    if (DataParser(pNode)) {
        return;
    }
    if (pNode->Type == ACPI_TYPE_FIELDUNIT) {
        PACPI_NAMESPACE pFieldNs;
        PVOID* pField = pNode->pUserContain;    // find the kernel...
        pFieldNs = pNode;

        while (pFieldNs->pPrev != NULL) {
            if (pFieldNs->pKernelAddr == *pField) {
                break;
            }
            pFieldNs = pFieldNs->pPrev;
        }
        if (pFieldNs->pPrev != NULL) {
            DataParser(pFieldNs->pPrev);
        }
        AmlAppend("\n");
        DataParser(pFieldNs);
        
        //assert(0);
        return;
    }
    {
        ULARGE_INTEGER ul;
        //gAcpiNameSpace[Index].pUserContain
        ul.QuadPart = (ULONGLONG)pNode->pUserContain;
        AmlAppend("Defaut %X %X %08lX%08lX", pNode->Type, pNode->Length, ul.HighPart, ul.LowPart);
        //AppendTextWin();
    }
}


ACPI_BUFFER_PARSER gAcpiBufferParser[] = {
    { ACPI_SIGNATURE('_','T','S','S'), VerifyTss },
    { ACPI_SIGNATURE('_','P','S','S'), VerifyPss },
    { ACPI_SIGNATURE('_','P','T','C'), VerifyPtc },
    { ACPI_SIGNATURE('_','P','C','T'), VerifyPtc },
    { ACPI_SIGNATURE('_','P','S','D'), VerifyXsd },
    { ACPI_SIGNATURE('_','T','S','D'), VerifyXsd },
    { ACPI_SIGNATURE('_','C','S','D'), VerifyXsd },
    { ACPI_SIGNATURE('_','C','S','T'), VerifyCst },
    { ACPI_SIGNATURE('_','C','R','S'), VerifyXrs },
    { ACPI_SIGNATURE('_','P','R','S'), VerifyXrs }
};

ACPI_ARGUMENT_PARSER gAcpiArgParser[] = {
    { ACPI_METHOD_ARGUMENT_INTEGER, IntArg},
    { ACPI_METHOD_ARGUMENT_STRING, StrArg },
    { ACPI_METHOD_ARGUMENT_BUFFER, BufArg},
    { ACPI_METHOD_ARGUMENT_PACKAGE, PkgArg},
    { ACPI_METHOD_ARGUMENT_PACKAGE_EX, PkgExArg}
};

ACPI_DATA_PARSER gAcpiDataParser[] = {
    { ACPI_TYPE_SCOPE, Device},
    { ACPI_TYPE_INTEGER, IntData},
    { ACPI_TYPE_STRING, StrData },
    { ACPI_TYPE_BUFFER, BufData},
    { ACPI_TYPE_PACKAGE, PkgData},
    //{ ACPI_TYPE_FIELDUNIT, FieldUnit},
    { ACPI_TYPE_DEVICE, Device},
    { ACPI_TYPE_SYNC_OBJECT, SyncObj},
    { ACPI_TYPE_METHOD, Method},
    { ACPI_TYPE_MUTEX, Mutex},
    { ACPI_TYPE_OPERATION_REG,OpReg},
    // ACPI_TYPE_POWER_SOURCE
    { ACPI_TYPE_PROCESSOR, Device},
    { ACPI_TYPE_TMERMAL_ZONE, Device},
    { ACPI_TYPE_BUFFUNIT, BufField },
    // ACPI_TYPE_DDBHANDLE
    // ACPI_TYPE_DEBUG
    { ACPI_TYPE_ALIAS, Alias},
    // ACPI_TYPE_DATAALIAS
    { ACPI_TYPE_BANKFIELD, BankField },
    { ACPI_TYPE_FIELD, Field},
    { ACPI_TYPE_INDEXFIELD, Field },
    // ACPI_TYPE_DATA         
    // ACPI_TYPE_DATAFIELD
    // ACPI_TYPE_DATAOBJ
};

VOID
SyncObj(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
    Parse Acpi Device Object

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    assert(0);
}

VOID
Device(
    ACPI_NAMESPACE* pAcpiNS
)
/*++

Routine Description:
    Parse Acpi Device Object

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    ACPI_NAMESPACE* pAcpiNSLast = pAcpiNS->pChild;
    ACPI_NAMESPACE* pAcpiNSChild = pAcpiNS->pChild;
    UNREFERENCED_PARAMETER(pAcpiNS);
    // TODO: Potentially AML code struct change on every windows release	
    static char MethodFlags[][20] = {
        {"NotSerialized"},
        {"Serialized"}
    };

    // Get the method power
    SetupTab();
    if (pAcpiNS == NULL) {
        return;
    }
    // Link to some place???
    if (pAcpiNS->Length < 0xC2) {
        if (pAcpiNS->Type == ACPI_TYPE_DEVICE) {
            AmlAppend("Device(%C%C%C%C) {\n", pAcpiNS->MethodName[0], pAcpiNS->MethodName[1],
                pAcpiNS->MethodName[2], pAcpiNS->MethodName[3]);
        }
        else if (pAcpiNS->Type == ACPI_TYPE_PROCESSOR) {
            ACPI_PROCESSOR* pProc = (ACPI_PROCESSOR*)pAcpiNS->pUserContain;
            AmlAppend("Processor(%C%C%C%C,0x%x,0x%x,%x) {\n", pAcpiNS->MethodName[0], pAcpiNS->MethodName[1],
                pAcpiNS->MethodName[2], pAcpiNS->MethodName[3], pProc->ProcID, pProc->PblkAddr, pProc->PblkLen);
        }
        else {
            AmlAppend("Scope(%C%C%C%C) {\n", pAcpiNS->MethodName[0], pAcpiNS->MethodName[1],
                pAcpiNS->MethodName[2], pAcpiNS->MethodName[3]);
        }
        // TODO:	// name space..
        m_nTab++;
        do {
            if (pAcpiNSChild == NULL) {
                break;
            }
			m_pParserNS = pAcpiNSChild;
            if (DataParser(pAcpiNSChild)) {
                AmlAppend("\n");
            }
            else {
                if (pAcpiNSChild->Type != ACPI_TYPE_FIELDUNIT) {
                    assert(0);
                }
            }
            pAcpiNSChild = pAcpiNSChild->pNext;
            if (pAcpiNSChild == NULL || pAcpiNSChild == pAcpiNSLast) {
                break;
            }
        } while (pAcpiNSChild != NULL && pAcpiNSChild != pAcpiNSLast);

        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }
}

LPBYTE
RecreateFullNS(
    ACPI_NAMESPACE* pAcpiNS,
    UCHAR			OpCode
)
{
    ULONG	PkgLength;
    ACPI_AML_CODE* pAml;
    // get method address
    pAml = (ACPI_AML_CODE*)pAcpiNS->pUserContain;

    // TODO: Potentially AML code struct change on every windows release
    PkgLength = pAcpiNS->Length - 0xC1;	// size from Flags
    // now adjust the size of total
    PkgLength += 4;	// add size of name seg 4 bytes

    if (PkgLength > (0xFFFFF - 3)) {
        ACPI_METHOD_PKG4* pPkg = (ACPI_METHOD_PKG4*)((&(pAml->rsvd1)) - sizeof(ACPI_METHOD_PKG4) + 2);
        PkgLength += 4;
        memcpy(pPkg->uName, pAcpiNS->MethodName, 4);
        pPkg->MethodOp = OpCode;
        pPkg->PkgLength[0] = (UCHAR)(PkgLength & 0xF) | 0xC0;
        pPkg->PkgLength[1] = (UCHAR)(PkgLength >> 4);
        pPkg->PkgLength[2] = (UCHAR)(PkgLength >> 12);
        pPkg->PkgLength[3] = (UCHAR)(PkgLength >> 20);
        return &pPkg->MethodOp;
    }
    else if (PkgLength > 4093) {
        ACPI_METHOD_PKG3* pPkg = (ACPI_METHOD_PKG3*)((&(pAml->rsvd1)) - sizeof(ACPI_METHOD_PKG3) + 2);
        PkgLength += 3;
        memcpy(pPkg->uName, pAcpiNS->MethodName, 4);
        pPkg->MethodOp = OpCode;
        pPkg->PkgLength[0] = (UCHAR)(PkgLength & 0xF) | 0x80;
        pPkg->PkgLength[1] = (UCHAR)(PkgLength >> 4);
        pPkg->PkgLength[2] = (UCHAR)(PkgLength >> 12);
        return &pPkg->MethodOp;
    }
    else if (PkgLength > 62) {
        ACPI_METHOD_PKG2* pPkg = (ACPI_METHOD_PKG2*)((&(pAml->rsvd1)) - sizeof(ACPI_METHOD_PKG2) + 2);
        PkgLength += 2;
        memcpy(pPkg->uName, pAcpiNS->MethodName, 4);
        pPkg->MethodOp = OpCode;
        pPkg->PkgLength[0] = (UCHAR)(PkgLength & 0xF) | 0x40;
        pPkg->PkgLength[1] = (UCHAR)(PkgLength >> 4);
        return &pPkg->MethodOp;
    }
    else {
        ACPI_METHOD_PKG1* pPkg = (ACPI_METHOD_PKG1*)((&(pAml->rsvd1)) - sizeof(ACPI_METHOD_PKG1) + 2);
        PkgLength += 1;
        memcpy(pPkg->uName, pAcpiNS->MethodName, 4);
        pPkg->MethodOp = OpCode;
        pPkg->PkgLength = (UCHAR)PkgLength;
        return &pPkg->MethodOp;
    }

    // setup the AML buffer

    //return NULL;
}


VOID
Method(
    ACPI_NAMESPACE* pAcpiNS
)
/*++

Routine Description:
    Parse acpi Method Object

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pAcpiNS);
    // TODO: Potentially AML code struct change on every windows release
    ACPI_AML_CODE* pAml;
    static char MethodFlags[][20] = {
        {"NotSerialized"},
        {"Serialized"}
    };

    // Get the method power
    SetupTab();
    if (pAcpiNS == NULL) {
        return;
    }
    if (pAcpiNS->MethodNameAsUlong == ACPI_SIGNATURE('_', 'O', 'S', 'I'))
    {
        AmlAppend("Method(_OSI, 1, NoSerialized)\n");
        SetupTab();
        AmlAppend("{\n");
        m_nTab++;
        SetupTab();
        AmlAppend("OSI(Arg0)\n");
        m_nTab--;
        SetupTab();
        AmlAppend("}");
        return;
    }
    if (pAcpiNS->Length < 0xC2) {
        AmlAppend("Method(%C%C%C%C){}\n", pAcpiNS->MethodName[0], pAcpiNS->MethodName[1],
            pAcpiNS->MethodName[2], pAcpiNS->MethodName[3]);
        return;
    }

    pAml = (ACPI_AML_CODE*)pAcpiNS->pUserContain;
    if (pAml == NULL) {
        AmlAppend("Method(%C%C%C%C){}\n", pAcpiNS->MethodName[0], pAcpiNS->MethodName[1],
            pAcpiNS->MethodName[2], pAcpiNS->MethodName[3]);
        return;
    }
    // 0x14 is method op code
    LPBYTE pAmlCode = RecreateFullNS(pAcpiNS, 0x14);

    if (pAmlCode != NULL) {
        HandleMethod((LPBYTE*)&pAmlCode);
    }
}

VOID
SubFieldUnit(
    ACPI_NAMESPACE* pFileds
)
/*++

Routine Description:
    Parse feilds list

Arguments:

    pFileds - Field

Return Value:

--*/
{
    ACPI_FIELD_UNIT* pField;
    ULONG ByteOffset = 0;
    while (pFileds->Type == ACPI_TYPE_FIELDUNIT) {
        //FieldUnit(pFileds);
        pField = pFileds->pUserContain;
        if (pFileds->Type != ACPI_TYPE_FIELDUNIT) {
            break;
        }
        SetupTab();
        if (pFileds->MethodNameAsUlong != 0)
        {
            AmlAppend("%C%C%C%C",
                pFileds->MethodName[0],
                pFileds->MethodName[1],
                pFileds->MethodName[2],
                pFileds->MethodName[3]);
        }
        ByteOffset += pField->NumOfBits;
        if (pField->NumOfBits > 64) {
            if (ByteOffset % 8 == 0) {
                AmlAppend("Offset(0x%x)",
                    ByteOffset / 8
                );
            }
            else {
                AmlAppend(",%d",
                    pField->NumOfBits
                );
            }
        }
        else {
            AmlAppend(",%d",
                pField->NumOfBits
            );
        }
        //ByteOffset = pField->Offset;

        pFileds = pFileds->pNext;
        if (pFileds->Type == ACPI_TYPE_FIELDUNIT) {
            AmlAppend(",\n");
        }
        else {
            AmlAppend("\n");
        }
    }
}

VOID
FieldParameter(
    ACPI_FIELD_UNIT* pField
)
/*++

Routine Description:
    Fill field parameter

Arguments:

    pField - Feild type information

Return Value:

--*/
{
    if ((pField->Type & 0xf) < strFieldTypeLen) {

        AmlAppend("%s,", strFieldType[pField->Type & 0xf]);
    }
    else {
        AmlAppend("Reserved");
    }
    if ((pField->Type & 0x10) == 0) {

        AmlAppend("NoLock,");
    }
    else {
        AmlAppend("Lock,");
    }

    if ((pField->Type & 0x60) == 0) {

        AmlAppend("Preserve)\n");
    }
    else if ((pField->Type & 0x20) == 0) {
        AmlAppend("WriteAsOnes)\n");
    }
    else if ((pField->Type & 0x40) == 0) {
        AmlAppend("WriteAsZeros)\n");
    }
    else {
        AmlAppend("Reserved)\n");
    }
}


VOID
BankField(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
Parse acpi BankField Object

Arguments:

pNode - Acpi Data Name Space

Return Value:

--*/
{
    //ACPI_NAMESPACE* pRoot;
    ACPI_NAMESPACE* pName;
    ACPI_NAMESPACE* pBankName;
    ACPI_BANKFIELD* pField;
    ACPI_FIELD_UNIT* pFieldUnit;
    if (pNode == NULL) {
        return;
    }
    pField = (ACPI_BANKFIELD*)pNode->pUserContain;
    pName = GetAcpiNsFromNsAddr(pField->pOperationReg);
    pBankName = GetAcpiNsFromNsAddr(pField->pField);

    if (pName != NULL && pBankName != NULL) {
        SetupTab();
        AmlAppend("BankField(%C%C%C%C,", pName->MethodName[0], pName->MethodName[1],
            pName->MethodName[2], pName->MethodName[3]);
        AmlAppend("%C%C%C%C,", pBankName->MethodName[0], pBankName->MethodName[1],
            pBankName->MethodName[2], pBankName->MethodName[3]);
        if (pField->Rsvd == 0) {
            AmlAppend("0x%X,", pField->Bank);
        }
        else {
            AmlAppend("0x%X%08lX,", pField->Rsvd, pField->Bank);
        }
        pFieldUnit = (ACPI_FIELD_UNIT*)pNode->pNext->pUserContain;
        //
        FieldParameter(pFieldUnit);
        SetupTab();
        AmlAppend("{\n");
        m_nTab++;
        SubFieldUnit(pNode->pNext);
        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }
}

VOID
Field(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
Parse acpi Field Object

Arguments:

pNode - Acpi Data Name Space

Return Value:

--*/
{
    ACPI_NAMESPACE* pRoot;
    ACPI_NAMESPACE* pName;
    ACPI_FIELD_UNIT* pField;
    if (pNode != NULL) {
        pName = GetAcpiNsFromNsAddr((PVOID)(*(PVOID*)pNode->pUserContain));
        if (pName != NULL && pNode->Type == ACPI_TYPE_FIELD) {

            pField = (ACPI_FIELD_UNIT*)pNode->pNext->pUserContain;
            SetupTab();
            if (pName->pParent == pNode->pParent) {
                AmlAppend("Field(%C%C%C%C,", pName->MethodName[0], pName->MethodName[1],
                    pName->MethodName[2], pName->MethodName[3]);
            }
            else {
                CHAR chNamePath[256];
                CHAR chNameSeg[5];
                BOOL bMatch = FALSE;
                ULONG Level = 0;
                AmlAppend("Field(");
                pRoot = pName;
                chNameSeg[4] = 0;
                chNamePath[0] = 0;
                while (pRoot->pParent != NULL) {
                    PushName(pRoot->MethodNameAsUlong);
                    if (pRoot->pParent == pNode->pParent) {
                        bMatch = TRUE;
                        break;
                    }
                    Level++;
                    pRoot = pRoot->pParent;
                }
                if (!bMatch) {
                    AmlAppend("\\");
                }
                Level++;
                while (PopName(chNameSeg)) {
                    StringCbCatA(chNamePath, 256, chNameSeg);
                    Level--;
                    if (Level != 0) {
                        StringCbCatA(chNamePath, 256, ".");
                    }
                }
                AmlAppend("%s,", chNamePath);
                ResetName();
            }
            FieldParameter(pField);
            SetupTab();
            AmlAppend("{\n");
            m_nTab++;
            SubFieldUnit(pNode->pNext);
            m_nTab--;
            SetupTab();
            AmlAppend("}");
        }
        else if (pName != NULL && pNode->Type == ACPI_TYPE_INDEXFIELD) {
            if (pNode->Length != sizeof(UINTN*) * 2) {
                return;
            }
            SetupTab();
            ACPI_NAMESPACE* pData = GetAcpiNsFromNsAddr((ACPI_NAMESPACE*)(((UINTN*)pNode->pUserContain)[1]));

            if (pData != NULL && pNode->pNext->Type == ACPI_TYPE_FIELDUNIT) {
                pField = (ACPI_FIELD_UNIT*)pNode->pNext->pUserContain;
                AmlAppend("IndexField(%C%C%C%C", pName->MethodName[0], pName->MethodName[1],
                    pName->MethodName[2], pName->MethodName[3]);
                AmlAppend(",%C%C%C%C,", pData->MethodName[0], pData->MethodName[1],
                    pData->MethodName[2], pData->MethodName[3]);
                FieldParameter(pField);
                SetupTab();
                AmlAppend("{\n");
                m_nTab++;
                SubFieldUnit(pNode->pNext);
                m_nTab--;
                SetupTab();
                AmlAppend("}");
            }
        }
    }
}


VOID
BufField(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
Parse acpi BufField Object

Arguments:

pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    ACPI_BUF_FIELD* pField;
    ACPI_NAMESPACE* pName;
    pField = pNode->pUserContain;
    SetupTab();
    pName = GetAcpiNsFromObjData((PVOID)(*(PVOID*)pNode->pUserContain));

    //if (pName != NULL && pNode->Type == ACPI_TYPE_FIELDUNIT) {
    if (pField != NULL && pName != NULL) {
        if (pField->BitOffset == 0) {
            // Create byte, word, or dword, qword
            if (pField->NumOfBits % 64 == 0) {
                // a qword
                AmlAppend("CreateQWordField(%C%C%C%C, 0x%x", pName->MethodName[0],
                    pName->MethodName[1],
                    pName->MethodName[2],
                    pName->MethodName[3],
                    pField->Offset);
            }
            else if (pField->NumOfBits % 32 == 0) {
                // a dword
                AmlAppend("CreateDWordField(%C%C%C%C, 0x%x", pName->MethodName[0],
                    pName->MethodName[1],
                    pName->MethodName[2],
                    pName->MethodName[3],
                    pField->Offset);
            }
            else if (pField->NumOfBits % 16 == 0) {
                // a word
                AmlAppend("CreateWordField(%C%C%C%C, 0x%x", pName->MethodName[0],
                    pName->MethodName[1],
                    pName->MethodName[2],
                    pName->MethodName[3],
                    pField->Offset);
            }
            else if (pField->NumOfBits % 8 == 0) {
                // a byte 
                AmlAppend("CreateByteField(%C%C%C%C, 0x%x", pName->MethodName[0],
                    pName->MethodName[1],
                    pName->MethodName[2],
                    pName->MethodName[3],
                    pField->Offset);
            }
            else {
                assert(0);
            }
            AmlAppend(",%C%C%C%C)", pNode->MethodName[0],
                pNode->MethodName[1],
                pNode->MethodName[2],
                pNode->MethodName[3]);
        }
        else {
            // it's a bit create
            //assert(0);
            AmlAppend("CreateField(%C%C%C%C, 0x%x,  %d", pName->MethodName[0],
                pName->MethodName[1],
                pName->MethodName[2],
                pName->MethodName[3],
                pField->Offset + pField->BitOffset, pField->NumOfBits);
            AmlAppend(",%C%C%C%C)", pNode->MethodName[0],
                pNode->MethodName[1],
                pNode->MethodName[2],
                pNode->MethodName[3]);
        }
    }
}

VOID
FieldUnit(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
    Parse acpi FieldUnit Object

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    ACPI_NAMESPACE* pName;
    ACPI_FIELD_UNIT* pField;

    if (pNode != NULL) {

        pName = GetAcpiNsFromNsAddr((PVOID)(*(PVOID*)pNode->pUserContain));
        if (pName != NULL && pNode->Type == ACPI_TYPE_FIELDUNIT) {
            //char cLine[256];
            //pName = GetAcpiNsFromNsAddr((PVOID)(*(PVOID*)pName->pUserContain));
            //if (pName != NULL) {
            //    //access the operation information now..
            //    if (GetAcpiRootPath(pName, cLine)) {
            //        AmlAppend("Field Inherit: %s\n", cLine);
            //    }
            //    else {
            //        AmlAppend("Field Inherit: %C%C%C%C\n", pName->MethodName[0], pName->MethodName[1],
            //            pName->MethodName[2], pName->MethodName[3]);
            //    }
            //    //OpReg(pName);
            //    pField = pNode->pUserContain;
            //    // display the access method of this feild
            //    AmlAppend("\nAccess Information:\n");
            //    SetupTab();
            //    if ((pField->Type & 0xf) < 6) {
            //       
            //        AmlAppend("Access Type: %s\n", strFieldType[pField->Type&0xf]);
            //    }
            //    else {
            //        AmlAppend("Access Type: Unknown\n");
            //    }
            //    SetupTab();
            //    AmlAppend("Offset: 0x%X\n", pField->Offset);
            //    SetupTab();
            //    AmlAppend("BitOff: 0x%X\n", pField->BitOffset);
            //    SetupTab();
            //    AmlAppend("NumBit: 0x%X\n", pField->NumOfBits);
            //}
        }
        else if (pName != NULL && pNode->Type == ACPI_TYPE_FIELD) {
            // this is a field, now field point to the parent field
            //
            /*char cLine[256];
            if (GetAcpiRootPath(pName, cLine)) {
                AmlAppend("Field Inherit: %s\n", cLine);
            }
            else {
                AmlAppend("Field Inherit: %C%C%C%C\n", pName->MethodName[0], pName->MethodName[1],
                    pName->MethodName[2], pName->MethodName[3]);
            }
            OpReg(pName);*/
        }
        else if (pName != NULL && pNode->Type == ACPI_TYPE_INDEXFIELD) {
            if (pNode->Length != sizeof(UINTN*) * 2) {
                return;
            }
            SetupTab();
            ACPI_NAMESPACE* pData = GetAcpiNsFromNsAddr((ACPI_NAMESPACE*)(((UINTN*)pNode->pUserContain)[1]));

            if (pData != NULL && pNode->pNext->Type == ACPI_TYPE_FIELDUNIT) {
                pField = (ACPI_FIELD_UNIT*)pNode->pNext->pUserContain;
                AmlAppend("IndexFeild(%C%C%C%C", pName->MethodName[0], pName->MethodName[1],
                    pName->MethodName[2], pName->MethodName[3]);
                AmlAppend(",%C%C%C%C,", pData->MethodName[0], pData->MethodName[1],
                    pData->MethodName[2], pData->MethodName[3]);
                FieldParameter(pField);
                SetupTab();
                AmlAppend("{\n");
                m_nTab++;
                SubFieldUnit(pNode->pNext);
                m_nTab--;
                SetupTab();
                AmlAppend("}");
            }
        }
    }
}

VOID
Alias(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
    Parse acpi alias Object

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    ACPI_NAMESPACE* pName;
    ACPI_OBJ* pAcpiObj;
    ULARGE_INTEGER ul;
    SetupTab();

    ul.QuadPart = (ULONGLONG)pNode->pUserContain;
    pAcpiObj = (ACPI_OBJ*)pNode->pUserContain;
    ul.QuadPart = (ULONGLONG)pAcpiObj->ObjData.pnsAlias;
    pName = GetAcpiNsFromNsAddr(pAcpiObj->ObjData.pnsAlias);
    if (pName == NULL) {
        /*AmlAppend("Alias %X %X %08lX%08lX", pNode->Type, pNode->Length, ul.HighPart, ul.LowPart);*/
        AmlAppend("Alias(%C%C%C%C", pNode->MethodName[0], pNode->MethodName[1],
            pNode->MethodName[2], pNode->MethodName[3]);
    }
    else {
        AmlAppend("Alias(%C%C%C%C,", pNode->MethodName[0], pNode->MethodName[1],
            pNode->MethodName[2], pNode->MethodName[3]);
        AmlAppend("%C%C%C%C)", pName->MethodName[0], pName->MethodName[1],
            pName->MethodName[2], pName->MethodName[3]);

        /*char chFullPathName[256];
        if (GetAcpiRootPath(pName, chFullPathName)) {
            AmlAppend("Alias to %s", chFullPathName);
        }
        else {
            AmlAppend("Alias to %C%C%C%C", pName->MethodName[0], pName->MethodName[1],
                pName->MethodName[2], pName->MethodName[3]);
        }*/
    }

}

BOOL DataParser(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
    Verify the Tss data

Arguments:
    pNode	- Acpi Obj Node
    pArg	- Acpi Argument

Return Value:


--*/
{
    ULONG Index;

    if (pNode == NULL) {
        return FALSE;
    }

    // Save the current Parser Name space
    gAcpiNSActive = pNode;

    for (Index = 0; Index < sizeof(gAcpiDataParser) / sizeof(ACPI_DATA_PARSER); Index++)
    {
        if (pNode->Type == gAcpiDataParser[Index].id) {
            gAcpiDataParser[Index].Func(pNode);
            return TRUE;
        }
    }
    return FALSE;
}


void ArgParser(
    ACPI_NAMESPACE* pNode,
    PACPI_METHOD_ARGUMENT pArg)
    /*++

    Routine Description:
        Verify the Tss data

    Arguments:
        pNode	- Acpi Obj Node
        pArg	- Acpi Argument

    Return Value:


    --*/
{
    if (pArg->Type > ACPI_METHOD_ARGUMENT_PACKAGE_EX) {
        return;
    }
    gAcpiArgParser[pArg->Type].Func(pNode, pArg);
}


BOOLEAN
VerifyTss(
    ULONG uName,
    ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
)
/*++

Routine Description:
    Verify the Tss data

Arguments:

    pAcpiData - ACPI Eval output buffer

Return Value:
    TRUE    -- OK

    FALSE	-- Invalid TSS Buffer

--*/
{
    UNREFERENCED_PARAMETER(pAcpiData);
    UNREFERENCED_PARAMETER(uName);
    //
    // Number
    //
    PACPI_METHOD_ARGUMENT       arg;
    PACPI_METHOD_ARGUMENT       data;
    ULONG                       Idx, Idx2;

    arg = pAcpiData->Argument;
    for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
        if (arg->Type != ACPI_METHOD_ARGUMENT_PACKAGE) {
            return FALSE;
        }
        //
        // Verify number of DWORD/Integer Data
        //
        //if (arg->DataLength != 5 * sizeof (ACPI_RETURN_PACKAGE_DATA)) {
        //    return FALSE;
        //    }
        //
        // There is fix integer data, verify it
        //
        data = (PACPI_METHOD_ARGUMENT)arg->Data;

        for (Idx2 = 0; Idx2 < 5; Idx2++) {
            if (data->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
                return FALSE;
            }
            data = ACPI_METHOD_NEXT_ARGUMENT(data);
        }

        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    }

    arg = pAcpiData->Argument;
    for (Idx = 0; Idx < pAcpiData->Count; Idx++) {

        //
         // There is fix integer data, verify it
         //
        data = (PACPI_METHOD_ARGUMENT)arg->Data;

        AmlAppend("\n  Processor Performance State %d\n",
            Idx);
        AmlAppend("    CoreFreq          %dMhz\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Power             %dmW\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    TransitionLatency %dms\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    BusMasterLatency  %dms\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Control           %08x\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Status            %08x\n\n",
            data->Argument);

        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);

    }
    return TRUE;
}


BOOLEAN
VerifyCst(
    ULONG uName,
    ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
)
/*++

Routine Description:
    Verify the _CST data

Arguments:

    pAcpiData - ACPI Eval output buffer

Return Value:
    TRUE    -- OK

    FALSE	-- Invalid _CST Buffer

--*/
{
    UNREFERENCED_PARAMETER(pAcpiData);
    UNREFERENCED_PARAMETER(uName);// Number
    //
    PACPI_METHOD_ARGUMENT       arg;
    PACPI_METHOD_ARGUMENT       data;
    ULONG                       Idx;
    ULONG                       CstCnt;

    //__asm int 3;
    arg = pAcpiData->Argument;
    if (arg->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
        return FALSE;
    }
    CstCnt = (ULONG)arg->Argument + 1;

    if (CstCnt != pAcpiData->Count) {
        return FALSE;
    }

    AmlAppend("\n  C-State count %d\n",
        CstCnt - 1
    );

    arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    for (Idx = 0; Idx < CstCnt - 1; Idx++) {
        data = (PACPI_METHOD_ARGUMENT)arg->Data;

        //
        //if (!VerifyGenericRegisterBuffer (arg, NULL, FALSE)) {
        //    //return FALSE;
        //    }
        AmlAppend("\n  CState %d\n",
            Idx
        );

        VerifyGenericRegisterBuffer(data, "    Register", TRUE);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("\n    Type            C%d\n",
            data->Data[0]
        );
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Lantency        %dms\n",
            *((UINT16*)&data->Data[0])
        );
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Power           %dmv\n",
            data->Argument
        );
        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    }


    return TRUE;
}


BOOLEAN
VerifyPss(
    ULONG uName,
    ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
)
/*++

Routine Description:
    Verify the _PSS data

Arguments:

    pAcpiData - ACPI Eval output buffer

Return Value:
    TRUE    -- OK

    FALSE	-- Invalid _PSS Buffer

--*/
{
    //
     // Number
     //
    UNREFERENCED_PARAMETER(pAcpiData);
    UNREFERENCED_PARAMETER(uName);
    PACPI_METHOD_ARGUMENT   arg;
    PACPI_METHOD_ARGUMENT   data;
    ULONG                   Idx, Idx2;

    arg = pAcpiData->Argument;
    for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
        if (arg->Type != ACPI_METHOD_ARGUMENT_PACKAGE) {
            return FALSE;
        }
        //
        // Verify number of DWORD/Integer Data
        //
        //if (arg->DataLength != 6 * sizeof (ACPI_RETURN_PACKAGE_DATA)) {
        //    return FALSE;
        //    }
        //
        // There is fix integer data, verify it
        //
        data = (PACPI_METHOD_ARGUMENT)arg->Data;

        for (Idx2 = 0; Idx2 < 6; Idx2++) {
            if (data->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
                return FALSE;
            }
            data = ACPI_METHOD_NEXT_ARGUMENT(data);
        }

        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    }

    arg = pAcpiData->Argument;
    for (Idx = 0; Idx < pAcpiData->Count; Idx++) {

        //
        // There is fix integer data, verify it
        //
        data = (PACPI_METHOD_ARGUMENT)arg->Data;

        AmlAppend("\n  Processor Performance State %d\n",
            Idx);
        AmlAppend("    CoreFreq          %dMhz\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Power             %dmW\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    TransitionLatency %dms\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    BusMasterLatency  %dms\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Control           %08x\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Status            %08x\n\n",
            data->Argument);

        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    }
    return TRUE;
}


BOOLEAN
VerifyPtc(
    ULONG uName,
    ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
)
/*++

Routine Description:
    Verify the _PTC data

Arguments:

    pAcpiData - ACPI Eval output buffer

Return Value:
    TRUE    -- OK

    FALSE	-- Invalid _PTC Buffer

--*/
{
    UNREFERENCED_PARAMETER(pAcpiData);
    UNREFERENCED_PARAMETER(uName);
    //
    // Number
    //
    PACPI_METHOD_ARGUMENT       arg;
    //PACPI_RETURN_PACKAGE_DATA data;
    ULONG                       Idx;
    ULONG* pUlong;

    if (pAcpiData->Count != 2) {
        return FALSE;
    }
    arg = pAcpiData->Argument;

    for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
        if (arg->Type != ACPI_METHOD_ARGUMENT_BUFFER) {
            return FALSE;
        }
        //
        // Verify number of DWORD/Integer Data
        //
        if (arg->DataLength != 0x11) {
            return FALSE;
        }

        if (arg->Data[0] != 0x82) {
            return FALSE;
        }

        if (arg->Data[1] != 0x0C) {
            return FALSE;
        }

        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    }


    arg = pAcpiData->Argument;

    pUlong = (PULONG)&arg->Data[7];


    AmlAppend("  Control Register (%s, %d, %d",
        GetResouceType(arg->Data[3]),
        arg->Data[4],
        arg->Data[5]
    );
    if (pUlong[1] == 0) {
        AmlAppend(", %X)\n", pUlong[0]);
    }
    else {
        AmlAppend(", %08X%08X)\n", pUlong[1], pUlong[0]);
    }
    arg = ACPI_METHOD_NEXT_ARGUMENT(arg);

    pUlong = (PULONG)&arg->Data[7];
    AmlAppend("  Status Register  (%s, %d, %d",
        GetResouceType(arg->Data[3]),
        arg->Data[4],
        arg->Data[5]
    );
    if (pUlong[1] == 0) {
        AmlAppend(", %X)\n", pUlong[0]);
    }
    else {
        AmlAppend(", %08X%08X)\n", pUlong[1], pUlong[0]);
    }

    return TRUE;
}


BOOLEAN
VerifyXrs(
    ULONG uName,
    ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
)
/*++

Routine Description:
    Verify the _XRS data

Arguments:

    pAcpiData - ACPI Eval output buffer

Return Value:
    TRUE    -- OK

    FALSE	-- Invalid _XRS Buffer

--*/
{
    UNREFERENCED_PARAMETER(pAcpiData);
    UNREFERENCED_PARAMETER(uName);
    return FALSE;
}



BOOLEAN
VerifyXsd(
    ULONG uName,
    ACPI_EVAL_OUTPUT_BUFFER* pAcpiData
)
/*++

Routine Description:
    Verify the _XSD data

Arguments:

    pAcpiData - ACPI Eval output buffer

Return Value:
    TRUE    -- OK

    FALSE	-- Invalid _XSD Buffer

--*/
{
    UNREFERENCED_PARAMETER(pAcpiData);
    UNREFERENCED_PARAMETER(uName);
    //
    // Number
    //
    PACPI_METHOD_ARGUMENT       arg;
    PACPI_METHOD_ARGUMENT       data;
    ULONG                       Idx, Idx2;

    arg = pAcpiData->Argument;
    for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
        if (arg->Type != ACPI_METHOD_ARGUMENT_PACKAGE) {
            return FALSE;
        }
        //
        // Verify number of DWORD/Integer Data
        //
        //if (arg->DataLength != 5 * sizeof (ACPI_RETURN_PACKAGE_DATA)) {
        //    return FALSE;
         //   }
        //
        // There is fix integer data, verify it
        //
        data = (PACPI_METHOD_ARGUMENT)arg->Data;

        for (Idx2 = 0; Idx2 < 5; Idx2++) {
            if (data->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
                return FALSE;
            }
            data = ACPI_METHOD_NEXT_ARGUMENT(data);
        }

        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    }

    arg = pAcpiData->Argument;
    for (Idx = 0; Idx < pAcpiData->Count; Idx++) {

        //
        // There is fix integer data, verify it
        //
        data = (PACPI_METHOD_ARGUMENT)arg->Data;
        if (uName == ACPI_SIGNATURE('_', 'P', 'S', 'D')) {
            AmlAppend("\n  P-State Dependency %d\n",
                Idx);
        }
        else if (uName == ACPI_SIGNATURE('_', 'T', 'S', 'D')) {
            AmlAppend("\n  T-State Dependency %d\n",
                Idx);
        }
        else if (uName == ACPI_SIGNATURE('_', 'C', 'S', 'D')) {
            AmlAppend("\n  C-State Dependency %d\n",
                Idx);
        }
        else {
            AmlAppend("\n  x-State Dependency %d\n",
                Idx);
        }
        AmlAppend("    NumberOfEntries:   %d\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Revision:          %d\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    Domain:            %d\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    CoordType:         0x%X\n",
            data->Argument);
        data = ACPI_METHOD_NEXT_ARGUMENT(data);
        AmlAppend("    NumProcessors:     %d\n",
            data->Argument);

        arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
    }
    return TRUE;
}


VOID
AcpiOutputParser(
    ACPI_NAMESPACE* pNode,
    PACPI_EVAL_OUTPUT_BUFFER arg
)
/*++

Routine Description:
    Parse acpi output buffer

Arguments:

    arg - output data from EvalAcpiMethod

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    UNREFERENCED_PARAMETER(arg);
    ULONG					 nArgs;
    PACPI_EVAL_OUTPUT_BUFFER pLocalArg = arg;
    PACPI_METHOD_ARGUMENT	 pArg;
    nArgs = arg->Count;
    pArg = pLocalArg->Argument;
    while (nArgs) {
        ArgParser(pNode, pArg);
        nArgs--;
        if (nArgs == 0) {
            break;
        }
        AmlAppend(",\n");
        pArg = ACPI_METHOD_NEXT_ARGUMENT(pArg);
    }
}

VOID
PkgData(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
    Parse acpi Data buffer

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    // ULONG Index;
    PACKAGEOBJ* pPkgObj;
    ACPI_EVAL_OUTPUT_BUFFER* pLocalArg;
    pPkgObj = (PACKAGEOBJ*)pNode->pUserContain;
    if (pPkgObj == NULL) {
        return;
    }
    pLocalArg = (ACPI_EVAL_OUTPUT_BUFFER*)pNode->pUserContain;
    SetupTab();
    AmlAppend("Name (%C%C%C%C, Package(0x%lX)\n",
        pNode->MethodName[0], pNode->MethodName[1], pNode->MethodName[2], pNode->MethodName[3],
        pLocalArg->Count);
    SetupTab();
    AmlAppend("{\n");
    m_nTab++;
    // Parse all the values..
    AcpiOutputParser(pNode, pLocalArg);
    m_nTab--;
    //SetupTab();	
    AmlAppend("\n");
    SetupTab();
    AmlAppend("})");
    // Parse the local package

    /*SetupTab();
    AmlAppend("Package(%C%C%C%C, 0x%lX) {\n", pNode->MethodName[0], pNode->MethodName[1], pNode->MethodName[2], pNode->MethodName[3],
        pPkgObj->dwcElements);
    m_nTab++;
    SetupTab();

    for (Index = 0; Index < pPkgObj->dwcElements; Index++)
    {
        if (pPkgObj->adata[Index].dwDataType == ACPI_TYPE_INTEGER) {
            AmlAppend("0x%X", pPkgObj->adata[Index].dwDataValue);
            if (Index != pPkgObj->dwcElements - 1)
            {
                AmlAppend(", ");
            }
        }
        else if (pPkgObj->adata[Index].dwDataType == ACPI_TYPE_STRING) {
            AmlAppend("0x%X", pPkgObj->adata[Index].dwDataValue);
            if (Index != pPkgObj->dwcElements - 1)
            {
                AmlAppend(", ");
            }
        }
    }
    AmlAppend("\n");
    m_nTab--;
    SetupTab();

    AmlAppend("}");*/
}

VOID
BufData(
    ACPI_NAMESPACE* pAcpiNS
)
/*++

Routine Description:
    Parse acpi Data buffer

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pAcpiNS);
    PACPI_METHOD_ARGUMENT   pArg;
    if (pAcpiNS->pUserContain == NULL) {
        return;
    }

    pArg = malloc(sizeof(ACPI_METHOD_ARGUMENT) + pAcpiNS->Length);
    if (pArg != NULL) {
        pArg->Type = ACPI_METHOD_ARGUMENT_BUFFER;
        pArg->DataLength = (USHORT)pAcpiNS->Length;
        memcpy(pArg->Data, pAcpiNS->pUserContain, pAcpiNS->Length);
        ArgParser(pAcpiNS, pArg);
        free(pArg);
    }

}

VOID
StrData(
    ACPI_NAMESPACE* pAcpiNS
)
/*++

Routine Description:
    Parse acpi Data buffer

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pAcpiNS);
    if (pAcpiNS->pUserContain == NULL) {
        return;
    }
    SetupTab();
    AmlAppend("Name(%C%C%C%C,\"%s\")",
        pAcpiNS->MethodName[0], pAcpiNS->MethodName[1], pAcpiNS->MethodName[2], pAcpiNS->MethodName[3],
        (char*)pAcpiNS->pUserContain);
    /*AmlAppend("String:   %s",(char *)pNode->pUserContain);*/
}

VOID
IntData(
    ACPI_NAMESPACE* pNode
)
/*++

Routine Description:
    Parse acpi Data buffer

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    ACPI_OBJ* pAcpiObj;
    pAcpiObj = (ACPI_OBJ*)pNode->pUserContain;
    SetupTab();

    AmlAppend("Name(%C%C%C%C, 0x%X)",
        pAcpiObj->ucName[0], pAcpiObj->ucName[1], pAcpiObj->ucName[2], pAcpiObj->ucName[3],
        pAcpiObj->ObjData.dwDataValue);

    //AmlAppend("Interger:   0x%lX(%ld)",
    //    pAcpiObj->ObjData.dwDataValue,
    //    pAcpiObj->ObjData.dwDataValue);
}

VOID
IntArg(
    ACPI_NAMESPACE* pNode,
    PACPI_METHOD_ARGUMENT pArg
)
/*++

Routine Description:
Parse acpi output buffer

Arguments:

arg - output data from EvalAcpiMethod

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    UNREFERENCED_PARAMETER(pArg);
    ULONG* pUlong;
    pUlong = (ULONG*)&(pArg->Data[0]);
    SetupTab();
    if (pNode->Type == ACPI_TYPE_INTEGER) {
        if (pArg->DataLength >= 8 && pUlong[1] != 0) {
            AmlAppend("Name(%C%C%C%C,0x%X%08X)",
                pNode->MethodName[0], pNode->MethodName[1], pNode->MethodName[2], pNode->MethodName[3],
                pUlong[1],
                pUlong[0]
            );
        }
        else {
            AmlAppend("Name(%C%C%C%C,0x%x)",
                pNode->MethodName[0], pNode->MethodName[1], pNode->MethodName[2], pNode->MethodName[3],
                pUlong[0]
            );
        }

        /*if (pArg->DataLength >= 8 && pUlong[1] != 0) {
            AmlAppend("Integer\t:	0x%X%08X",
                pUlong[1],
                pUlong[0]
                );
        }
        else {
            AmlAppend("Integer\t:	0x%X (%d)",
                pArg->Argument, pArg->Argument
                );
        }*/
    }
    else {
        //	SetupTab();
        if (pArg->DataLength >= 8 && pUlong[1] != 0) {
            AmlAppend("0x%X%08X",
                pUlong[1],
                pUlong[0]
            );
        }
        else {
            AmlAppend("0x%X",
                pArg->Argument
            );
        }
    }
}

VOID
StrArg(
    ACPI_NAMESPACE* pNode,
    PACPI_METHOD_ARGUMENT pArg
)
/*++

Routine Description:
Parse acpi output buffer

Arguments:

arg - output data from EvalAcpiMethod

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    UNREFERENCED_PARAMETER(pArg);
    SetupTab();
    if (pNode->Type == ACPI_TYPE_STRING) {
        AmlAppend("Name(%C%C%C%C, \"%s\")",
            pNode->MethodName[0], pNode->MethodName[1], pNode->MethodName[2], pNode->MethodName[3],
            pArg->Data
        );
        /*AmlAppend("String\t: \"%s\"",
            pArg->Data);*/
    }
    else {
        AmlAppend("\"%s\"",
            pArg->Data);
    }
}

VOID
BufArg(
    ACPI_NAMESPACE* pNode,
    PACPI_METHOD_ARGUMENT pArg
)
/*++

Routine Description:
Parse acpi output buffer

Arguments:

arg - output data from EvalAcpiMethod

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    UNREFERENCED_PARAMETER(pArg);
    ULONG Index;
    USHORT Length;
    SetupTab();
    if (pNode->Type == ACPI_TYPE_BUFFER) {
        AmlAppend("Name(%C%C%C%C, ",
            pNode->MethodName[0], pNode->MethodName[1], pNode->MethodName[2], pNode->MethodName[3]
        );
    }
    AmlAppend("Buffer (0x%x) {\n    ", pArg->DataLength);
    m_nTab++;


    for (Index = 0; Index < pArg->DataLength; Index++) {
        if (Index > 0 && Index % 8 == 0) {
            PrintAscIIInComments((char*)&pArg->Data[Index - 8], 8);
            AmlAppend("\n");
            SetupTab();
        }
        else if (Index == 0) {
            m_nTab--;
            SetupTab();
            m_nTab++;
        }
        if (pArg->DataLength > 8 && Index % 8 == 0) {
            if (Index + 8 > pArg->DataLength)
                AmlAppend("/*%4d - %4d*/  ", Index + 1, pArg->DataLength);
            else
                AmlAppend("/*%4d - %4d*/  ", Index + 1, Index + 8);
        }
        if (Index == (UINT)(pArg->DataLength - 1)) {
            AmlAppend("0x%02X", pArg->Data[Index]);
        }
        else {
            AmlAppend("0x%02X, ", pArg->Data[Index]);
        }
    }
    if (pArg->DataLength != 0)

    {
        Length = pArg->DataLength % 8;
        if (Length == 0)
        {
            Length = 8;
        }
        AmlAppend("  ");
        PrintAscIIInComments((char*)&pArg->Data[pArg->DataLength - Length], Length);
        //AmlAppend("\n");
        SetupTab();
    }
    if (pArg->DataLength > 8) {
        AmlAppend("\n");
        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }
    else {
        AmlAppend("\n");
        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }
    if (pNode->Type == ACPI_TYPE_BUFFER) {
        // TODO		
        AmlAppend(")");
    }
}

VOID
PkgArg(
    ACPI_NAMESPACE* pNode,
    PACPI_METHOD_ARGUMENT pArg
)
/*++

Routine Description:
Parse acpi output buffer

Arguments:

arg - output data from EvalAcpiMethod

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    UNREFERENCED_PARAMETER(pArg);
    //AmlAppend("Package Size\t: %d",
    //	pArg->DataLength);
    PACPI_METHOD_ARGUMENT LocalArg;
    LPBYTE                lpStart;
    LPBYTE                lpEnd;
    ULONG				  ArgNum;

    lpStart = (LPBYTE)pArg->Data;
    lpEnd = lpStart + pArg->DataLength;
    ArgNum = 0;
    while (lpEnd > lpStart) {
        LocalArg = (PACPI_METHOD_ARGUMENT)lpStart;
        LocalArg = ACPI_METHOD_NEXT_ARGUMENT(LocalArg);
        lpStart = (LPBYTE)LocalArg;
        ArgNum++;
    }
    SetupTab();
    AmlAppend("Package (%d) {\n", ArgNum);
    m_nTab++;
    lpStart = (LPBYTE)pArg->Data;
    while (lpEnd > lpStart) {
        LocalArg = (PACPI_METHOD_ARGUMENT)lpStart;
        ArgParser(pNode, LocalArg);
        LocalArg = ACPI_METHOD_NEXT_ARGUMENT(LocalArg);
        lpStart = (LPBYTE)LocalArg;
        if (lpEnd > lpStart) {
            AmlAppend(", \n");
        }
    }
    m_nTab--;
    AmlAppend("\n");
    SetupTab();
    AmlAppend("}");
}

VOID
PkgExArg(
    ACPI_NAMESPACE* pNode,
    PACPI_METHOD_ARGUMENT pArg
)
/*++

Routine Description:
Parse acpi output buffer

Arguments:

arg - output data from EvalAcpiMethod

Return Value:

--*/
{
    UNREFERENCED_PARAMETER(pNode);
    UNREFERENCED_PARAMETER(pArg);
    AmlAppend("PackageEx Size\t: %d",
        pArg->DataLength);
}

VOID
OpReg(
    ACPI_NAMESPACE* pAcpiNS
)
/*++

Routine Description:
    Parse acpi Operation Region

Arguments:

    pAcpiNS - Acpi Data Name Space

Return Value:

--*/
{
    ULARGE_INTEGER ul;

    //ULONG ByteOffset = 0;
    if (pAcpiNS->pUserContain != NULL) {
        SetupTab();
        PACPI_OPERATION_REGION pAcpiRegion = (PACPI_OPERATION_REGION)pAcpiNS->pUserContain;
        AmlAppend("OperationRegion(%C%C%C%C,",
            pAcpiNS->MethodName[0], pAcpiNS->MethodName[1], pAcpiNS->MethodName[2], pAcpiNS->MethodName[3]
        );
        if (pAcpiRegion->Type <= OpRegionTypeLen) {
            AmlAppend(OpRegionType[pAcpiRegion->Type]);
        }
        else {
            AmlAppend("OemDefined");
        }
        ul.QuadPart = (ULONGLONG)pAcpiRegion->Addr;
        if (ul.HighPart == 0) {
            AmlAppend(",0x%lX,0x%lX)", ul.LowPart, pAcpiRegion->Length);
        }
        else {
            AmlAppend(",0x%lX%08lX,0x%lX)", ul.HighPart, ul.LowPart, pAcpiRegion->Length);
        }
        /*AcpiOperationRegion(pAcpiRegion);*/
#if 0	// TODO: Nessary to handle field in operation region?
        {
            ACPI_NAMESPACE* pFileds;
            pFileds = pAcpiNS->pNext;
            if (pFileds->MethodNameAsUlong == 0 && pFileds->Type == ACPI_TYPE_FIELD) {
                // correct sequency, now do the parsing for all fields
                ACPI_FIELD_UNIT* pField;
                pFileds = pFileds->pNext;	// check the type of accessing
                pField = pFileds->pUserContain;
                if (pFileds->Type != ACPI_TYPE_FIELDUNIT) {
                    return;
                }
                // what kind type of access method
                AmlAppend("\n");
                SetupTab();
                AmlAppend("Feild(%C%C%C%C,", pAcpiNS->MethodName[0], pAcpiNS->MethodName[1], pAcpiNS->MethodName[2], pAcpiNS->MethodName[3]
                );
                FieldParameter(pField);
                SetupTab();
                AmlAppend("{\n");
                m_nTab++;
                SubFieldUnit(pFileds);
                m_nTab--;
                SetupTab();
                AmlAppend("}");
            }
        }
#endif
    }
}


VOID
Mutex(
    ACPI_NAMESPACE* pAcpiNS
)
/*++

Routine Description:
    Parse acpi Mutex Object

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    ULONG* dwSyncLevel = (ULONG*)pAcpiNS->pUserContain;
    SetupTab();
    AmlAppend("Mutex(%C%C%C%C,0x%X)",
        pAcpiNS->MethodName[0], pAcpiNS->MethodName[1], pAcpiNS->MethodName[2], pAcpiNS->MethodName[3],
        *dwSyncLevel);
    /*ULONG   *dwSyncLevel = (ULONG * )pNode->pUserContain;
    if (pNode->TypeExt == 2) {
        AmlAppend("Global Mutex\n");
    }
    else {
        AmlAppend("Mutex\n");
    }
    SetupTab();
    AmlAppend("SyncLevel: %d \n", *dwSyncLevel);*/
}


VOID AcpiOperationRegion(
    PACPI_OPERATION_REGION pAcpiRegion
)
/*++

Routine Description:
    Parse acpi Data buffer

Arguments:

    pNode - Acpi Data Name Space

Return Value:

--*/
{
    ULARGE_INTEGER ul;
    ul.QuadPart = (ULONGLONG)pAcpiRegion->Addr;
    AmlAppend("Operation Region:\n");
    SetupTab();
    AmlAppend("Address\t= 0x%X%08X\n", ul.HighPart, ul.LowPart);
    SetupTab();
    AmlAppend("Length\t= 0x%X\n", pAcpiRegion->Length);
    SetupTab();
    AmlAppend("Type\t= 0x%X (", pAcpiRegion->Type);

    if (pAcpiRegion->Type <= 7) {
        AmlAppend(OpRegionType[pAcpiRegion->Type]);
    }
    else {
        AmlAppend("User Defined");
    }
    AmlAppend(")");
}
//
//
//VOID
//ParseReturnData(HWND hEdit, PACPI_NAMESPACE pAcpiName, PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
//{
//    UNREFERENCED_PARAMETER(hEdit);
//    PACPI_METHOD_ARGUMENT arg;
//    UINT                  Idx;
//    if (pAcpiData == NULL) {
//        AmlAppend("Method run succssully");
//        AppendTextWin();
//        return;
//    }
//
//    Idx = sizeof(ULONG);
//    switch (pAcpiName->MethodNameAsUlong) {
//    case ACPI_SIGNATURE('_', 'P', 'S', 'S'):
//        //
//        // How many package's
//        //
//        if (VerifyValidPssReturnData(pAcpiData)) {
//            break;
//        }
//        else {
//            //assert(0);
//            goto NormalResult;
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'T', 'S', 'S'):
//        //
//        // How many package's
//        //
//        if (VerifyValidTssReturnData(pAcpiData)) {
//            break;
//        }
//        else {
//            assert(0);
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'P', 'T', 'C'):
//        //
//        // How many package's
//        //
//        AmlAppend("\n\n  Processor Throttling Control\n");
//        if (VerifyValidPtcReturnData(pAcpiData)) {
//            break;
//        }
//        else {
//            assert(0);
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'P', 'C', 'T'):
//        //
//        // How many package's
//        //
//        AmlAppend("\n\n  Performance Control\n");
//        if (VerifyValidPtcReturnData(pAcpiData)) {
//            break;
//        }
//        else {
//            assert(0);
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'P', 'S', 'D'):
//        //
//        // How many package's
//        //
//        if (VerifyValidXsdReturnData(pAcpiData, 0)) {
//            break;
//        }
//        else {
//            assert(0);
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'T', 'S', 'D'):
//        //
//        // How many package's
//        //
//        if (VerifyValidXsdReturnData(pAcpiData, 1)) {
//            break;
//        }
//        else {
//            assert(0);
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'C', 'S', 'D'):
//        //
//        // How many package's
//        //
//        if (VerifyValidXsdReturnData(pAcpiData, 2)) {
//            break;
//        }
//        else {
//            assert(0);
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'S', '0', '_'):
//    case ACPI_SIGNATURE('_', 'S', '1', '_'):
//    case ACPI_SIGNATURE('_', 'S', '2', '_'):
//    case ACPI_SIGNATURE('_', 'S', '3', '_'):
//    case ACPI_SIGNATURE('_', 'S', '4', '_'):
//    case ACPI_SIGNATURE('_', 'S', '5', '_'):
//        break;
//    case ACPI_SIGNATURE('_', 'C', 'S', 'T'):
//        //
//        // How many package's
//        //
//        if (VerifyValidCstReturnData(pAcpiData)) {
//            break;
//        }
//        else {
//            assert(0);
//        }
//        break;
//    case ACPI_SIGNATURE('_', 'C', 'R', 'S'):
//    case ACPI_SIGNATURE('_', 'P', 'R', 'S'):
//        //
//        // Must one buffer
//        //
//        arg = pAcpiData->Argument;
//        if (pAcpiData->Count != 1) {
//            assert(0);
//        }
//        else {
//            //AmlAppend("\n Resource Template: \n");
//            AmlAppend("\n");
//            //
//            // Parse the resource template
//            //
//            ParseResourceTemplateData(pAcpiData->Argument[0].Data, pAcpiData->Argument[0].DataLength);
//        }
//        break;
//    default:
//    NormalResult:
//        arg = pAcpiData->Argument;
//        if (pAcpiData->Count == 1) {
//            switch (arg->Type) {
//            case ACPI_METHOD_ARGUMENT_INTEGER:
//                AmlAppend("Integer: ");
//                ParseIntegerReturnData(arg);
//                break;
//            case ACPI_METHOD_ARGUMENT_STRING:
//                AmlAppend("String: ");
//                ParseStringReturnData(arg);
//                break;
//            case ACPI_METHOD_ARGUMENT_BUFFER:
//                AmlAppend("");
//                ParseBufferReturnData(arg);
//                break;
//            case ACPI_METHOD_ARGUMENT_PACKAGE:
//                m_nTab = 0;
//                ParsePackageReturnData(arg);
//                break;
//            default:
//                assert(0);
//            }
//        }
//        else {
//            m_nTab = 0;
//            AmlAppend("Package (%d) {\n", pAcpiData->Count);
//            m_nTab++;
//            for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
//                switch (arg->Type) {
//                case ACPI_METHOD_ARGUMENT_INTEGER:
//                    ParseIntegerReturnData(arg);
//                    break;
//                case ACPI_METHOD_ARGUMENT_STRING:
//                    ParseStringReturnData(arg);
//                    break;
//                case ACPI_METHOD_ARGUMENT_BUFFER:
//                    ParseBufferReturnData(arg);
//                    break;
//                case ACPI_METHOD_ARGUMENT_PACKAGE:
//                    ParsePackageReturnData(arg);
//                    break;
//                default:
//                    assert(0);
//                    break;
//                }
//                if (Idx != pAcpiData->Count - 1) {
//                    AmlAppend(", \n");
//                }
//                arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
//            }
//            m_nTab--;
//            AmlAppend("\n}");
//        }
//
//        break;
//    }
//    //AppendTextWin();
//}


VOID
ParseIntegerReturnData(PACPI_METHOD_ARGUMENT arg)
{
    ULONG* pUlong;

    pUlong = (ULONG*)&(arg->Data[0]);
    SetupTab();
    if (arg->DataLength >= 8 && pUlong[1] != 0) {
        AmlAppend("0x%X%08X",
            pUlong[1],
            pUlong[0]
        );
    }
    else {
        AmlAppend("0x%X (%d)",
            arg->Argument, arg->Argument
        );
    }

}

VOID
ParseStringReturnData(PACPI_METHOD_ARGUMENT arg)
{
    SetupTab();
    AmlAppend("\"%s\"", arg->Data);
}

VOID
ParseBufferReturnData(PACPI_METHOD_ARGUMENT arg)
{
    UINT Idx2;
    UINT Length;
    SetupTab();
    if (arg->DataLength > 8) {
        AmlAppend("Buffer (%d) {\n    ", arg->DataLength);
        m_nTab++;
    }
    else {
        AmlAppend("Buffer (%d) {", arg->DataLength);
    }

    for (Idx2 = 0; Idx2 < arg->DataLength; Idx2++) {
        if (Idx2 > 0 && Idx2 % 8 == 0) {
            PrintAscIIInComments((char*)&arg->Data[Idx2 - 8], 8);
            AmlAppend("\n");
            SetupTab();
        }
        else if (Idx2 == 0) {
            m_nTab--;
            SetupTab();
            m_nTab++;
        }
        if (arg->DataLength > 8 && Idx2 % 8 == 0) {
            if (Idx2 + 8 > arg->DataLength)
                AmlAppend(" /*%4d - %4d*/  ", Idx2 + 1, arg->DataLength);
            else
                AmlAppend(" /*%4d - %4d*/  ", Idx2 + 1, Idx2 + 8);
        }
        if (Idx2 == (UINT)(arg->DataLength - 1)) {
            AmlAppend("0x%02X", arg->Data[Idx2]);
        }
        else {
            AmlAppend("0x%02X, ", arg->Data[Idx2]);
        }
    }
    if (arg->DataLength != 0)

    {
        Length = arg->DataLength % 8;
        if (Length == 0)
        {
            Length = 8;
        }
        AmlAppend("  ");
        PrintAscIIInComments((char*)&arg->Data[arg->DataLength - Length], Length);
        AmlAppend("\n");
        SetupTab();
    }
    if (arg->DataLength > 8) {
        AmlAppend("\n");
        m_nTab--;
        SetupTab();
        AmlAppend("}");
    }
    else {
        AmlAppend("}");
    }

}


VOID
ParsePackageReturnData(PACPI_METHOD_ARGUMENT arg)
{
    PACPI_METHOD_ARGUMENT LocalArg;
    LPBYTE                lpStart;
    LPBYTE                lpEnd;
    UINT                  ArgNum;
    //__asm int 3;

    lpStart = (LPBYTE)arg->Data;
    lpEnd = lpStart + arg->DataLength;
    ArgNum = 0;
    while (lpEnd > lpStart) {
        LocalArg = (PACPI_METHOD_ARGUMENT)lpStart;
        LocalArg = ACPI_METHOD_NEXT_ARGUMENT(LocalArg);
        lpStart = (LPBYTE)LocalArg;
        ArgNum++;
    }



    SetupTab();
    AmlAppend("Package (%d) {\n", ArgNum);
    m_nTab++;
    lpStart = (LPBYTE)arg->Data;
    while (lpEnd > lpStart) {
        LocalArg = (PACPI_METHOD_ARGUMENT)lpStart;
        switch (LocalArg->Type) {
        case ACPI_METHOD_ARGUMENT_INTEGER:
            ParseIntegerReturnData(LocalArg);
            break;
        case ACPI_METHOD_ARGUMENT_STRING:
            ParseStringReturnData(LocalArg);
            break;
        case ACPI_METHOD_ARGUMENT_BUFFER:
            ParseBufferReturnData(LocalArg);
            break;
        case ACPI_METHOD_ARGUMENT_PACKAGE:
            ParsePackageReturnData(LocalArg);
            break;
        default:
            assert(0);
        }
        LocalArg = ACPI_METHOD_NEXT_ARGUMENT(LocalArg);
        lpStart = (LPBYTE)LocalArg;
        if (lpEnd > lpStart) {
            AmlAppend(", \n");
        }
    }
    m_nTab--;
    AmlAppend("\n");
    SetupTab();
    AmlAppend("}");

}

VOID
ParseReturnData(PACPI_NAMESPACE pAcpiName, PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
    PACPI_METHOD_ARGUMENT arg;
    UINT                  Idx;
    if (pAcpiData == NULL) {
        AmlAppend("Method run succssully");
        return;
    }
	if (OutputDataParser(pAcpiName->MethodNameAsUlong, pAcpiData)) {
		return;
	}
    arg = pAcpiData->Argument;
    if (pAcpiData->Count == 1) {
        switch (arg->Type) {
        case ACPI_METHOD_ARGUMENT_INTEGER:
            AmlAppend("Integer: ");
            ParseIntegerReturnData(arg);
            break;
        case ACPI_METHOD_ARGUMENT_STRING:
            AmlAppend("String: ");
            ParseStringReturnData(arg);
            break;
        case ACPI_METHOD_ARGUMENT_BUFFER:
            AmlAppend("");
            ParseBufferReturnData(arg);
            break;
        case ACPI_METHOD_ARGUMENT_PACKAGE:
            m_nTab = 0;
            ParsePackageReturnData(arg);
            break;
        default:
            assert(0);
        }
    }
    else {
        m_nTab = 0;
        AmlAppend("Package (%d) {\n", pAcpiData->Count);
        m_nTab++;
        for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
            switch (arg->Type) {
            case ACPI_METHOD_ARGUMENT_INTEGER:
                ParseIntegerReturnData(arg);
                break;
            case ACPI_METHOD_ARGUMENT_STRING:
                ParseStringReturnData(arg);
                break;
            case ACPI_METHOD_ARGUMENT_BUFFER:
                ParseBufferReturnData(arg);
                break;
            case ACPI_METHOD_ARGUMENT_PACKAGE:
                ParsePackageReturnData(arg);
                break;
            default:
                assert(0);
                break;
            }
            if (Idx != pAcpiData->Count - 1) {
                AmlAppend(", \n");
            }
            arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
        }
        m_nTab--;
        AmlAppend("\n}");
    }
}

BOOL
OutputDataParser(UINT32 NameSeg, PACPI_EVAL_OUTPUT_BUFFER pAcpiData) {
	for (int iIndex = 0; iIndex < gDataParserLength; iIndex++) {
		if (gDataParser[iIndex].uSig == NameSeg) {
			gDataParser[iIndex].AmlHandler(pAcpiData);
			return TRUE;
		}
	}
	return FALSE;
}

BOOL
VerifyValidPssReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	PACPI_METHOD_ARGUMENT       arg;
	PACPI_METHOD_ARGUMENT       data;
	UINT                        Idx, Idx2;

	arg = pAcpiData->Argument;
	for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
		if (arg->Type != ACPI_METHOD_ARGUMENT_PACKAGE) {
			return FALSE;
		}
		//
		// Verify number of DWORD/Integer Data
		//
		data = (PACPI_METHOD_ARGUMENT)arg->Data;

		for (Idx2 = 0; Idx2 < 6; Idx2++) {
			if (data->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
				return FALSE;
			}
			data = ACPI_METHOD_NEXT_ARGUMENT(data);
		}

		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}

	arg = pAcpiData->Argument;
	for (Idx = 0; Idx < pAcpiData->Count; Idx++) {

		//
		// There is fix integer data, verify it
		//
		data = (PACPI_METHOD_ARGUMENT)arg->Data;

		AmlAppend("\n  Processor Performance State %d\n",
			Idx);
		AmlAppend("    CoreFreq          %dMhz\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Power             %dmW\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    TransitionLatency %dms\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    BusMasterLatency  %dms\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Control           %08x\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Status            %08x\n\n",
			data->Argument);

		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}
	return TRUE;

}

BOOL
VerifyValidPsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	return VerifyValidXsdReturnData(pAcpiData, 0);
}
BOOL
VerifyValidTsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	return VerifyValidXsdReturnData(pAcpiData, 1);
}
BOOL
VerifyValidCsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	return VerifyValidXsdReturnData(pAcpiData, 2);
}
BOOL
VerifyValidUsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	return VerifyValidXsdReturnData(pAcpiData, 3);
}
BOOL
VerifyValidXsdReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData, UINT Type)
{
	//
	// Number
	//
	PACPI_METHOD_ARGUMENT       arg;
	PACPI_METHOD_ARGUMENT       data;
	UINT                        Idx, Idx2;

	arg = pAcpiData->Argument;
	for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
		if (arg->Type != ACPI_METHOD_ARGUMENT_PACKAGE) {
			return FALSE;
		}
		//
		// Verify number of DWORD/Integer Data
		//
		//if (arg->DataLength != 5 * sizeof (ACPI_RETURN_PACKAGE_DATA)) {
		//    return FALSE;
		 //   }
		//
		// There is fix integer data, verify it
		//
		data = (PACPI_METHOD_ARGUMENT)arg->Data;

		for (Idx2 = 0; Idx2 < 5; Idx2++) {
			if (data->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
				return FALSE;
			}
			data = ACPI_METHOD_NEXT_ARGUMENT(data);
		}

		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}

	arg = pAcpiData->Argument;
	for (Idx = 0; Idx < pAcpiData->Count; Idx++) {

		//
		// There is fix integer data, verify it
		//
		data = (PACPI_METHOD_ARGUMENT)arg->Data;
		if (Type == 0)
			AmlAppend("\n  P-State Dependency %d\n",
				Idx);
		else if (Type == 1)
			AmlAppend("\n  T-State Dependency %d\n",
				Idx);
		else if (Type == 2)
			AmlAppend("\n  C-State Dependency %d\n",
				Idx);
		else
			AmlAppend("\n  x-State Dependency %d\n",
				Idx);
		AmlAppend("    NumberOfEntries:   %d\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Revision:          %d\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Domain:            %d\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    CoordType:         0x%X\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    NumProcessors:     %d\n",
			data->Argument);

		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}
	return TRUE;

}

BOOL
VerifyValidTssReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	//
	// Number
	//
	PACPI_METHOD_ARGUMENT       arg;
	PACPI_METHOD_ARGUMENT       data;
	UINT                        Idx, Idx2;
	//__asm int 3;
	arg = pAcpiData->Argument;
	for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
		if (arg->Type != ACPI_METHOD_ARGUMENT_PACKAGE) {
			return FALSE;
		}
		//
		// Verify number of DWORD/Integer Data
		//
		//if (arg->DataLength != 5 * sizeof (ACPI_RETURN_PACKAGE_DATA)) {
		//    return FALSE;
		//    }
		//
		// There is fix integer data, verify it
		//
		data = (PACPI_METHOD_ARGUMENT)arg->Data;

		for (Idx2 = 0; Idx2 < 5; Idx2++) {
			if (data->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
				return FALSE;
			}
			data = ACPI_METHOD_NEXT_ARGUMENT(data);
		}

		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}

	arg = pAcpiData->Argument;
	for (Idx = 0; Idx < pAcpiData->Count; Idx++) {

		//
		// There is fix integer data, verify it
		//
		data = (PACPI_METHOD_ARGUMENT)arg->Data;
		AmlAppend("\n  Throttling Supported State %d\n",
			Idx);
		AmlAppend("    FreqPercentageOfMaximum  %d%c\n",
			data->Argument, '%');
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Power                    %dmW\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    TransitionLatency        %dms\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Control                  %08x\n",
			data->Argument);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Status                   %08x\n\n",
			data->Argument);


		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}
	return TRUE;

}

BOOL
VerifyValidPtcReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	//
	// Number
	//
	PACPI_METHOD_ARGUMENT       arg;
	//PACPI_RETURN_PACKAGE_DATA   data;
	UINT                        Idx;
	ULONG* pUlong;

	if (pAcpiData->Count != 2) {
		return FALSE;
	}
	arg = pAcpiData->Argument;

	for (Idx = 0; Idx < pAcpiData->Count; Idx++) {
		if (arg->Type != ACPI_METHOD_ARGUMENT_BUFFER) {
			return FALSE;
		}
		//
		// Verify number of DWORD/Integer Data
		//
		if (arg->DataLength != 0x11) {
			return FALSE;
		}

		if (arg->Data[0] != 0x82) {
			return FALSE;
		}

		if (arg->Data[1] != 0x0C) {
			return FALSE;
		}

		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}


	arg = pAcpiData->Argument;

	pUlong = (PULONG)&arg->Data[7];


	AmlAppend("  Control Register (%s, %d, %d",
		GetResouceType(arg->Data[3]),
		arg->Data[4],
		arg->Data[5]
	);
	if (pUlong[1] == 0) {
		AmlAppend(", %X)\n", pUlong[0]);
	}
	else {
		AmlAppend(", %08X%08X)\n", pUlong[1], pUlong[0]);
	}
	arg = ACPI_METHOD_NEXT_ARGUMENT(arg);

	pUlong = (PULONG)&arg->Data[7];
	AmlAppend("  Status Register  (%s, %d, %d",
		GetResouceType(arg->Data[3]),
		arg->Data[4],
		arg->Data[5]
	);
	if (pUlong[1] == 0) {
		AmlAppend(", %X)\n", pUlong[0]);
	}
	else {
		AmlAppend(", %08X%08X)\n", pUlong[1], pUlong[0]);
	}

	return TRUE;
}



UINT8
ResourceType(BYTE Tag)
{
	if (BIT_MASK_VALUE(Tag, 0x80)) {
		return LARGE_RESOURCE_TYPE;
	}
	else {
		return SMALL_RESOURCE_TYPE;
	}
}
//GpioInt(Edge, ActiveHigh, Exclusive, PullDown, , " \\_SB.GPI2") { 14 }
////Power Button
//GpioInt(Edge, ActiveLow, ExclusiveAndWake, PullUp, , " \\_SB.GPI2") { 36 }
//})
VOID
ParseLargeExtIrqResource(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	USHORT Length = pHeader->Length;
	
	SetupOffset();
	AmlAppend("ExtendedInterrupt (");
	
	if (BIT_MASK_VALUE(pHeader->Data[0], 1)) {
		AmlAppend("Edge, ");
	}
	else {
		AmlAppend("Level, ");
	}
	if (BIT_MASK_VALUE(pHeader->Data[0], 2)) {
		AmlAppend("Active Low, ");
	}
	else {
		AmlAppend("Active High, ");
	}
	if (BIT_MASK_VALUE(pHeader->Data[0], 3)) {
		AmlAppend("Shared");
	}
	else {
		AmlAppend("Exclusive");
	}
	if (BIT_MASK_VALUE(pHeader->Data[0], 4)) {
		AmlAppend("AndWake,");
	}
	else {
		AmlAppend(",");
	}
	if (BIT_MASK_VALUE(pHeader->Data[0], 1)) {
		AmlAppend("Consumer) {\n");
	}
	else {
		AmlAppend("Producer) {\n");
	}
	gOffset++;
	UINT32* pIrqNumber;
	pIrqNumber = (UINT32*)&pHeader->Data[2];
	for (UINT8 uIndex = 0; uIndex < pHeader->Data[1]; uIndex++) {
		SetupOffset();
		if (uIndex < pHeader->Data[1] - 1) {
			AmlAppend("0x%08X,\n", pIrqNumber[uIndex]);
		}
		else {
			AmlAppend("0x%08X\n", pIrqNumber[uIndex]);
		}
	}
	gOffset--;
	SetupOffset();
	AmlAppend("}\n");
}
VOID
ParseSmallIrq(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	BYTE    Idx;
	WORD    IrqMap;
	BOOL    bFirst = TRUE;
	//pHeader    
	memcpy(&IrqMap, pHeader->Data, sizeof(WORD));
	if (pHeader->Length == 3) {
		SetupOffset();
		AmlAppend("IRQ (");
		if (BIT_MASK_VALUE(pHeader->Data[2], 1)) {
			AmlAppend("Edge, ");
		}
		else {
			AmlAppend("Level, ");
		}
		if (BIT_MASK_VALUE(pHeader->Data[2], 8)) {
			AmlAppend("ActiveLow, ");
		}
		else {
			AmlAppend("ActiveHigh, ");
		}
		if (BIT_MASK_VALUE(pHeader->Data[2], 0x10)) {
			AmlAppend("Shared) {");
		}
		else {
			AmlAppend("Exclusive) {");
		}
		for (Idx = 0; Idx < 16; Idx++) {
			if (((IrqMap >> Idx) & 1) == 1) {
				if (bFirst) {
					AmlAppend("%d", Idx);
					bFirst = FALSE;
				}
				else {
					AmlAppend(" ,%d", Idx);
				}
			}
		}
		AmlAppend("}\n");
	}
	else {
		SetupOffset();
		AmlAppend("IRQNoFlags  () {");
		for (Idx = 0; Idx < 16; Idx++) {
			if (((IrqMap >> Idx) & 1) == 1) {
				if (bFirst) {
					AmlAppend("%d", Idx);
					bFirst = FALSE;
				}
				else {
					AmlAppend(" ,%d", Idx);
				}
			}
		}
		AmlAppend("}\n");
	}
}

VOID
ParseSmallDma(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	BYTE    Idx;
	BOOL    bFirst = TRUE;
	SetupOffset();
	AmlAppend("DMA (");
	if (MASK_VALUE(pHeader->Data[1], 0x60, 0x0)) {
		AmlAppend("Compatible, ");
	}
	else if (MASK_VALUE(pHeader->Data[1], 0x60, 0x20)) {
		AmlAppend("TypeA, ");
	}
	else if (MASK_VALUE(pHeader->Data[1], 0x60, 0x40)) {
		AmlAppend("TypeB, ");
	}
	else {
		AmlAppend("TypeF, ");
	}
	if (BIT_MASK_VALUE(pHeader->Data[1], 0x4)) {
		AmlAppend("BusMaster, ");
	}
	else {
		AmlAppend("NotBusMaster, ");
	}
	if (MASK_VALUE(pHeader->Data[1], 3, 0x0)) {
		AmlAppend("Transfer8) {");
	}
	else if (MASK_VALUE(pHeader->Data[1], 3, 1)) {
		AmlAppend("Transfer8And16) {");
	}
	else if (MASK_VALUE(pHeader->Data[1], 3, 2)) {
		AmlAppend("Transfer16) {");
	}
	else {
		AmlAppend("Reserver) {");
	}
	for (Idx = 0; Idx < 8; Idx++) {
		if (((pHeader->Data[1] >> Idx) & 1) == 1) {
			if (bFirst) {
				AmlAppend("%d", Idx);
				bFirst = FALSE;
			}
			else {
				AmlAppend(" ,%d", Idx);
			}
		}
	}
	AmlAppend("}\n");
}


VOID
ParseSmallIo(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	SetupOffset();
	AmlAppend("IO (");
	if (BIT_MASK_VALUE(pHeader->Data[0], 1)) {
		AmlAppend("Decode16, ");
		AmlAppend("0x%02X%02X, ", pHeader->Data[2], pHeader->Data[1]);
		AmlAppend("0x%02X%02X, ", pHeader->Data[4], pHeader->Data[3]);
		AmlAppend("0x%02X, ", pHeader->Data[5]);
		AmlAppend("0x%02X)\n", pHeader->Data[6]);
	}
	else {
		AmlAppend("Decode10, ");
		AmlAppend("0x%02X%02X, ", pHeader->Data[2] & 0x3, pHeader->Data[1]);
		AmlAppend("0x%02X%02X, ", pHeader->Data[4] & 0x3, pHeader->Data[3]);
		AmlAppend("0x%02X, ", pHeader->Data[5]);
		AmlAppend("0x%02X)\n", pHeader->Data[6]);
	}
}

VOID
ParseSmallFixIo(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	SetupOffset();
	AmlAppend("FixedIO (");
	AmlAppend("0x%02X%02X, ", pHeader->Data[1] & 0x3, pHeader->Data[0]);
	AmlAppend("0x%02X\n)", pHeader->Data[2]);
}

#define NEXT_RESOURCE_DATA(a) \
    if ((a)->Tag == 0) { (a) = (PSMALL_RESOURCE_DATA_TYPE_HEADER) ((UINTN)(a) + 1 + (UINTN) ((a)->Length));} \
    else {(a) = (PSMALL_RESOURCE_DATA_TYPE_HEADER) ((UINTN)(a) + 3 + (UINTN) (((PLARGE_RESOURCE_DATA_TYPE_HEADER)(a))->Length));}

VOID
ParseSmallDepend(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	PSMALL_RESOURCE_DATA_TYPE_HEADER pNext;
	//PSMALL_RESOURCE_DATA_TYPE_HEADER
	if (pHeader->ItemName == SMALL_ITEM_START_DEP) {
		if (pHeader->Length == 1) {
			AmlAppend("    StartDependentFn (\n");
			AmlAppend("        %d,", (pHeader->Data[0]) & 3);
			AmlAppend("        %d)\n    {\n", (pHeader->Data[0] >> 2) & 3);
		}
		else {

			AmlAppend("    StartDependentFnNoPri () {\n");
		}
		//
		// Start Next
		//
		pNext = pHeader;
		NEXT_RESOURCE_DATA(pNext)
			while (!(pNext->Tag == 0 && pNext->ItemName == SMALL_ITEM_END_DEP)) {
				//
				// Checked
				//.......................
				ParseResourceTemplate(pNext);
				// Next Item
				NEXT_RESOURCE_DATA(pNext)
			}
	}
	else {
		AmlAppend("EndDependentFn()");
	}
}



VOID
ParseSmallVendor(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	UNREFERENCED_PARAMETER(pHeader);
	AmlAppend("    Vendor defined");
}

VOID
ParseLarge24Bit(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	SetupOffset();
	AmlAppend("Memory24 (\n");
	gOffset++;
	SetupOffset();
	if ((pHeader->Data[0] & 1) == 1) {
		AmlAppend("Writeable,\n");
	}
	else {
		AmlAppend("ReadOnly,\n");
	}
	SetupOffset();
	AmlAppend("0x00%02X%02X00,\n",
		pHeader->Data[2], pHeader->Data[1]);
	SetupOffset();
	AmlAppend("0x00%02X%02X00,\n",
		pHeader->Data[4], pHeader->Data[3]);
	SetupOffset();
	AmlAppend("0x0000%02X%02X,\n",
		pHeader->Data[6], pHeader->Data[5]);
	SetupOffset();
	AmlAppend("0x00%02X%02X00)\n",
		pHeader->Data[8], pHeader->Data[7]);
	gOffset--;
}

VOID
ParseLargeVendor(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	UNREFERENCED_PARAMETER(pHeader);
	SetupOffset();
	AmlAppend("Vendor ()\n");
}

VOID
ParseLarge32BitMem(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	SetupOffset();
	AmlAppend("Memory32 (\n");
	gOffset++;
	SetupOffset();
	if ((pHeader->Data[0] & 1) == 1) {
		AmlAppend("Writeable,\n");
	}
	else {
		AmlAppend("ReadOnly,\n");
	}
	SetupOffset();
	AmlAppend("0x%02X%02X%02X%02X,\n",
		pHeader->Data[4], pHeader->Data[3],
		pHeader->Data[2], pHeader->Data[1]);
	SetupOffset();
	AmlAppend("0x%02X%02X%02X%02X,\n",
		pHeader->Data[8], pHeader->Data[7],
		pHeader->Data[6], pHeader->Data[5]);
	SetupOffset();
	AmlAppend("0x%02X%02X%02X%02X,\n",
		pHeader->Data[12], pHeader->Data[11],
		pHeader->Data[10], pHeader->Data[9]);
	SetupOffset();
	AmlAppend("0x%02X%02X%02X%02X\n)\n",
		pHeader->Data[16], pHeader->Data[15],
		pHeader->Data[14], pHeader->Data[13]);
	gOffset--;
}

VOID
ParseLarge32BitMemFix(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	SetupOffset();
	AmlAppend("Memory32Fixed (\n");
	gOffset++;
	SetupOffset();
	if ((pHeader->Data[0] & 1) == 1) {
		AmlAppend("Writeable,\n");
	}
	else {
		AmlAppend("ReadOnly,\n");
	}
	SetupOffset();
	AmlAppend("0x%02X%02X%02X%02X,\n",
		pHeader->Data[4], pHeader->Data[3],
		pHeader->Data[2], pHeader->Data[1]);
	SetupOffset();
	AmlAppend("0x%02X%02X%02X%02X\n)\n",
		pHeader->Data[8], pHeader->Data[7],
		pHeader->Data[6], pHeader->Data[5]);
	gOffset--;

}

VOID
ParseLargeQwordResource(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	QWORD_RESOURCE* pQword = (QWORD_RESOURCE*)pHeader;

	SetupOffset();
	if (pHeader->Data[0] == 0) {
		//
		// Memory Resource
		AmlAppend("QWordMemory (\n");
	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
		AmlAppend("QWordIO (\n");
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
		AmlAppend("QWordSpace (\n");
	}
	else if (pHeader->Data[0] >= 192) {
		AmlAppend("QWord Vendor Define (\n");
		//
		// Hardware Vendor Defined Resource

	}
	else {
		return;
	}
	gOffset++;

	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 1)) {
		AmlAppend("ResourceConsumer,\n");
	}
	else {
		AmlAppend("ResourceProducer,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 2)) {
		AmlAppend("Substract-Decode,\n");
	}
	else {
		AmlAppend("Positive-Decode,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 4)) {
		AmlAppend("MinFixed,\n");
	}
	else {
		AmlAppend("MinNotFixed,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 8)) {
		AmlAppend("MaxFixed,\n");
	}
	else {
		AmlAppend("MaxNotFixed,\n");
	}
	// SetupOffset ();
	if (pHeader->Data[0] == 0) {
		SetupOffset();
		//
		// Memory Resource
		//AmlAppend("QWordMemory (\n");
		if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0)) {
			AmlAppend("Non-Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x2)) {
			AmlAppend("Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x4)) {
			AmlAppend("Cacheable-Write Combining,\n");
		}
		else {
			AmlAppend("Cacheable-Prefetchable,\n");
		}
		SetupOffset();
		if (BIT_MASK_VALUE(pQword->TypeSpecificFlags, 1)) {
			AmlAppend("ReadWrite,\n");
		}
		else {
			AmlAppend("ReadOnly,\n");
		}

	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
	}
	SetupOffset();
	AmlAppend("0x%08X%08X,\n", pQword->Gra1, pQword->Gra);
	SetupOffset();
	AmlAppend("0x%08X%08X,\n", pQword->Min1, pQword->Min);
	SetupOffset();
	AmlAppend("0x%08X%08X,\n", pQword->Max1, pQword->Max);
	SetupOffset();
	AmlAppend("0x%08X%08X,\n", pQword->Tra1, pQword->Tra);
	SetupOffset();
	AmlAppend("0x%08X%08X)\n", pQword->Len1, pQword->Len);

	gOffset--;
}

VOID
ParseLargeExtendResource(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	EXTENDED_ADDRESS_SPACE* pQword = (EXTENDED_ADDRESS_SPACE*)pHeader;

	SetupOffset();
	if (pHeader->Data[0] == 0) {
		//
		// Memory Resource
		AmlAppend("QWordMemory (\n");
	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
		AmlAppend("QWordIO (\n");
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
		AmlAppend("QWordSpace (\n");
	}
	else if (pHeader->Data[0] >= 192) {
		AmlAppend("QWord Vendor Define (\n");
		//
		// Hardware Vendor Defined Resource

	}
	else {
		return;
	}
	gOffset++;

	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 1)) {
		AmlAppend("ResourceConsumer,\n");
	}
	else {
		AmlAppend("ResourceProducer,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 2)) {
		AmlAppend("Substract-Decode,\n");
	}
	else {
		AmlAppend("Positive-Decode,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 4)) {
		AmlAppend("MinFixed,\n");
	}
	else {
		AmlAppend("MinNotFixed,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 8)) {
		AmlAppend("MaxFixed,\n");
	}
	else {
		AmlAppend("MaxNotFixed,\n");
	}
	//SetupOffset ();
	if (pHeader->Data[0] == 0) {
		SetupOffset();
		//
		// Memory Resource
		//AmlAppend("QWordMemory (\n");
		if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0)) {
			AmlAppend("Non-Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x2)) {
			AmlAppend("Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x4)) {
			AmlAppend("Cacheable-Write Combining,\n");
		}
		else {
			AmlAppend("Cacheable-Prefetchable,\n");
		}
		SetupOffset();
		if (BIT_MASK_VALUE(pQword->TypeSpecificFlags, 1)) {
			AmlAppend("ReadWrite,\n");
		}
		else {
			AmlAppend("ReadOnly,\n");
		}

	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
	}
	SetupOffset();
	AmlAppend("0x%08X%08X,", pQword->Gra1, pQword->Gra);
	SetupOffset();
	AmlAppend("0x%08X%08X,", pQword->Min1, pQword->Min);
	SetupOffset();
	AmlAppend("0x%08X%08X,", pQword->Max1, pQword->Max);
	SetupOffset();
	AmlAppend("0x%08X%08X,", pQword->Tra1, pQword->Tra);
	SetupOffset();
	AmlAppend("0x%08X%08X)\n", pQword->Len1, pQword->Len);
	SetupOffset();
	AmlAppend("0x%08X%08X)\n", pQword->Att1, pQword->Att);

	gOffset--;
}

VOID
ParseLargeDwordResource(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	DWORD_RESOURCE* pQword = (DWORD_RESOURCE*)pHeader;

	SetupOffset();
	if (pHeader->Data[0] == 0) {
		//
		// Memory Resource
		AmlAppend("DWordMemory (\n");
	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
		AmlAppend("DWordIO (\n");
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
		AmlAppend("DWordSpace (\n");
	}
	else if (pHeader->Data[0] >= 192) {
		AmlAppend("DWord Vendor Define (\n");
		//
		// Hardware Vendor Defined Resource

	}
	else {
		return;
	}
	gOffset++;

	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 1)) {
		AmlAppend("ResourceConsumer,\n");
	}
	else {
		AmlAppend("ResourceProducer,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 2)) {
		AmlAppend("Substract-Decode,\n");
	}
	else {
		AmlAppend("Positive-Decode,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 4)) {
		AmlAppend("MinFixed,\n");
	}
	else {
		AmlAppend("MinNotFixed,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 8)) {
		AmlAppend("MaxFixed,\n");
	}
	else {
		AmlAppend("MaxNotFixed,\n");
	}

	if (pHeader->Data[0] == 0) {
		SetupOffset();
		//
		// Memory Resource
		//AmlAppend("QWordMemory (\n");
		if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0)) {
			AmlAppend("Non-Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x2)) {
			AmlAppend("Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x4)) {
			AmlAppend("Cacheable-Write Combining,\n");
		}
		else {
			AmlAppend("Cacheable-Prefetchable,\n");
		}
		SetupOffset();
		if (BIT_MASK_VALUE(pQword->TypeSpecificFlags, 1)) {
			AmlAppend("ReadWrite,\n");
		}
		else {
			AmlAppend("ReadOnly,\n");
		}

	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
	}

	SetupOffset();
	AmlAppend("0x%08X,\n", pQword->Gra, pQword->Gra);
	SetupOffset();
	AmlAppend("0x%08X,\n", pQword->Min, pQword->Min);
	SetupOffset();
	AmlAppend("0x%08X,\n", pQword->Max, pQword->Max);
	SetupOffset();
	AmlAppend("0x%08X,\n", pQword->Tra, pQword->Tra);
	SetupOffset();
	AmlAppend("0x%08X)\n", pQword->Len, pQword->Len);
	gOffset--;
}

VOID
ParseLargeExtAddrResource(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	USHORT Length = pHeader->Length;
}

VOID
ParseLargeWordResource(PLARGE_RESOURCE_DATA_TYPE_HEADER pHeader)
{
	WORD_RESOURCE* pQword = (WORD_RESOURCE*)pHeader;

	SetupOffset();
	if (pHeader->Data[0] == 0) {
		//
		// Memory Resource
		AmlAppend("WordMemory (\n");
	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
		AmlAppend("WordIO (\n");
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
		AmlAppend("WordBusSpace (\n");
	}
	else if (pHeader->Data[0] >= 192) {
		AmlAppend("Word Vendor Define (\n");
		//
		// Hardware Vendor Defined Resource

	}
	else {
		return;
	}
	gOffset++;

	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 1)) {
		AmlAppend("ResourceConsumer,\n");
	}
	else {
		AmlAppend("ResourceProducer,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 2)) {
		AmlAppend("Substract-Decode,\n");
	}
	else {
		AmlAppend("Positive-Decode,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 4)) {
		AmlAppend("MinFixed,\n");
	}
	else {
		AmlAppend("MinNotFixed,\n");
	}
	SetupOffset();
	if (BIT_MASK_VALUE(pHeader->Data[1], 8)) {
		AmlAppend("MaxFixed,\n");
	}
	else {
		AmlAppend("MaxNotFixed,\n");
	}

	if (pHeader->Data[0] == 0) {
		SetupOffset();
		//
		// Memory Resource
		//AmlAppend("QWordMemory (\n");
		if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0)) {
			AmlAppend("Non-Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x2)) {
			AmlAppend("Cacheable,\n");
		}
		else if (MASK_VALUE(pQword->TypeSpecificFlags, 0x06, 0x4)) {
			AmlAppend("Cacheable-Write Combining,\n");
		}
		else {
			AmlAppend("Cacheable-Prefetchable,\n");
		}

		if (BIT_MASK_VALUE(pQword->TypeSpecificFlags, 1)) {
			SetupOffset();
			AmlAppend("ReadWrite,\n");
		}
		else {
			SetupOffset();
			AmlAppend("ReadOnly,\n");
		}

	}
	else if (pHeader->Data[0] == 1) {
		//
		// IO Resource
	}
	else if (pHeader->Data[0] == 2) {
		//
		// BUs Resource
	}

	SetupOffset();
	AmlAppend("0x%04X,\n", pQword->Gra, pQword->Gra);
	SetupOffset();
	AmlAppend("0x%04X,\n", pQword->Min, pQword->Min);
	SetupOffset();
	AmlAppend("0x%04X,\n", pQword->Max, pQword->Max);
	SetupOffset();
	AmlAppend("0x%04X,\n", pQword->Tra, pQword->Tra);
	SetupOffset();
	AmlAppend("0x%04X)\n", pQword->Len, pQword->Len);
	gOffset--;
}

VOID
ParseResourceTemplate(PSMALL_RESOURCE_DATA_TYPE_HEADER pHeader)
{

	BYTE    Data;
	PLARGE_RESOURCE_DATA_TYPE_HEADER pLarger =
		(PLARGE_RESOURCE_DATA_TYPE_HEADER)pHeader;

	memcpy(&Data, pHeader, sizeof(Data));
	while (TRUE) {
		if ((Data & 0x80) == 0) {
			switch (pHeader->ItemName) {
			case SMALL_ITEM_IRQ:
				ParseSmallIrq(pHeader);
				break;
			case SMALL_ITEM_DMA:
				ParseSmallDma(pHeader);
				break;
			case SMALL_ITEM_START_DEP:
			case SMALL_ITEM_END_DEP:
				ParseSmallDepend(pHeader);
				break;
			case SMALL_ITEM_IO:
				ParseSmallIo(pHeader);
				break;
			case SMALL_ITEM_FIXED_IO:
				ParseSmallFixIo(pHeader);
				break;
			case SMALL_ITEM_VENDOR_DEF:
				ParseSmallVendor(pHeader);
				break;
			case SMALL_ITEM_END_TAG:
				assert(0);
				break;
			default:
				assert(0);
				break;
			}
		}
		else {
			switch (pLarger->ItemName) {
			case LARGE_ITEM_24BIT_MEMORY:
				ParseLarge24Bit(pLarger);
				break;
			case LARGE_ITEM_32BIT_FIXED_MEMORY:
				ParseLarge32BitMemFix(pLarger); 
				break;
			case LARGE_ITEM_32BIT_MEMORY:
				ParseLarge32BitMem(pLarger);
				break;
			case LARGE_ITEM_DWORD_ADDR_SPACE:
				ParseLargeDwordResource(pLarger);
				break;
			case LARGE_ITEM_QWORD_ADDR_SPACE:
				ParseLargeQwordResource(pLarger);
				break;
			case LARGE_ITEM_WORD_ADDR_SPACE:
				ParseLargeWordResource(pLarger);
				break;
			case LARGE_ITEM_EXT_ADDR_SPACE:
				assert(0);
				ParseLargeExtAddrResource(pLarger);
				break;
			case LARGE_ITEM_EXT_IRQ:
				ParseLargeExtIrqResource(pLarger);
				//assert(0);
				break;
			default:
				assert(0);
				break;
			}
		}
		break;
	}
}


VOID
ParseResourceTemplateData(PACPI_EVAL_OUTPUT_BUFFER pArg) //LPBYTE Data, ULONG Length)
{
	PSMALL_RESOURCE_DATA_TYPE_HEADER pNext;
	LPBYTE  End;
	LPBYTE  Start;
	if (pArg->Count > 1) {
		assert(0);
	}
	Start = pArg->Argument[0].Data;
	End = Start + (UINTN)pArg->Argument[0].DataLength;
	pNext = (PSMALL_RESOURCE_DATA_TYPE_HEADER)Start;
	do {
		if (pNext->Tag == 0 && pNext->ItemName == SMALL_ITEM_END_TAG) {
			break;
		}
		ParseResourceTemplate(pNext);
		// Next Item
		NEXT_RESOURCE_DATA(pNext)
		//
		// Check the end of Scope
		//
		Start = (LPBYTE)pNext;
	} while (Start < End);
}

BOOL
VerifyValidCstReturnData(PACPI_EVAL_OUTPUT_BUFFER pAcpiData)
{
	// Number
	//
	PACPI_METHOD_ARGUMENT       arg;
	PACPI_METHOD_ARGUMENT       data;
	UINT                        Idx;
	ULONG                       CstCnt;

	//__asm int 3;
	arg = pAcpiData->Argument;
	if (arg->Type != ACPI_METHOD_ARGUMENT_INTEGER) {
		return FALSE;
	}
	CstCnt = (ULONG)arg->Argument + 1;

	if (CstCnt != pAcpiData->Count) {
		return FALSE;
	}

	AmlAppend("\n  C-State count %d\n",
		CstCnt - 1
	);

	arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	for (Idx = 0; Idx < CstCnt - 1; Idx++) {
		data = (PACPI_METHOD_ARGUMENT)arg->Data;

		//
		//if (!VerifyGenericRegisterBuffer (arg, NULL, FALSE)) {
		//    //return FALSE;
		//    }
		AmlAppend("\n  CState %d\n",
			Idx
		);

		VerifyGenericRegisterBuffer(data, "    Register", TRUE);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("\n    Type            C%d\n",
			data->Data[0]
		);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Lantency        %dms\n",
			*((UINT16*)&data->Data[0])
		);
		data = ACPI_METHOD_NEXT_ARGUMENT(data);
		AmlAppend("    Power           %dmv\n",
			data->Argument
		);
		arg = ACPI_METHOD_NEXT_ARGUMENT(arg);
	}


	return TRUE;
}