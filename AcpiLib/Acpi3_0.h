
#ifndef _ACPI_3_0_H_
#define _ACPI_3_0_H_

//
// Statements that include other files
//
#include "AcpiCommon.h"

//
// Ensure proper structure formats
//
#pragma pack(1)
//
// ACPI Specification Revision
//
#define ACPI_3_0_REVISION 0x03

//
// ACPI 3.0 Generic Address Space definition
//
typedef struct {
  UINT8   AddressSpaceId;
  UINT8   RegisterBitWidth;
  UINT8   RegisterBitOffset;
  UINT8   AccessSize;
  union {
    UINT64  Address;
    struct {
        UINT32 Low32;
        UINT32 High32;
        };
    };
} GENERIC_ADDRESS_STRUCTURE, *PGENERIC_ADDRESS_STRUCTURE;

typedef GENERIC_ADDRESS_STRUCTURE ACPI_3_0_GENERIC_ADDRESS_STRUCTURE;

//
// Generic Address Space Address IDs
//
#define ACPI_3_0_SYSTEM_MEMORY              0
#define ACPI_3_0_SYSTEM_IO                  1
#define ACPI_3_0_PCI_CONFIGURATION_SPACE    2
#define ACPI_3_0_EMBEDDED_CONTROLLER        3
#define ACPI_3_0_SMBUS                      4
#define ACPI_3_0_FUNCTIONAL_FIXED_HARDWARE  0x7F

//
// Generic Address Space Access Sizes
//
#define ACPI_3_0_UNDEFINED  0
#define ACPI_3_0_BYTE       1
#define ACPI_3_0_WORD       2
#define ACPI_3_0_DWORD      3
#define ACPI_3_0_QWORD      4

//
// ACPI 3.0 table structures
//
//
// Root System Description Pointer Structure
//
typedef struct {
    union {
        UINT64  Signature;
        UCHAR   Sig[8];
        };
    UINT8   Checksum;
    UINT8   OemId[6];
    UINT8   Revision;
    UINT32  RsdtAddress;
    UINT32  Length;
    UINT64  XsdtAddress;
    UINT8   ExtendedChecksum;
    UINT8   Reserved[3];
} ACPI_ROOT_SYSTEM_DESCRIPTION_POINTER;

//
// RSD_PTR Revision (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_ROOT_SYSTEM_DESCRIPTION_POINTER_REVISION 0x02  // ACPISpec30 (Revision 3.0) says current value is 2
//
// Common table header, this prefaces all ACPI tables, including FACS, but
// excluding the RSD PTR structure
//
typedef struct {
  UINT32  Signature;
  UINT32  Length;
} ACPI_COMMON_HEADER;

//
// Root System Description Table
// No definition needed as it is a common description table header followed by a
// variable number of UINT32 table pointers.
//
//
// RSDT Revision (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_ROOT_SYSTEM_DESCRIPTION_TABLE_REVISION 0x01

//
// Extended System Description Table
// No definition needed as it is a common description table header followed by a
// variable number of UINT64 table pointers.
//
//
// XSDT Revision (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_EXTENDED_SYSTEM_DESCRIPTION_TABLE_REVISION 0x01

