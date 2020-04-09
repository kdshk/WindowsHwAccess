

#ifndef _ACPI_COMMON_H_
#define _ACPI_COMMON_H_

//
// Common ACPI description table header.  This structure prefaces most ACPI tables.
//
#pragma pack(1)
#pragma warning(disable:4201)
#pragma warning(disable:4218)
typedef struct {
	UINT32	Signature;
	UINT32	Length;
	UINT32	HardwareSignautre;
	UINT32	FirmwareWakingVector;
} ACPI_FACS_TABLE, *PACPI_FACS_TABLE;

typedef struct {
	UINT8	bytes[8];
} UINT64_B;

typedef struct {
	UINT16	words[4];
} UINT64_W;

typedef struct {
	UINT32	dwords[2];
} UINT64_D;

typedef struct {
    union {
        UINT32  Signature;
        UCHAR   Sig[4];
        };
    UINT32  Length;
    UINT8   Revision;
    UINT8   Checksum;
    UINT8   OemId[6];
    union {
        UINT64  OemTableId;
        UCHAR   chOemTableId[8];
        };
    UINT32  OemRevision;
    union {
        UINT32  CreatorId;
        UCHAR   chCreatorId[4];
        };
    union {
        UINT32  CreatorRevision;
        struct {
            UINT16  Minor;
            UINT8   Major;
            UINT8   Build;
            };
        };
} ACPI_DESCRIPTION_HEADER, *PACPI_DESCRIPTION_HEADER;


//typedef ACPI_DESCRIPTION_HEADER EFI_ACPI_DESCRIPTION_HEADER;

#pragma pack()
//
// Define for Pci Host Bridge Resource Allocation
//
#define ACPI_ADDRESS_SPACE_DESCRIPTOR 0x8A
#define ACPI_END_TAG_DESCRIPTOR       0x79

#define ACPI_ADDRESS_SPACE_TYPE_MEM   0x00
#define ACPI_ADDRESS_SPACE_TYPE_IO    0x01
#define ACPI_ADDRESS_SPACE_TYPE_BUS   0x02

//
// Make sure structures match spec
//
#pragma pack(1)

typedef struct {
  UINT8   Desc;
  UINT16  Len;
  UINT8   ResType;
  UINT8   GenFlag;
  UINT8   SpecificFlag;
  UINT64  AddrSpaceGranularity;
  UINT64  AddrRangeMin;
  UINT64  AddrRangeMax;
  UINT64  AddrTranslationOffset;
  UINT64  AddrLen;
} EFI_ACPI_ADDRESS_SPACE_DESCRIPTOR;

typedef struct {
  UINT8 Desc;
  UINT8 Checksum;
} EFI_ACPI_END_TAG_DESCRIPTOR;

//
// General use definitions
//
#define EFI_ACPI_RESERVED_BYTE  0x00
#define EFI_ACPI_RESERVED_WORD  0x0000
#define EFI_ACPI_RESERVED_DWORD 0x00000000
#define EFI_ACPI_RESERVED_QWORD 0x0000000000000000

#pragma pack()

#endif
