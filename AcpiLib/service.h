#pragma once

#define DRIVER_FUNC_INSTALL     0x01
#define DRIVER_FUNC_REMOVE      0x02
#define DRIVER_NAME             "HwAcc"


BOOLEAN
InstallService(
    __in SC_HANDLE  SchSCManager,
    __in LPTSTR     ServiceName,
    __in LPTSTR     ServiceExe
);


BOOLEAN
RemoveService(
    __in SC_HANDLE  SchSCManager,
    __in LPTSTR     ServiceName
);

BOOLEAN
DemandService(
    __in SC_HANDLE  SchSCManager,
    __in LPTSTR     ServiceName
);

BOOLEAN
StopService(
    __in SC_HANDLE  SchSCManager,
    __in LPTSTR     ServiceName
);

BOOLEAN
ManageService(
    __in LPTSTR  ServiceName,
    __in LPTSTR  ServiceExe,
    __in USHORT   Function
);

void CloseDll();