//
// Fixed ACPI Description Table Structure (FADT)
//
typedef struct {
  ACPI_DESCRIPTION_HEADER             Header;
  UINT32                                  FirmwareCtrl;
  UINT32                                  Dsdt;
  UINT8                                   Reserved0;
  UINT8                                   PreferredPmProfile;
  UINT16                                  SciInt;
  UINT32                                  SmiCmd;
  UINT8                                   AcpiEnable;
  UINT8                                   AcpiDisable;
  UINT8                                   S4BiosReq;
  UINT8                                   PstateCnt;
  UINT32                                  Pm1aEvtBlk;
  UINT32                                  Pm1bEvtBlk;
  UINT32                                  Pm1aCntBlk;
  UINT32                                  Pm1bCntBlk;
  UINT32                                  Pm2CntBlk;
  UINT32                                  PmTmrBlk;
  UINT32                                  Gpe0Blk;
  UINT32                                  Gpe1Blk;
  UINT8                                   Pm1EvtLen;
  UINT8                                   Pm1CntLen;
  UINT8                                   Pm2CntLen;
  UINT8                                   PmTmrLen;
  UINT8                                   Gpe0BlkLen;
  UINT8                                   Gpe1BlkLen;
  UINT8                                   Gpe1Base;
  UINT8                                   CstCnt;
  UINT16                                  PLvl2Lat;
  UINT16                                  PLvl3Lat;
  UINT16                                  FlushSize;
  UINT16                                  FlushStride;
  UINT8                                   DutyOffset;
  UINT8                                   DutyWidth;
  UINT8                                   DayAlrm;
  UINT8                                   MonAlrm;
  UINT8                                   Century;
  UINT16                                  IaPcBootArch;
  UINT8                                   Reserved1;
  UINT32                                  Flags;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  ResetReg;
  UINT8                                   ResetValue;
  UINT8                                   Reserved2[3];
  UINT64                                  XFirmwareCtrl;
  UINT64                                  XDsdt;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XPm1aEvtBlk;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XPm1bEvtBlk;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XPm1aCntBlk;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XPm1bCntBlk;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XPm2CntBlk;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XPmTmrBlk;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XGpe0Blk;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  XGpe1Blk;
} ACPI_3_0_FIXED_ACPI_DESCRIPTION_TABLE, *PACPI_3_0_FIXED_ACPI_DESCRIPTION_TABLE;

//
// FADT Version (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_FIXED_ACPI_DESCRIPTION_TABLE_REVISION  0x04

//
// Fixed ACPI Description Table Preferred Power Management Profile
//
#define ACPI_3_0_PM_PROFILE_UNSPECIFIED         0
#define ACPI_3_0_PM_PROFILE_DESKTOP             1
#define ACPI_3_0_PM_PROFILE_MOBILE              2
#define ACPI_3_0_PM_PROFILE_WORKSTATION         3
#define ACPI_3_0_PM_PROFILE_ENTERPRISE_SERVER   4
#define ACPI_3_0_PM_PROFILE_SOHO_SERVER         5
#define ACPI_3_0_PM_PROFILE_APPLIANCE_PC        6
#define ACPI_3_0_PM_PROFILE_PERFORMANCE_SERVER  7

//
// Fixed ACPI Description Table Boot Architecture Flags
// All other bits are reserved and must be set to 0.
//
#define ACPI_3_0_LEGACY_DEVICES    (1 << 0)
#define ACPI_3_0_8042              (1 << 1)
#define ACPI_3_0_VGA_NOT_PRESENT   (1 << 2)
#define ACPI_3_0_MSI_NOT_SUPPORTED (1 << 3)
#define ACPI_3_0_PCIE_ASPM_CONTROLS (1 << 4)
//
// Fixed ACPI Description Table Fixed Feature Flags
// All other bits are reserved and must be set to 0.
//
#define ACPI_3_0_WBINVD                   (1 << 0)
#define ACPI_3_0_WBINVD_FLUSH             (1 << 1)
#define ACPI_3_0_PROC_C1                  (1 << 2)
#define ACPI_3_0_P_LVL2_UP                (1 << 3)
#define ACPI_3_0_PWR_BUTTON               (1 << 4)
#define ACPI_3_0_SLP_BUTTON               (1 << 5)
#define ACPI_3_0_FIX_RTC                  (1 << 6)
#define ACPI_3_0_RTC_S4                   (1 << 7)
#define ACPI_3_0_TMR_VAL_EXT              (1 << 8)
#define ACPI_3_0_DCK_CAP                  (1 << 9)
#define ACPI_3_0_RESET_REG_SUP            (1 << 10)
#define ACPI_3_0_SEALED_CASE              (1 << 11)
#define ACPI_3_0_HEADLESS                 (1 << 12)
#define ACPI_3_0_CPU_SW_SLP               (1 << 13)
#define ACPI_3_0_PCI_EXP_WAK              (1 << 14)
#define ACPI_3_0_USE_PLATFORM_CLOCK       (1 << 15)
#define ACPI_3_0_S4_RTC_STS_VALID         (1 << 16)
#define ACPI_3_0_REMOTE_POWER_ON_CAPABLE  (1 << 17)
#define ACPI_3_0_FORCE_APIC_CLUSTER_MODEL (1 << 18)
#define ACPI_3_0_FORCE_APIC_PHYSICAL_DESTINATION_MODE (1 << 19)

//
// Firmware ACPI Control Structure
//
typedef struct {
  UINT32  Signature;
  UINT32  Length;
  UINT32  HardwareSignature;
  UINT32  FirmwareWakingVector;
  UINT32  GlobalLock;
  UINT32  Flags;
  UINT64  XFirmwareWakingVector;
  UINT8   Version;
  UINT8   Reserved[31];
} ACPI_3_0_FIRMWARE_ACPI_CONTROL_STRUCTURE;

//
// FACS Version (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_FIRMWARE_ACPI_CONTROL_STRUCTURE_VERSION  0x01

//
// Firmware Control Structure Feature Flags
// All other bits are reserved and must be set to 0.
//
#define ACPI_3_0_S4BIOS_F (1 << 0)

//
// Differentiated System Description Table,
// Secondary System Description Table
// and Persistent System Description Table,
// no definition needed as they are common description table header followed by a
// definition block.
//
#define ACPI_3_0_DIFFERENTIATED_SYSTEM_DESCRIPTION_TABLE_REVISION   0x02
#define ACPI_3_0_SECONDARY_SYSTEM_DESCRIPTION_TABLE_REVISION        0x02

//
// Multiple APIC Description Table header definition.  The rest of the table
// must be defined in a platform specific manner.
//
typedef struct {
  ACPI_DESCRIPTION_HEADER Header;
  UINT32                      LocalApicAddress;
  UINT32                      Flags;
} ACPI_3_0_MADT_TABLE_HEADER, *PACPI_3_0_MADT_TABLE_HEADER;

//
// MADT Revision (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_MADT_TABLE_REVISION 0x02

//
// Multiple APIC Flags
// All other bits are reserved and must be set to 0.
//
#define ACPI_3_0_PCAT_COMPAT  (1 << 0)

//
// Multiple APIC Description Table APIC structure types
// All other values between 0x09 an 0xFF are reserved and
// will be ignored by OSPM.
//
#define ACPI_3_0_PROCESSOR_LOCAL_APIC           0x00
#define ACPI_3_0_IO_APIC                        0x01
#define ACPI_3_0_INTERRUPT_SOURCE_OVERRIDE      0x02
#define ACPI_3_0_NON_MASKABLE_INTERRUPT_SOURCE  0x03
#define ACPI_3_0_LOCAL_APIC_NMI                 0x04
#define ACPI_3_0_LOCAL_APIC_ADDRESS_OVERRIDE    0x05
#define ACPI_3_0_IO_SAPIC                       0x06
#define ACPI_3_0_LOCAL_SAPIC                    0x07
#define ACPI_3_0_PLATFORM_INTERRUPT_SOURCES     0x08

//
// APIC Structure Definitions
//
//
// Processor Local APIC Structure Definition
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT8   AcpiProcessorId;
  UINT8   ApicId;
  UINT32  Flags;
} ACPI_3_0_PROCESSOR_LOCAL_APIC_STRUCTURE, *PACPI_3_0_PROCESSOR_LOCAL_APIC_STRUCTURE;

//
// Local APIC Flags.  All other bits are reserved and must be 0.
//
#define ACPI_3_0_LOCAL_APIC_ENABLED (1 << 0)

//
// IO APIC Structure
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT8   IoApicId;
  UINT8   Reserved;
  UINT32  IoApicAddress;
  UINT32  GlobalSystemInterruptBase;
} ACPI_3_0_IO_APIC_STRUCTURE, *PACPI_3_0_IO_APIC_STRUCTURE;

//
// Interrupt Source Override Structure
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT8   Bus;
  UINT8   Source;
  UINT32  GlobalSystemInterrupt;
  UINT16  Flags;
} ACPI_3_0_INTERRUPT_SOURCE_OVERRIDE_STRUCTURE, *PACPI_3_0_INTERRUPT_SOURCE_OVERRIDE_STRUCTURE;

//
// Platform Interrupt Sources Structure Definition
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT16  Flags;
  UINT8   InterruptType;
  UINT8   ProcessorId;
  UINT8   ProcessorEid;
  UINT8   IoSapicVector;
  UINT32  GlobalSystemInterrupt;
  UINT32  PlatformInterruptSourceFlags;
  UINT8   CpeiProcessorOverride;
  UINT8   Reserved[31];
} ACPI_3_0_PLATFORM_INTERRUPT_APIC_STRUCTURE, *PACPI_3_0_PLATFORM_INTERRUPT_APIC_STRUCTURE;

//
// MPS INTI flags.
// All other bits are reserved and must be set to 0.
//
#define ACPI_3_0_POLARITY      (3 << 0)
#define ACPI_3_0_TRIGGER_MODE  (3 << 2)

//
// Non-Maskable Interrupt Source Structure
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT16  Flags;
  UINT32  GlobalSystemInterrupt;
} ACPI_3_0_NON_MASKABLE_INTERRUPT_SOURCE_STRUCTURE, *PACPI_3_0_NON_MASKABLE_INTERRUPT_SOURCE_STRUCTURE;

//
// Local APIC NMI Structure
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT8   AcpiProcessorId;
  UINT16  Flags;
  UINT8   LocalApicLint;
} ACPI_3_0_LOCAL_APIC_NMI_STRUCTURE, *PACPI_3_0_LOCAL_APIC_NMI_STRUCTURE;

//
// Local APIC Address Override Structure
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT16  Reserved;
  UINT64  LocalApicAddress;
} ACPI_3_0_LOCAL_APIC_ADDRESS_OVERRIDE_STRUCTURE, *PACPI_3_0_LOCAL_APIC_ADDRESS_OVERRIDE_STRUCTURE;

//
// IO SAPIC Structure
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT8   IoApicId;
  UINT8   Reserved;
  UINT32  GlobalSystemInterruptBase;
  UINT64  IoSapicAddress;
} ACPI_3_0_IO_SAPIC_STRUCTURE, *PACPI_3_0_IO_SAPIC_STRUCTURE;

//
// Local SAPIC Structure
// This struct followed by a null-terminated ASCII string - ACPI Processor UID String
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT8   AcpiProcessorId;
  UINT8   LocalSapicId;
  UINT8   LocalSapicEid;
  UINT8   Reserved[3];
  UINT32  Flags;
  UINT32  ACPIProcessorUIDValue;
} ACPI_3_0_PROCESSOR_LOCAL_SAPIC_STRUCTURE, *PACPI_3_0_PROCESSOR_LOCAL_SAPIC_STRUCTURE;

//
// Platform Interrupt Sources Structure
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT16  Flags;
  UINT8   InterruptType;
  UINT8   ProcessorId;
  UINT8   ProcessorEid;
  UINT8   IoSapicVector;
  UINT32  GlobalSystemInterrupt;
  UINT32  PlatformInterruptSourceFlags;
} ACPI_3_0_PLATFORM_INTERRUPT_SOURCES_STRUCTURE, *PACPI_3_0_PLATFORM_INTERRUPT_SOURCES_STRUCTURE;

//
// Platform Interrupt Source Flags.
// All other bits are reserved and must be set to 0.
//
#define ACPI_3_0_CPEI_PROCESSOR_OVERRIDE     (1 << 0)

//
// Smart Battery Description Table (SBST)
//
typedef struct {
  ACPI_DESCRIPTION_HEADER Header;
  UINT32                      WarningEnergyLevel;
  UINT32                      LowEnergyLevel;
  UINT32                      CriticalEnergyLevel;
} ACPI_3_0_SMART_BATTERY_DESCRIPTION_TABLE;

//
// SBST Version (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_SMART_BATTERY_DESCRIPTION_TABLE_REVISION 0x01

//
// Embedded Controller Boot Resources Table (ECDT)
// The table is followed by a null terminated ASCII string that contains
// a fully qualified reference to the name space object.
//
typedef struct {
  ACPI_DESCRIPTION_HEADER             Header;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  EcControl;
  ACPI_3_0_GENERIC_ADDRESS_STRUCTURE  EcData;
  UINT32                                  Uid;
  UINT8                                   GpeBit;
} ACPI_3_0_EMBEDDED_CONTROLLER_BOOT_RESOURCES_TABLE;

//
// ECDT Version (as defined in ACPI 3.0 spec.)
//
#define ACPI_3_0_EMBEDDED_CONTROLLER_BOOT_RESOURCES_TABLE_REVISION  0x01

//
// System Resource Affinity Table (SRAT.  The rest of the table
// must be defined in a platform specific manner.
//
typedef struct {
  ACPI_DESCRIPTION_HEADER Header;
  UINT32                      Reserved1;  // Must be set to 1
  UINT64                      Reserved2;
} SYSTEM_RESOURCE_AFFINITY_TABLE_HEADER, *PSYSTEM_RESOURCE_AFFINITY_TABLE_HEADER;

//
// SRAT Version (as defined in ACPI 3.0 spec.)
//
#define ACPI_SYSTEM_RESOURCE_AFFINITY_TABLE_REVISION  0x02

//
// SRAT structure types.
// All other values between 0x02 an 0xFF are reserved and
// will be ignored by OSPM.
//
#define ACPI_PROCESSOR_LOCAL_APIC_SAPIC_AFFINITY  0x00
#define ACPI_MEMORY_AFFINITY                      0x01

//
// Processor Local APIC/SAPIC Affinity Structure Definition
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT8   ProximityDomain7To0;
  UINT8   ApicId;
  UINT32  Flags;
  UINT8   LocalSapicEid;
  UINT8   ProximityDomain31To8[3];
  UINT8   Reserved[4];
} PROCESSOR_LOCAL_APIC_SAPIC_AFFINITY_STRUCTURE, *PPROCESSOR_LOCAL_APIC_SAPIC_AFFINITY_STRUCTURE;

//
// Local APIC/SAPIC Flags.  All other bits are reserved and must be 0.
//
#define PROCESSOR_LOCAL_APIC_SAPIC_ENABLED (1 << 0)

//
// Memory Affinity Structure Definition
//
typedef struct {
  UINT8   Type;
  UINT8   Length;
  UINT32  ProximityDomain;
  UINT16  Reserved1;
  UINT32  AddressBaseLow;
  UINT32  AddressBaseHigh;
  UINT32  LengthLow;
  UINT32  LengthHigh;
  UINT32  Reserved2;
  UINT32  Flags;
  UINT64  Reserved3;
} MEMORY_AFFINITY_STRUCTURE, *PMEMORY_AFFINITY_STRUCTURE;

//
// Memory Flags.  All other bits are reserved and must be 0.
//
#define ACPI_MEMORY_ENABLED       (1 << 0)
#define ACPI_MEMORY_HOT_PLUGGABLE (1 << 1)
#define ACPI_MEMORY_NONVOLATILE   (1 << 2)

//
// System Locality Distance Information Table (SLIT).
// The rest of the table is a matrix.
//
typedef struct {
  ACPI_DESCRIPTION_HEADER Header;
  UINT64                  NumberOfSystemLocalities;
} YSTEM_LOCALITY_DISTANCE_INFORMATION_TABLE_HEADER;

//
// SLIT Version (as defined in ACPI 3.0 spec.)
//
#define ACPI_SYSTEM_LOCALITY_DISTANCE_INFORMATION_TABLE_REVISION  0x01

//
// Known table signatures
//
//
// "RSD PTR " Root System Description Pointer
//
#define ACPI_3_0_ROOT_SYSTEM_DESCRIPTION_POINTER_SIGNATURE  0x2052545020445352

//
// "APIC" Multiple APIC Description Table
//
#define ACPI_3_0_MULTIPLE_APIC_DESCRIPTION_TABLE_SIGNATURE  0x43495041

//
// "DSDT" Differentiated System Description Table
//
#define ACPI_3_0_DIFFERENTIATED_SYSTEM_DESCRIPTION_TABLE_SIGNATURE  0x54445344

//
// "ECDT" Embedded Controller Boot Resources Table
//
#define ACPI_3_0_EMBEDDED_CONTROLLER_BOOT_RESOURCES_TABLE_SIGNATURE 0x54444345

//
// "FACP" Fixed ACPI Description Table
//
#define ACPI_3_0_FIXED_ACPI_DESCRIPTION_TABLE_SIGNATURE 0x50434146

//
// "FACS" Firmware ACPI Control Structure
//
#define ACPI_3_0_FIRMWARE_ACPI_CONTROL_STRUCTURE_SIGNATURE  0x53434146

//
// "PSDT" Persistent System Description Table
//
#define ACPI_3_0_PERSISTENT_SYSTEM_DESCRIPTION_TABLE_SIGNATURE  0x54445350

//
// "RSDT" Root System Description Table
//
#define ACPI_3_0_ROOT_SYSTEM_DESCRIPTION_TABLE_SIGNATURE  0x54445352

//
// "SBST" Smart Battery Specification Table
//
#define ACPI_3_0_SMART_BATTERY_SPECIFICATION_TABLE_SIGNATURE  0x54534253

//
// "SLIT" System Locality Information Table
//
#define ACPI_3_0_SYSTEM_LOCALITY_INFORMATION_TABLE_SIGNATURE  0x54494C53

//
// "SRAT" System Resource Affinity Table
//
#define ACPI_3_0_SYSTEM_RESOURCE_AFFINITY_TABLE_SIGNATURE 0x54415253

//
// "SSDT" Secondary System Description Table
//
#define ACPI_3_0_SECONDARY_SYSTEM_DESCRIPTION_TABLE_SIGNATURE 0x54445353

//
// "XSDT" Extended System Description Table
//
#define ACPI_3_0_EXTENDED_SYSTEM_DESCRIPTION_TABLE_SIGNATURE  0x54445358

//
// "BOOT" MS Simple Boot Spec
//
#define ACPI_3_0_SIMPLE_BOOT_FLAG_TABLE_SIGNATURE 0x544F4F42

//
// "CPEP" Corrected Platform Error Polling Table
// See
//
#define ACPI_3_0_CORRECTED_PLATFORM_ERROR_POLLING_TABLE_SIGNATURE 0x50455043

//
// "DBGP" MS Debug Port Spec
//
#define ACPI_3_0_DEBUG_PORT_TABLE_SIGNATURE 0x50474244

//
// "ETDT" Event Timer Description Table
//
#define ACPI_3_0_EVENT_TIMER_DESCRIPTION_TABLE_SIGNATURE  0x54445445

//
// "HPET" IA-PC High Precision Event Timer Table
//
#define ACPI_3_0_HIGH_PRECISION_EVENT_TIMER_TABLE_SIGNATURE 0x54455048

//
// "MCFG" PCI Express Memory Mapped Configuration Space Base Address Description Table
//
#define ACPI_3_0_PCI_EXPRESS_MEMORY_MAPPED_CONFIGURATION_SPACE_BASE_ADDRESS_DESCRIPTION_TABLE_SIGNATURE  0x4746434D

//
// "SPCR" Serial Port Concole Redirection Table
//
#define ACPI_3_0_SERIAL_PORT_CONSOLE_REDIRECTION_TABLE_SIGNATURE  0x52435053

//
// "SPMI" Server Platform Management Interface Table
//
#define ACPI_3_0_SERVER_PLATFORM_MANAGEMENT_INTERFACE_TABLE_SIGNATURE 0x494D5053

//
// "TCPA" Trusted Computing Platform Alliance Capabilities Table
//
#define ACPI_3_0_TRUSTED_COMPUTING_PLATFORM_ALLIANCE_CAPABILITIES_TABLE_SIGNATURE 0x41504354

//
// "WDRT" Watchdog Resource Table
//
#define ACPI_3_0_WATCHDOG_RESOURCE_TABLE_SIGNATURE 0x54524457

//
// MS Simple Boot Flag Table
typedef struct {
	ACPI_DESCRIPTION_HEADER	Header;
	UINT8					CmosIndex;
	UINT8					Rsvd[3];
} ACPI_BOOT_TABLE, *PACPI_BOOT_TABLE;

//
// Intel HPET
typedef struct {
	ACPI_DESCRIPTION_HEADER		Header;
	UINT32						EventTimerId;
	GENERIC_ADDRESS_STRUCTURE	BaseAddr;
	UINT8						HpetNumber;
	UINT16						MainCounterMinimumClockTickInPerodic;
	UINT8						PageProtectionAndOEMAttribute;
} ACPI_HPET_TABLE, *PACPI_HPET_TABLE;

//
// PCI MCFG
typedef struct {
	UINT64_D	BaseAddr;
	UINT16		PciSeg;
	UINT8		StartBus;
	UINT8		EndBus;
	UINT32		Rsvd;
} MCFG_CONFIG_SPACE;

typedef struct {
	ACPI_DESCRIPTION_HEADER		Header;
	UINT64						Rsvd;
	MCFG_CONFIG_SPACE			Config[1];	
} ACPI_MCFG_TABLE, *PACPI_MCFG_TABLE;

//
// MS SLIC
#define ACPI_3_0_SLIC_TABLE_SIGNATURE  0x43494C53


#pragma pack()

#endif
