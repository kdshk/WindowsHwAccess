/*++

2008-2020  NickelS

Module Name:

    AcpiLib.c

Abstract:

    Install on demanded start driver and place the driver into correct path.

Environment:

    User mode only.

--*/
#include "pch.h"
#include "acpilib.h"
#include "service.h"
#include "mdmap.h"

typedef ACPI_NAMESPACE ACPI_NS;
ACPI_NAMESPACE* GetAcpiNsFromNsAddr(PVOID pVoid);

VOID BuildAcpiNSData(
    UINT            MethodStartIndex,
    PACPI_NAMESPACE pRoot,
    UINT            uCount,
    PACPI_NAMESPACE pKernalParent,
    PACPI_NAMESPACE pActuallyParent
);

#pragma warning(disable:4996)   // to use GetVersion

TCHAR           DevInstanceId[201];
TCHAR           DevInstanceId1[201];
static HANDLE ghLocalDriver         = INVALID_HANDLE_VALUE;
PACPI_NAMESPACE pLocalAcpiNS = NULL;
UINT  uLocalAcpiNSCount      = 0;
UINT  uLocalMethodOffset     = METHOD_START_INDEX;

VOID
AmlParser(
    PACPI_NAMESPACE pNode,
    char* Scope
);

UINT64
CopyAslCode(
    TCHAR *pAsl
);

ACPI_NS*
APIENTRY
GetAcpiNS(
    TCHAR* pParent,
    USHORT* puType
);

void 
ResetAmlPrintMem(
);

void
AmlAppend(
    _In_z_ _Printf_format_string_ char const* const _Format,
    ...);

VOID
ParseReturnData(
    PACPI_NAMESPACE pAcpiName,
    PACPI_EVAL_OUTPUT_BUFFER pAcpiData
);

void CloseDll()
{
    CloseAcpiService(ghLocalDriver);
}

void ApiError(DWORD dwErr, TCHAR* Title)
{

    TCHAR   wszMsgBuff[512];  // Buffer for text.

    DWORD   dwChars;  // Number of chars returned.

    // Try to get the message from the system errors.
    dwChars = FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM |
        FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL,
        dwErr,
        0,
        wszMsgBuff,
        512,
        NULL);

    if (0 == dwChars)
    {
        // The error code did not exist in the system errors.
        // Try Ntdsbmsg.dll for the error code.

        HINSTANCE hInst;

        // Load the library.
        hInst = LoadLibrary(_T("Ntdsbmsg.dll"));
        if (NULL == hInst)
        {
            printf("cannot load Ntdsbmsg.dll\n");
            return;
        }

        // Try getting message text from ntdsbmsg.
        dwChars = FormatMessage(FORMAT_MESSAGE_FROM_HMODULE |
            FORMAT_MESSAGE_IGNORE_INSERTS,
            hInst,
            dwErr,
            0,
            wszMsgBuff,
            512,
            NULL);

        // Free the library.
        FreeLibrary(hInst);

    }

    //// Display the error message, or generic text if not found.
    //if (sizeof(TCHAR) == sizeof(CHAR)) {
    //    printf("A Error value: %d Message: %s\r\n",
    //        dwErr,
    //        (char *) (dwChars ? (char*)wszMsgBuff : "Error message not found."));
    //}
    //else {
    //    wprintf(L"T Error value: %d Message: %ls\r\n",
    //        dwErr,
    //        dwChars ? wszMsgBuff : L"Error message not found.");
    //    
    //}
    //MessageBox(NULL, dwChars ? wszMsgBuff : _T("Error message not found."), Title, MB_OK);
}

BOOLEAN
InstallService(
    __in SC_HANDLE SchSCManager,
    __in LPTSTR    ServiceName,
    __in LPTSTR    ServiceExe
    )
/*++

Routine Description:

    create a on-demanded service 

Arguments:

    SchSCManager    - Handle of service control manager
    ServiceName     - Service Name to create
    ServiceExe      - Service Excute file path

Return Value:
    
    TRUE            - Service create and installed succesfully
    FALSE           - Failed to create service

--*/
{
    SC_HANDLE   schService;
    DWORD       err;

    //
    // NOTE: This creates an entry for a standalone driver. If this
    //       is modified for use with a driver that requires a Tag,
    //       Group, and/or Dependencies, it may be necessary to
    //       query the registry for existing driver information
    //       (in order to determine a unique Tag, etc.).
    //

    //
    // Create a new a service object.
    //

    schService = CreateService(SchSCManager,           // handle of service control manager database
                               ServiceName,            // address of name of service to start
                               ServiceName,            // address of display name
                               SERVICE_ALL_ACCESS,     // type of access to service
                               SERVICE_KERNEL_DRIVER,  // type of service
                               SERVICE_DEMAND_START,   // when to start service
                               SERVICE_ERROR_NORMAL,   // severity if service fails to start
                               ServiceExe,             // address of name of binary file
                               NULL,                   // service does not belong to a group
                               NULL,                   // no tag requested
                               NULL,                   // no dependency names
                               NULL,                   // use LocalSystem account
                               NULL                    // no password for service account
                               );

    if (schService == NULL) {

        err = GetLastError();

        if (err == ERROR_SERVICE_EXISTS) {
            //
            // Ignore this error.
            //
            return TRUE;
        } else {
            printf("CreateService failed!  Error = %d \n", err );
            //
            // Indicate an error.
            //
            return  FALSE;
        }
        ApiError(err,_T("InstallService"));
    }

    //
    // Close the service object.
    //
    if (schService) {

        CloseServiceHandle(schService);
    }

    //
    // Indicate success.
    //
    return TRUE;
}

BOOLEAN
ManageService(
    __in LPTSTR  ServiceName,
    __in LPTSTR  ServiceExe,
    __in USHORT  Function
    )
/*++

Routine Description:

    Manager the driver to install, stop or remove

Arguments:

    ServiceName     - Service Name to create
    ServiceExe      - Service Excute file path
    Function        - Target function in install, stop or remove

Return Value:
    
    TRUE            - Service Manager successfully
    FALSE           - Failed to manager Service

--*/
{

    SC_HANDLE   schSCManager = NULL;

    BOOLEAN rCode = TRUE;

    //
    // Insure (somewhat) that the driver and service names are valid.
    //
    if (!ServiceExe || !ServiceName) {
        printf("Invalid Driver or Service provided to ManageDriver() \n");
        return FALSE;
    }

    //
    // Connect to the Service Control Manager and open the Services database.
    //

    schSCManager = OpenSCManager(
        NULL,                   // local machine
        NULL,                   // local database
        SC_MANAGER_ALL_ACCESS   // access required
    );

    if (!schSCManager) {

        //printf("Open SC Manager failed! Error = %d \n", GetLastError());
        ApiError(GetLastError(), _T("Open SC Manager failed"));
        return FALSE;
    }

    //
    // Do the requested function.
    //

    switch( Function ) {

        case DRIVER_FUNC_INSTALL:

            //
            // Install the driver service.
            //         
            
            if (InstallService(schSCManager,
                              ServiceName,
                              ServiceExe
                              )) {

                //
                // Start the driver service (i.e. start the driver).
                //
                rCode = DemandService(
                    schSCManager,
                    ServiceName
                );
            } else {
                //
                // Indicate an error.
                //
                rCode = FALSE;
            }
            break;
        case DRIVER_FUNC_REMOVE:

            //
            // Stop the driver.
            //

            StopService(
                schSCManager,
                ServiceName
            );

            //
            // Remove the driver service.
            //

            RemoveService(
                schSCManager,
                ServiceName
            );

            //
            // Ignore all errors.
            //

            rCode = TRUE;

            break;

        default:

            printf("Unknown ManageDriver() function. \n");

            rCode = FALSE;

            break;
    }

    //
    // Close handle to service control manager.
    //

    if (schSCManager) {

        CloseServiceHandle(schSCManager);
    }

    return rCode;

}   // ManageDriver


BOOLEAN
RemoveService(
    __in SC_HANDLE SchSCManager,
    __in LPTSTR    ServiceName
    )
/*++

Routine Description:

    Remove service 

Arguments:

    SchSCManager    - Handle of service control manager
    ServiceName     - Service Name to remove    

Return Value:
    
    TRUE            - Remove Service Successfully
    FALSE           - Failed to Remove Service

--*/
{
    SC_HANDLE   schService;
    BOOLEAN     rCode;

    //
    // Open the handle to the existing service.
    //

    schService = OpenService(
        SchSCManager,
        ServiceName,
        SERVICE_ALL_ACCESS
    );

    if (schService == NULL) {
        printf("OpenService failed!  Error = %d \n", GetLastError());
        //
        // Indicate error.
        //
        return FALSE;
    }

    //
    // Mark the service for deletion from the service control manager database.
    //
    if (DeleteService(schService)) {

        //
        // Indicate success.
        //
        rCode = TRUE;

    } else {
        //printf("DeleteService failed!  Error = %d \n", GetLastError());
        ApiError(GetLastError(), _T("DeleteService failed"));
        //
        // Indicate failure.  Fall through to properly close the service handle.
        //
        rCode = FALSE;
    }

    //
    // Close the service object.
    //
    if (schService) {

        CloseServiceHandle(schService);
    }

    return rCode;

}  

BOOLEAN
DemandService(
    __in SC_HANDLE SchSCManager,
    __in LPTSTR    ServiceName
    )
/*++

Routine Description:

    Start service on demanded

Arguments:

    SchSCManager    - Handle of service control manager
    ServiceName     - Service Name to start    

Return Value:
    
    TRUE            - Start Service Successfully
    FALSE           - Failed to Start Service

--*/
{
    SC_HANDLE   schService;
    DWORD       err;

    //
    // Open the handle to the existing service.
    //

    schService = OpenService(SchSCManager,
        ServiceName,
        SERVICE_ALL_ACCESS
    );

    if (schService == NULL) {

        //printf("OpenService failed!  Error = %d \n", GetLastError());
        ApiError(GetLastError(), _T("OpenService failed"));
        //
        // Indicate failure.
        //

        return FALSE;
    }

    //
    // Start the execution of the service (i.e. start the driver).
    //

    if (!StartService(schService,     // service identifier
                      0,              // number of arguments
                      NULL            // pointer to arguments
                      )) {

        err = GetLastError();

        if (err == ERROR_SERVICE_ALREADY_RUNNING) {

            //
            // Ignore this error.
            //

            return TRUE;

        } else {

            //printf("StartService failure! Error = %d \n", err );
            ApiError(err, _T("StartService failed"));

            //
            // Indicate failure.  Fall through to properly close the service handle.
            //

            return FALSE;
        }

    }

    //
    // Close the service object.
    //

    if (schService) {

        CloseServiceHandle(schService);
    }

    return TRUE;

} 



BOOLEAN
StopService(
    __in SC_HANDLE SchSCManager,
    __in LPTSTR    ServiceName
    )
/*++

Routine Description:

    Start service on demanded

Arguments:

    SchSCManager    - Handle of service control manager
    ServiceName     - Service Name to Stop    

Return Value:
    
    TRUE            - Start Service Successfully
    FALSE           - Failed to Start Service

--*/
{
    BOOLEAN         rCode = TRUE;
    SC_HANDLE       schService;
    SERVICE_STATUS  serviceStatus;

    //
    // Open the handle to the existing service.
    //

    schService = OpenService(SchSCManager,
        ServiceName,
        SERVICE_ALL_ACCESS
    );

    if (schService == NULL) {

        //printf("OpenService failed!  Error = %d \n", GetLastError());
        ApiError(GetLastError(), _T("OpenService failed"));
        return FALSE;
    }

    //
    // Request that the service stop.
    //

    if (ControlService(schService,
                       SERVICE_CONTROL_STOP,
                       &serviceStatus
                       )) {

        //
        // Indicate success.
        //

        rCode = TRUE;

    } else {

        //printf("ControlService failed!  Error = %d \n", GetLastError() );
        ApiError(GetLastError(), _T("ControlService failed"));

        //
        // Indicate failure.  Fall through to properly close the service handle.
        //

        rCode = FALSE;
    }

    //
    // Close the service object.
    //

    if (schService) {

        CloseServiceHandle (schService);
    }

    return rCode;

}   

BOOLEAN
GetSerivceName(
    __inout_bcount_full(BufferLength) TCHAR *DriverLocation,
    __in ULONG BufferLength
    )
/*++

Routine Description:

    Get the current full path service name for install/stop/remove service

Arguments:

    DriverLocation  - Buffer to receive location of service
    BufferLength    - Buffer size  

Return Value:
    
    TRUE            - Get service full path successfully
    FALSE           - Failed to get service full path

--*/
{
    HANDLE  fileHandle;
    TCHAR   driver[MAX_PATH];
	TCHAR   file[MAX_PATH];
    size_t  pcbLength = 0;
    size_t  Idx;

    if (DriverLocation == NULL || BufferLength < 1) {
        return FALSE;
    }

    if (GetSystemDirectory(DriverLocation, BufferLength - 1) == 0) {
        return FALSE;
    }
    if (GetCurrentDirectory(MAX_PATH, driver) == 0)
    {
        return FALSE;
    }
    GetModuleFileName (NULL, file, MAX_PATH - 1);

    if (FAILED(StringCbLength(file, MAX_PATH, &pcbLength)) || pcbLength == 0) {
        return FALSE;
    }
    pcbLength = pcbLength / sizeof(TCHAR);
	for (Idx = (pcbLength) - 1; Idx > 0; Idx --) {
	    if (file[Idx] == '\\') {
		    file[Idx + 1]  = 0;
		    break;
	    }
	}

    if (FAILED(StringCbCat(file, MAX_PATH, _T("HwAcc.sys")))) {
        return FALSE;
    }

    // test file is existing
    if ((fileHandle = CreateFile(file,
        GENERIC_READ,
        0,
        NULL,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        NULL
    )) == INVALID_HANDLE_VALUE) {

        //
        // Indicate failure.
        //
        return FALSE;
    }

    //
    // Close open file handle.
    //
    if (fileHandle) {

        CloseHandle(fileHandle);
    }
	  //MessageBox (NULL, file, file, MB_OK);

    //
    // Setup path name to driver file.
    //
    if (FAILED(StringCbCat(driver, MAX_PATH, _T("\\HwAcc.sys")))) {
        return FALSE;
    }

    /*if (GetLastError () == 0) {
        
        StringCbCat (DriverLocation, MAX_PATH, _T("\\HwAcc.sys"));        
    }*/    

    StringCbPrintf (DriverLocation,BufferLength, file);
    return TRUE;
}

BOOL
GetAcpiDevice (
    PCTSTR Name, 
    LPBYTE PropertyBuffer, 
    DWORD Idx
)
/*++

Routine Description:

    Get the current full path service name for install/stop/remove service

Arguments:

    DriverLocation  - Buffer to receive location of service
    BufferLength    - Buffer size

Return Value:

    TRUE            - Get service full path successfully
    FALSE           - Failed to get service full path

--*/
{
    HDEVINFO    hdev;
    SP_DEVINFO_DATA devdata;    
    DWORD       PropertyRegDataType;
    DWORD       RequiedSize;
    
    hdev = SetupDiGetClassDevsEx (NULL, Name, NULL, DIGCF_ALLCLASSES, NULL, NULL, NULL);
    
        if (hdev != INVALID_HANDLE_VALUE) {
            if (TRUE) {
                ZeroMemory (&devdata, sizeof (devdata));
                devdata.cbSize = sizeof (devdata);      
                if (SetupDiEnumDeviceInfo (hdev, Idx, &devdata)) {      
                    if (SetupDiGetDeviceInstanceId (hdev, & devdata, &DevInstanceId[0], 200, NULL)) {
                        //printf (DevInstanceId);
                        CopyMemory (DevInstanceId1, DevInstanceId, 201);
                        if (SetupDiGetDeviceRegistryProperty (hdev, & devdata, 0xE, 
                                &PropertyRegDataType, &PropertyBuffer[0],0x400, &RequiedSize)) {
                            //printf (PropertyBuffer);                 
                            //printf ("\n");
                        } else {
                            //printf ("Failed to call SetupDiGetDeviceRegistryProperty\n");
                        }
                    } else {
                        //printf ("Failed to call SetupDiGetDeviceInstanceId\n");
                    }
                } else {
                    SetupDiDestroyDeviceInfoList (hdev);    
                    //printf ("Failed to call SetupDiEnumDeviceInfo\n");
                    return FALSE;
                }       
                SetupDiDestroyDeviceInfoList (hdev);    
            }
        } else {
            
            //printf ("Failed to call SetupDiGetClassDevsEx\n");
            return FALSE;
        }
    return TRUE;
}

HANDLE
APIENTRY
OpenAcpiService(
)
/*++

Routine Description:

    create, start and open service

Arguments:


Return Value:

    Handle                  - Get service full path successfully
    Invalid_Handle_value    - Failed to get service full path

--*/ 
{
    HANDLE hndFile;
    DWORD  errNum;
    TCHAR  driverLocation[MAX_PATH];
       
    hndFile = CreateFile(
        _T("\\\\.\\HwAcc"),
        GENERIC_READ,
        FILE_SHARE_READ,
        NULL,
        OPEN_EXISTING,
        0,
        NULL
    );

    if (hndFile == INVALID_HANDLE_VALUE) {
        errNum = GetLastError();
        
        if (errNum != ERROR_FILE_NOT_FOUND) {
            //printf("CreateFile failed!  ERROR_FILE_NOT_FOUND = %d\n", errNum);
            ApiError(errNum, _T("OpenAcpiService"));
            return INVALID_HANDLE_VALUE;
        }

        //
        // The driver is not started yet so let us the install the driver.
        // First setup full path to driver name.
        //
        if (!GetSerivceName(driverLocation, MAX_PATH)) {

            return INVALID_HANDLE_VALUE;
        }

        if (!ManageService(_T("HwAcc"),
            driverLocation,
            DRIVER_FUNC_INSTALL
            )) {

            //
            // Error - remove driver.
            //
            ManageService(_T("HwAcc"),
                driverLocation,
                DRIVER_FUNC_REMOVE
                );
            return INVALID_HANDLE_VALUE;
        }

        hndFile = CreateFile(
            _T("\\\\.\\HwAcc"),
            GENERIC_READ | GENERIC_WRITE,
            0,
            NULL,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            NULL);

        if ( hndFile == INVALID_HANDLE_VALUE ){
            //MessageBox (NULL, "Failed to load HwAcc Driver", "Error", MB_ICONSTOP);
            return INVALID_HANDLE_VALUE ;
        }

        if (!OpenAcpiDevice(hndFile))
        {
            CloseHandle(hndFile);
            hndFile = INVALID_HANDLE_VALUE;
        }
        else {
            ghLocalDriver = hndFile;
        }
    }
    return hndFile;
}

BOOLEAN
APIENTRY
OpenAcpiDevice (
    __in HANDLE hDriver
    )
/*++

Routine Description:

    Open Acpi Device

Arguments:

    hDriver     - Handle of service

Return Value:

    TRUE        - Open ACPI driver acpi.sys ready
    FALSE       - Failed to open acpi driver

--*/ 
{
    UINT        Idx;   
    BYTE        PropertyBuffer[0x401];
    TCHAR       *pChar;
    CHAR        AcpiName[0x401];
    BOOL        IoctlResult = FALSE;
    ACPI_NAME   acpi;
    ULONG       ReturnedLength = 0;
    OSVERSIONINFO osinfo;
    osinfo.dwOSVersionInfoSize = sizeof (OSVERSIONINFO);
    if (!GetVersionEx (&osinfo)) {
    //  printf ("OS Major Version is %d, Minor Version is %d", osinfo.dwMajorVersion, osinfo.dwMinorVersion);
      return FALSE;
    }

    acpi.dwMajorVersion = osinfo.dwMajorVersion;
    acpi.dwMinorVersion = osinfo.dwMinorVersion;
    acpi.dwBuildNumber  = osinfo.dwBuildNumber;
    acpi.dwPlatformId   = osinfo.dwPlatformId;
    acpi.pAcpiDeviceName = PropertyBuffer;
    Idx = 0;    
    
    while (GetAcpiDevice (_T("ACPI_HAL"), PropertyBuffer, Idx)) {           
        if (Idx >= 1) {
            //break;
        }
        Idx ++;

        if (sizeof(TCHAR) == sizeof(WCHAR)) {
            // unicode defined, need to change from unicode to uchar code..
            pChar = (TCHAR*)PropertyBuffer;
            WideCharToMultiByte(CP_UTF8, WC_COMPOSITECHECK, pChar, 400, AcpiName, 400, NULL, NULL); 
            acpi.pAcpiDeviceName = AcpiName;
        }
        
        acpi.uAcpiDeviceNameLength = (ULONG)strlen (acpi.pAcpiDeviceName);
       
        IoctlResult = DeviceIoControl(
                                    hDriver,            // Handle to device
                                    (DWORD)IOCTL_GPD_OPEN_ACPI,    // IO Control code for Read
                                    &acpi,        // Buffer to driver.
                                    sizeof (ACPI_NAME), // Length of buffer in bytes.
                                    NULL,       // Buffer from driver.
                                    0,      // Length of buffer in bytes.
                                    &ReturnedLength,    // Bytes placed in DataBuffer.
                                    NULL                // NULL means wait till op. completes.
                                    );
        if (IoctlResult) {
            break;
        }
    }

    if (!IoctlResult) {
        return FALSE;
        }
    return TRUE;
}


VOID
APIENTRY
CloseAcpiService(
    HANDLE hDriver
)
/*++

Routine Description:

    Close and remove driver

Arguments:


Return Value:
    

--*/
{
    TCHAR       driverLocation[MAX_PATH];
    ReleaseMethodMap();
    ReleaseAcpiNS();
    if (hDriver != INVALID_HANDLE_VALUE) {
        CloseHandle(hDriver);
		ghLocalDriver = INVALID_HANDLE_VALUE;
    }
    if (GetSerivceName(driverLocation, MAX_PATH)) {
        ManageService(_T("HwAcc"),
            driverLocation,
            DRIVER_FUNC_REMOVE
        );
    }
}


void
RemoveAcpiService(
)
/*++

Routine Description:

    Remove driver

Arguments:


Return Value:


--*/
{
    TCHAR       driverLocation[MAX_PATH];    
    if (GetSerivceName(driverLocation, MAX_PATH)) {
        ManageService(_T("HwAcc"),
            driverLocation,
            DRIVER_FUNC_REMOVE
        );
    }
}

BOOL
APIENTRY
LoadNotifiyMethod(
    HANDLE      hDriver,
    AML_SETUP*  pAmlSetup,
    ULONG       uSize
)
/*++

Routine Description:

    Load dynamic notification AML Code

Arguments:
    
    hDriver     -- Handle of service driver
    pAmlSetup   -- Aml Code size, offset, path and notifiation Code

Return Value:
    
    TRUE        -- Load succesfully
    FALSE       -- Failed to load notify AML Code

--*/
{
    DWORD ReadCnt;
    DWORD ReturnedLength;
    //AML_SETUP amlSetup = { 0x100, 0xC1 };
    //amlSetup.ulCode = ulCode;
    //StringCbPrintf(amlSetup.Name, 255, Path);
    return DeviceIoControl(
        hDriver,                // Handle to device
        IOCTL_GPD_LOAD_AML,     // IO Control code for Read
        pAmlSetup,              // Buffer to driver.
        uSize,                  // Length of buffer in bytes.
        &ReadCnt,               // Buffer from driver.
        sizeof(ReadCnt),        // Length of buffer in bytes.
        &ReturnedLength,        // Bytes placed in DataBuffer.
        NULL                    // NULL means wait till op. completes.
    );
}

void
APIENTRY
UnloadNotifiyMethod(
    HANDLE      hDriver
)
/*++

Routine Description:

    Unload dynamic notification AML Code

Arguments:

    hDriver     -- Handle of service driver
   

Return Value:

 
--*/
{
    DWORD ReadCnt;
    DWORD ReturnedLength;
    DeviceIoControl(
        hDriver,                // Handle to device
        IOCTL_GPD_UNLOAD_AML,     // IO Control code for Read
        NULL,              // Buffer to driver.
        0,                  // Length of buffer in bytes.
        &ReadCnt,               // Buffer from driver.
        sizeof(ReadCnt),        // Length of buffer in bytes.
        &ReturnedLength,        // Bytes placed in DataBuffer.
        NULL                    // NULL means wait till op. completes.
    );
}


UINT64
APIENTRY
EvalAcpiNSAndParse(
    TCHAR   *pPath,
    TCHAR   *pAsl
    )
/*++

Routine Description:

    Eval acpi name space by path and parse the output

Arguments:

    hDriver     -- Handle of service driver
    pPath       -- Path of Acpi Namespace

Return Value:

    TRUE        -- Load succesfully
    FALSE       -- Failed to load notify AML Code

--*/
{
    UINT64 rc = 0;
    if (pAsl != NULL) {
        return CopyAslCode(pAsl);
    }
    
    PACPI_EVAL_OUTPUT_BUFFER pAcpiData = NULL;
    ACPI_NS* pAcpiName = GetAcpiNS(pPath, NULL);
    if (pAcpiName == NULL) {
        return 0;
    }
    ResetAmlPrintMem();
    if (ghLocalDriver == INVALID_HANDLE_VALUE) {
        AmlAppend("Driver is not loaded\n");
        return CopyAslCode(pAsl);
    }

    if (EvalAcpiNS(ghLocalDriver, pAcpiName, &pAcpiData, NULL)) {        
        ParseReturnData(pAcpiName, pAcpiData);
        rc = CopyAslCode(pAsl);
    }
    if (pAcpiData != NULL) {
        free(pAcpiData);
    }
    return rc;
}

BOOL
APIENTRY
EvalAcpiNS(
    HANDLE          hDriver,
    ACPI_NAMESPACE* pAcpiName,
    PVOID*          pReturnData,
    ULONG*          puLength)
/*++

Routine Description:

    Eval acpi name space include method or other data name space

Arguments:
    
    hDriver     -- Handle of service driver
    pAcpiName   -- Acpi NameNS
    pReturnData -- Receive for result data
    puLength    -- Length of received data

Return Value:
    
    TRUE        -- Load succesfully
    FALSE       -- Failed to load notify AML Code

--*/
{
    BOOL                        IoctlResult;
    DWORD                       ReturnedLength, LastError;
    ACPI_EVAL_OUTPUT_BUFFER     DataNeeded;
    PACPI_EVAL_OUTPUT_BUFFER    ActualData;


    IoctlResult = DeviceIoControl(
        hDriver,                               // Handle to device
        IOCTL_GPD_EVAL_ACPI_WITHOUT_PARAMETER,     // IO Control code for Read
        pAcpiName,                              // Buffer to driver.
        sizeof(ACPI_NAMESPACE),               // Length of buffer in bytes.
        &DataNeeded,                            // Buffer from driver.
        sizeof(ACPI_EVAL_OUTPUT_BUFFER),        // Length of buffer in bytes.
        &ReturnedLength,                        // Bytes placed in DataBuffer.
        NULL                                    // NULL means wait till op. completes.
    );

    if (!IoctlResult) {
        LastError = GetLastError();
        if (LastError == 0xEA) {
            ActualData = (PACPI_EVAL_OUTPUT_BUFFER)malloc(DataNeeded.Length);
            if (puLength != NULL) {
                *puLength = DataNeeded.Length;
            }
            if (ActualData == NULL) {
                return FALSE;
            }
            IoctlResult = DeviceIoControl(
                hDriver,           // Handle to device
                IOCTL_GPD_EVAL_ACPI_WITHOUT_PARAMETER,    // IO Control code for Read
                pAcpiName,        // Buffer to driver.
                sizeof(ACPI_NAMESPACE), // Length of buffer in bytes.
                ActualData,     // Buffer from driver.
                DataNeeded.Length,
                &ReturnedLength,    // Bytes placed in DataBuffer.
                NULL                // NULL means wait till op. completes.
            );
            if (IoctlResult) {
                if (pReturnData != NULL) {
                    (*pReturnData) = (PVOID)ActualData;
                    if (ActualData->Signature != ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE)
                    {
                        (*pReturnData) = NULL;
                    }
                    return TRUE;
                }
            }
            //free (ActualData);
            //
        }
        else if (LastError == 2) {
            //MessageBox(ghMainWnd, "Internal ACPI Error! Necessary name space is not valid or not exist!", "ERROR", MB_ICONSTOP);
            //PrintCSBackupAPIErrorMessage(LastError, "EvalAcpiNS");
        }
        else if (LastError == 0x29d) {
            //MessageBox(ghMainWnd, "Internal ACPI Error! Method may be excuted successfully.", "WARNING", MB_OK);
            //PrintCSBackupAPIErrorMessage(LastError, "EvalAcpiNS");
        }
        else {
            //MessageBox(ghMainWnd, "Unknow Error!", "ERROR", MB_ICONSTOP);
           // PrintCSBackupAPIErrorMessage(LastError, "EvalAcpiNS");
        }
    }
    else {
        if (pReturnData != NULL) {
            if (ReturnedLength == 0) {
                (*pReturnData) = NULL;
                return TRUE;
            }
            if (DataNeeded.Signature != ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE)
            {
                (*pReturnData) = NULL;
                return TRUE;
            }
            (*pReturnData) = malloc(DataNeeded.Length);
            if (puLength != NULL) {
                *puLength = DataNeeded.Length;
            }
            if ((*pReturnData) != NULL) {
                memcpy((*pReturnData), &DataNeeded, DataNeeded.Length);
            }
            else {
                return FALSE;
            }
        }
    }
    return IoctlResult;
}

void
APIENTRY
FreeMemory(
    PVOID pMem
)
/*++

Routine Description:

    Free dynamic allocated memory

Arguments:

    pMem     -- memory resource ptr

Return Value:



--*/
{
    if (pMem != NULL) {
        free(pMem);
    }
}

UINT64
APIENTRY
EvalAcpiNSArgAndParse(
    TCHAR* pPath,
    ACPI_EVAL_INPUT_BUFFER_COMPLEX *pComplexInput,
    TCHAR* pAsl
)
/*++

Routine Description:

    Eval acpi name space by path and parse the output

Arguments:

    hDriver     -- Handle of service driver
    pPath       -- Path of Acpi Namespace

Return Value:

    TRUE        -- Load succesfully
    FALSE       -- Failed to load notify AML Code

--*/
{
    ACPI_METHOD_ARG_COMPLEX *pComplexData;
    UINT64 rc = 0;
    if (pAsl != NULL) {
        return CopyAslCode(pAsl);
    }

    PACPI_EVAL_OUTPUT_BUFFER pAcpiData = NULL;
    ACPI_NS* pAcpiName = GetAcpiNS(pPath, NULL);
    if (pAcpiName == NULL) {
        return 0;
    }
    ResetAmlPrintMem();
    if (ghLocalDriver == INVALID_HANDLE_VALUE) {
        AmlAppend("Driver is not loaded\n");
        return CopyAslCode(pAsl);
    }

    pComplexData = malloc(sizeof(ACPI_METHOD_ARG_COMPLEX)+ pComplexInput->Size);

    if (pComplexData == NULL) {
        AmlAppend("Failed to allocate resource\n");
        return CopyAslCode(pAsl);
    }
    pComplexData->NameSpace = *pAcpiName;
    memcpy(&pComplexData->InputBufferComplex, pComplexInput, pComplexInput->Size);

    pComplexData->InputBufferComplex.MethodNameAsUlong = pAcpiName->MethodNameAsUlong;

    if (EvalAcpiNSArg(ghLocalDriver, pComplexData, &pAcpiData, pComplexData->InputBufferComplex.Size + sizeof(ACPI_NAMESPACE))) {
		if (pAcpiName == NULL) {
			AmlAppend("Run args failed with output");
		}
        ParseReturnData(pAcpiName, pAcpiData);
        rc = CopyAslCode(pAsl);
		if (rc == 0) {
			AmlAppend("Output is empty");
			rc = CopyAslCode(pAsl);
		}
	}
	else {
		//AmlAppend("Failed to evaluate method with args");
		rc = CopyAslCode(pAsl);
	}
    if (pAcpiData != NULL) {
        free(pAcpiData);
    }
    if (pComplexData != NULL) {
        free(pComplexData);
    }
    return rc;
}


PVOID
APIENTRY
EvalAcpiNSArgOutput(
    TCHAR*                          pPath,
    ACPI_EVAL_INPUT_BUFFER_COMPLEX* pComplexInput
)
/*++

Routine Description:

    Eval acpi name space by path and return output

Arguments:

    pPath       -- Path of Acpi Namespace

Return Value:

    The output data of eval resutl
    NULL indicate the failure of method running

--*/
{
    ACPI_METHOD_ARG_COMPLEX* pComplexData;
    UINT64 rc = 0;   

    PACPI_EVAL_OUTPUT_BUFFER pAcpiData = NULL;
    ACPI_NS* pAcpiName = GetAcpiNS(pPath, NULL);
    if (pAcpiName == NULL) {
        return NULL;
    }   
    pComplexData = malloc(sizeof(ACPI_METHOD_ARG_COMPLEX) + pComplexInput->Size);
    if (pComplexData == NULL) {        
        return NULL;
    }
    pComplexData->NameSpace = *pAcpiName;
    memcpy(&pComplexData->InputBufferComplex, pComplexInput, pComplexInput->Size);
    pComplexData->InputBufferComplex.MethodNameAsUlong = pAcpiName->MethodNameAsUlong;
    if (EvalAcpiNSArg(ghLocalDriver, pComplexData, &pAcpiData, pComplexData->InputBufferComplex.Size + sizeof(ACPI_NAMESPACE))) 
    {
        return pAcpiData;
    }   
    if (pAcpiData != NULL) {
        free(pAcpiData);
    }    
    return NULL;
}

PVOID
APIENTRY
EvalAcpiNSOutput(
    TCHAR* pPath
)
/*++

Routine Description:

    Eval acpi name space by path and return output

Arguments:

    pPath       -- Path of Acpi Namespace

Return Value:

    The output data of eval resutl
    NULL indicate the failure of method running

--*/
{
    PACPI_EVAL_OUTPUT_BUFFER pAcpiData = NULL;
    ACPI_NS* pAcpiName = GetAcpiNS(pPath, NULL);
    if (pAcpiName == NULL) {
        return NULL;
    }
    //if (EvalAcpiNS(ghLocalDriver, pAcpiName, &pAcpiData, NULL)) {      
    if (EvalAcpiNS(ghLocalDriver, pAcpiName, &pAcpiData, NULL))
    {
        return pAcpiData;
    }
    if (pAcpiData != NULL) {
        free(pAcpiData);
    }
    return NULL;
}

BOOL
APIENTRY
EvalAcpiNSArg(
    HANDLE                   hDriver,
    PACPI_METHOD_ARG_COMPLEX pComplexData, 
    PVOID*                   pReturnData, 
    UINT                     Size
)
/*++

Routine Description:

    Eval acpi method with args

Arguments:
    
    hDriver         -- Handle of service driver
    pComplexData    -- Acpi Namespace and Args
    pReturnData     -- Receive for result data
    puLength        -- Length of received data

Return Value:
    
    TRUE        -- Load succesfully
    FALSE       -- Failed to load notify AML Code

--*/
{
    //
    // Display the data
    //
    UINT                        Index;
    PACPI_METHOD_ARGUMENT       pNext;
    BOOL                        IoctlResult;
    DWORD                       ReturnedLength, LastError;
    ACPI_EVAL_OUTPUT_BUFFER     DataNeeded;
    PACPI_EVAL_OUTPUT_BUFFER    ActualData;


    pNext = pComplexData->InputBufferComplex.Argument;
    for (Index = 0; Index < pComplexData->InputBufferComplex.ArgumentCount; Index++) {
        switch (pNext->Type) {
        case ACPI_METHOD_ARGUMENT_INTEGER:
            //sprintf (msg, "Integer data 0x%X", pNext->Argument);
            break;
        case ACPI_METHOD_ARGUMENT_STRING:
            //
            // Calucate the data 
            //
            //sprintf (msg, "String data %s", pNext->Data);
            break;
        case ACPI_METHOD_ARGUMENT_BUFFER:
            //sprintf (msg, "Buffer data 0x%X", pNext->Argument);

            break;
        default:
            assert(0);
            break;
        }
        pNext = ACPI_METHOD_NEXT_ARGUMENT(pNext);
    }
    pComplexData->InputBufferComplex.Signature = ACPI_EVAL_INPUT_BUFFER_COMPLEX_SIGNATURE;
    pComplexData->InputBufferComplex.Size = (ULONG)((UINTN)(pNext)-(UINTN)(&pComplexData->InputBufferComplex));

    IoctlResult = DeviceIoControl(
        hDriver,                                // Handle to device
        IOCTL_GPD_EVAL_ACPI_WITH_PARAMETER,     // IO Control code for Read
        pComplexData,                           // Buffer to driver.
        Size,                                   // Length of buffer in bytes.
        &DataNeeded,                            // Buffer from driver.
        sizeof(ACPI_EVAL_OUTPUT_BUFFER),        // Length of buffer in bytes.
        &ReturnedLength,                        // Bytes placed in DataBuffer.
        NULL                                    // NULL means wait till op. completes.
    );

    if (!IoctlResult) {
        LastError = GetLastError();
		//ApiError(LastError,_T(""));
        if (LastError == 0xEA) {
            ActualData = (PACPI_EVAL_OUTPUT_BUFFER)malloc(DataNeeded.Length);
            if (ActualData == NULL) {
                return FALSE;
            }
            IoctlResult = DeviceIoControl(
                hDriver,           // Handle to device
                IOCTL_GPD_EVAL_ACPI_WITH_PARAMETER,    // IO Control code for Read
                pComplexData,        // Buffer to driver.
                Size, // Length of buffer in bytes.
                ActualData,     // Buffer from driver.
                DataNeeded.Length,
                &ReturnedLength,    // Bytes placed in DataBuffer.
                NULL                // NULL means wait till op. completes.
            );
            if (IoctlResult) {
                //sprintf (debug, "Count = %d Length = %d Type = %d ", 
                //    ActualData->Count, ActualData->Length, ActualData->Argument[0].Type);
                //MessageBox(ghMainWnd, debug, "Oops!", MB_OK);
                if (pReturnData != NULL) {
                    (*pReturnData) = (PVOID)ActualData;
                    if (ActualData->Signature != ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE)
                    {
                        (*pReturnData) = NULL;
                        return TRUE;
                    }
                    return TRUE;
                }
            }
            //free (ActualData);
        }
        else if (LastError == 2) {
            //PrintCSBackupAPIErrorMessage(LastError, "EvalAcpiNameWithParaRtl");
			AmlAppend("Acpi Subsystem Error: Invalid Acpi Namespace Path");
        }
        else if (LastError == 0x29d) {
            //PrintCSBackupAPIErrorMessage(LastError, "EvalAcpiNameWithParaRtl");
			AmlAppend("Acpi Subsystem Error: Caused by wrong argument");
        }
        else {
            //PrintCSBackupAPIErrorMessage(LastError, "EvalAcpiNameWithParaRtl");
			AmlAppend("Acpi Subsystem Error: Unknown error");
        }
    }
    else {
        if (pReturnData != NULL) {
            if (ReturnedLength == 0) {
                (*pReturnData) = NULL;
                return TRUE;
            }
            if (DataNeeded.Signature != ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE)
            {
                (*pReturnData) = NULL;
                return TRUE;
            }
            (*pReturnData) = malloc(DataNeeded.Length);
            if ((*pReturnData) != NULL) {
                memcpy((*pReturnData), &DataNeeded, DataNeeded.Length);
            }
            else {
                return FALSE;
            }
        }
    }
    return IoctlResult;
}

PVOID 
APIENTRY
ReadAcpiMemory(
    HANDLE hDriver,
    PVOID  Address, 
    ULONG  Size
)
/*++

Routine Description:

    Read Acpi Namespace memory

Arguments:

    hDriver         -- Handle of service driver
    Address         -- Acpi NS Address to read
    Size            -- Length of data to read

Return Value:

    Address         -- Acpi NS Memory readed

--*/
{
    BOOL            IoctlResult = TRUE;
    PVOID           pLocal = NULL;
    DWORD           ReturnedLength;

    pLocal = malloc(Size);

    if (pLocal == NULL) {
        return NULL;
    }


    IoctlResult = DeviceIoControl(
        hDriver,          // Handle to device
        (DWORD)IOCTL_GPD_READ_ACPI_MEMORY, // IO Control code for Read
        &Address,         // Buffer to driver.
        sizeof(PVOID), // Length of buffer in bytes.
        pLocal,     // Buffer from driver.
        Size, // Length of buffer in bytes.
        &ReturnedLength,    // Bytes placed in DataBuffer.
        NULL                // NULL means wait till op. completes.
    );

    if (!IoctlResult) {
        free(pLocal);
        pLocal = NULL;
    }
    return pLocal;
}



void 
BuildNotifyAml(
	UINT Offset
)

/*++

Routine Description:

	Setup Notify Acpi Namespace node

Arguments:

	Offset -- Method Start Index of method buffer

Return Value:

	Address         -- Acpi NS Memory readed

--*/ 
{
	DWORD ReadCnt;
	DWORD ReturnedLength;
	AML_SETUP amlSetup = { 0x100, 0xC1 };
	//AML_SETUP amlSetup = { 0x100, 0xC1 };
	amlSetup.dwOffset = Offset;
	amlSetup.dwSize = Offset / 0x100 + 0x100;
		
	if (!DeviceIoControl(
		ghLocalDriver,            // Handle to device
		IOCTL_GPD_LOAD_AML,    // IO Control code for Read
		&amlSetup,        // Buffer to driver.
		sizeof(amlSetup), // Length of buffer in bytes.
		&ReadCnt,       // Buffer from driver.
		sizeof(ReadCnt),      // Length of buffer in bytes.
		&ReturnedLength,    // Bytes placed in DataBuffer.
		NULL                // NULL means wait till op. completes.
	)) {

	}
}
BOOLEAN
APIENTRY
QueryAcpiNS(
    HANDLE          hDriver,
    ACPI_NS_DATA*   pAcpiNsData,
    UINT            MethodStartIndex
)
/*++

Routine Description:

    Read Acpi Namespace memory

Arguments:

    hDriver          -- Handle of service driver
    Address          -- Acpi NS Address to read
    Size             -- Length of data to read
    MethodStartIndex -- Method Start Index of method buffer

Return Value:

    Address         -- Acpi NS Memory readed

--*/
{
    uLocalMethodOffset = MethodStartIndex;
	BuildNotifyAml(uLocalMethodOffset);
    if(QueryAcpiNSInLib())
    {
        if (pAcpiNsData != NULL) {
            pAcpiNsData->pAcpiNS = pLocalAcpiNS;
            pAcpiNsData->uCount = uLocalAcpiNSCount;
            pAcpiNsData->uLength = uLocalAcpiNSCount * sizeof(ACPI_NAMESPACE);
        }
        return TRUE;
        
    }
    return FALSE;
}


VOID
InsertAcpiNS(
    __in PACPI_NAMESPACE OldTable,
    __in PACPI_NAMESPACE NewTable
)
/*++

Routine Description:

    ACPI NS Link builder

Arguments:

    OldTable        -- Insert before old table
    NewTable        -- Target table to insert

Return Value:

    

--*/
{
    //
    // pAcpiTableLink always point to the end of
    //
   

    if (NewTable == NULL) {
        return;
    }

    if (OldTable == NULL) {
        //
        // The first table
        //
        NewTable->pNext = NULL;
        NewTable->pPrev = NULL;
    }
    else {
        // Insert at tail
        PACPI_NAMESPACE pNext;
        pNext = OldTable;
        while (pNext->pNext != NULL) {
            pNext = pNext->pNext;
        }

        pNext->pNext = NewTable;
        NewTable->pPrev = pNext;
        NewTable->pNext = NULL;

       /* 
       // Insert at head
       NewTable->pNext = OldTable;
        OldTable->pPrev = NewTable;
        NewTable->pPrev = NULL;*/

    }
}

VOID BuildAcpiNSDataInternal(
    UINT            MethodStartIndex,
    PACPI_NAMESPACE pRoot,
    UINT            uCount,
    PACPI_NAMESPACE pKernalParent,
    PACPI_NAMESPACE pActuallyParent
)
/*++

Routine Description:

    Build the Acpi NS Data structre to user space memory related

Arguments:

    pRoot           -- Root of start acpi ns
    uCount          -- Size of acpi ns
    pKernalParent   -- Parent kernel address
    pActuallyParent -- Parent of user space address

Return Value:



--*/
{
    UINT                Index;
    PACPI_NAMESPACE     pNext = pRoot;
    PACPI_NAMESPACE     pFirst = NULL;

    BOOL                bFirst = TRUE;

    if (pRoot == NULL) {
        return;
    }
    for (Index = 0; Index < uCount; Index++) {
        if (pNext->pParentNameSpace == pKernalParent) {
            pNext->pParent = pActuallyParent;
            if (pActuallyParent != NULL && pActuallyParent->pChild == NULL) {
                pActuallyParent->pChild = pNext;
                bFirst = FALSE;
            }
            InsertAcpiNS(pFirst, pNext);
            if (pNext->pUserContain == NULL) {
                pNext->pUserContain = ReadAcpiMemory(ghLocalDriver, pNext->Contain, pNext->Length);
            }
            pFirst = pNext;
            if (pNext->Type == ACPI_TYPE_METHOD) {

                if (pNext->pUserContain != NULL) {
                    pNext->uFlags = ((UCHAR*)pNext->pUserContain)[0xC1];
                    AddMethodMap(pNext->MethodName, pNext->uFlags, pNext);
                }
            }
            //BuildAcpiNSData(MethodStartIndex, pRoot, uCount, pNext->pAddress, pNext);
        }
        pNext++;
    }
}


VOID BuildAcpiNSData(
    UINT            MethodStartIndex,
    PACPI_NAMESPACE pRoot,
    UINT            uCount,
    PACPI_NAMESPACE pKernalParent,
    PACPI_NAMESPACE pActuallyParent
)
/*++

Routine Description:

    Build the Acpi NS Data structre to user space memory related 

Arguments:

    pRoot           -- Root of start acpi ns
    uCount          -- Size of acpi ns
    pKernalParent   -- Parent kernel address
    pActuallyParent -- Parent of user space address

Return Value:



--*/ 
{

    UINT                Index, ParentIndex;
    PACPI_NAMESPACE     pAcpiNS = pRoot;
    PACPI_NAMESPACE     pNext = pRoot;
    PACPI_NAMESPACE     pParentNext = pRoot;
    PACPI_NAMESPACE     pFirst = NULL;

    BOOL                bFirst = TRUE;

    if (pRoot == NULL) {
        return;
    }
    // pKernalParent & pActuallyParent is NULL, means the first..
    for (Index = 0; Index < uCount; Index++) {
        pParentNext = pRoot;
        for (ParentIndex = 0; ParentIndex < uCount; ParentIndex++) {
            if (pNext->pParentNameSpace == pParentNext->pAddress) {
                // found the parent, set the parent
                pNext->pParent = pParentNext;
                InsertAcpiNS(pParentNext->pChild, pNext);
                if (pParentNext->pChild == NULL) {
                    pParentNext->pChild = pNext;
                }  
                break;
            }
            pParentNext++;
        }
        if (pNext->pUserContain == NULL) {

            if (pNext->Type == ACPI_TYPE_PACKAGE) {
                ULONG uLength = pNext->Length;
                if (!EvalAcpiNS(ghLocalDriver, pNext, &pNext->pUserContain, &uLength)) {
                    pNext->pUserContain = NULL;
                    if (pNext->Length == 0) {
                        pNext->pUserContain = ReadAcpiMemory(ghLocalDriver, pNext->pKernelAddr, sizeof(ACPI_OBJ));
                    }
                    else {
                        pNext->pUserContain = ReadAcpiMemory(ghLocalDriver, pNext->Contain, pNext->Length);
                    }
                }
                else {
                    // update the length of data
                    pNext->Length = uLength;
                }
            }
            else {
                if (pNext->Length == 0) {
                    pNext->pUserContain = ReadAcpiMemory(ghLocalDriver, pNext->pKernelAddr, sizeof(ACPI_OBJ));
                }
                else {
                    pNext->pUserContain = ReadAcpiMemory(ghLocalDriver, pNext->Contain, pNext->Length);
                }
            }
        }
        pFirst = pNext;
        if (pNext->Type == ACPI_TYPE_METHOD) {
            // check method check 
            if (pNext->pUserContain != NULL) {
                pNext->uFlags = ((UCHAR*)pNext->pUserContain)[0xC1];
                AddMethodMap(pNext->MethodName, pNext->uFlags, pNext);
            }
        }
        pNext++;
    }

    //for (Index = 0; Index < uCount; Index++) {
    //    if (pNext->pParentNameSpace == pKernalParent) {
    //        pNext->pParent = pActuallyParent;
    //        if (pActuallyParent != NULL ) {
    //            if (pActuallyParent->pChild == NULL) {
    //                pActuallyParent->pChild = pNext;
    //            }
    //            else {
    //                // already have child, then insert it 
    //            }
    //        }
    //        InsertAcpiNS(pFirst, pNext);
    //        if (pNext->pUserContain == NULL) {
    //            pNext->pUserContain = ReadAcpiMemory(ghLocalDriver, pNext->Contain, pNext->Length);
    //        }
    //        pFirst = pNext; 
    //        if (pNext->Type == ACPI_TYPE_METHOD) {      
    //            
    //            if (pNext->pUserContain != NULL) {
    //                pNext->uFlags = ((UCHAR*)pNext->pUserContain)[0xC1];
    //                AddMethodMap(pNext->MethodName, pNext->uFlags, pNext);
    //            }
    //        }
    //        BuildAcpiNSDataInternal(MethodStartIndex, pNext, uCount, pNext->pAddress, pNext);
    //        break;
    //    }
    //    pNext++;
    //}  
    if (pRoot == NULL) {
        return;
    }
}

ACPI_METHOD_MAP* 
APIENTRY
GetMethod(
    UINT32 NameSeg
)
/*++

Routine Description:

    Get the method by name seg

Arguments:

    NameSeg  -- The Name of Method

Return Value:

        -- If the name seg is method, return the method

--*/
{
    return GetMethodMap(NameSeg);
}

void 
APIENTRY
ReleaseAcpiNS(
)
/*++

Routine Description:

    Release the resource of acpi name space

Arguments:

    NameSeg  -- The Name of Method

Return Value:

     

--*/
{
    ULONG Index;
    if (pLocalAcpiNS == NULL && uLocalAcpiNSCount == 0)
    {
        return;
    }
    for (Index = 0; Index < uLocalAcpiNSCount; Index++) {
        if (pLocalAcpiNS[Index].pUserContain != NULL) {
            free(pLocalAcpiNS[Index].pUserContain);
            pLocalAcpiNS[Index].pUserContain = NULL;
        }
    }
    free(pLocalAcpiNS);
    pLocalAcpiNS = NULL;
    uLocalAcpiNSCount = 0;
}

BOOLEAN
APIENTRY
QueryAcpiNSInLib(    
)
/*++

Routine Description:

    Read Acpi Namespace memory

Arguments:

    hDriver          -- Handle of service driver
    Address          -- Acpi NS Address to read
    Size             -- Length of data to read
    MethodStartIndex -- Method Start Index of method buffer

Return Value:

    Address         -- Acpi NS Memory readed

--*/
{
    UINTN       ReadCnt;
    DWORD       ReturnedLength;
    BOOL        IoctlResult;
    PACPI_NAMESPACE pAcpiNS = NULL;
    
   
    ReadCnt = 0;
    IoctlResult = DeviceIoControl(
        ghLocalDriver,            // Handle to device
        IOCTL_GPD_BUILD_ACPI,    // IO Control code for Read
        NULL,        // Buffer to driver.
        0, // Length of buffer in bytes.
        &ReadCnt,       // Buffer from driver.
        sizeof(ReadCnt),      // Length of buffer in bytes.
        &ReturnedLength,    // Bytes placed in DataBuffer.
        NULL                // NULL means wait till op. completes.
    );
    //printf("Need memory %ld\n", (UINT)ReadCnt);
    if (IoctlResult) {
        
        uLocalAcpiNSCount = (UINT)ReadCnt;
        pLocalAcpiNS = (PACPI_NAMESPACE)malloc(ReadCnt);
        if (pLocalAcpiNS != NULL) {
            //printf("Need memory %ld\n", (UINT)ReadCnt);
            memset(pLocalAcpiNS, 0, ReadCnt);
            IoctlResult = DeviceIoControl(
                ghLocalDriver,                // Handle to device
                IOCTL_GPD_BUILD_ACPI,    // IO Control code for Read
                NULL,                   // Buffer to driver.
                0,                      // Length of buffer in bytes.
                pLocalAcpiNS,   // Buffer from driver.
                (DWORD)ReadCnt,         // Length of buffer in bytes.
                &ReturnedLength,        // Bytes placed in DataBuffer.
                NULL                    // NULL means wait till op. completes.
            );
            //printf("Need memory %ld\n", (UINT)ReadCnt);
            if (!IoctlResult) {
                ApiError(GetLastError(), _T("Query Acpi NS In Lib"));
                free(pLocalAcpiNS);
                pLocalAcpiNS = NULL;
                uLocalAcpiNSCount = 0;
                
                return FALSE;
            }
            ApiError(GetLastError(), _T("Query Acpi NS In Lib"));
            uLocalAcpiNSCount = ReturnedLength / sizeof(ACPI_NAMESPACE);
            //  build Acpi NS data structure
            BuildAcpiNSData(uLocalMethodOffset, pLocalAcpiNS, uLocalAcpiNSCount, NULL, NULL);
            return TRUE;
        }
    }
    else {
        //MessageBox(NULL, dwChars ? wszMsgBuff : _T("Error message not found."), Title, MB_OK); */
        ApiError(GetLastError(),_T("Query Acpi NS In Lib"));
    }
    uLocalAcpiNSCount = 0;
    return FALSE;
}

UINT 
APIENTRY
GetNamePath(
    UINT32* puParent,
    BYTE * pChild
    //UINT32* puChild
    )
/*++

Routine Description:

    Get NameSeg child from giving Name Path
    NamePath[0] is segcount

Arguments:

    puParent          -- Parent Path
    puChild           -- The child Names

Return Value:

    UINT              -- Length of child names

--*/
{
    UINT32* puChild = (UINT32*)pChild;
    ACPI_NAMESPACE* pAcpiNS = pLocalAcpiNS;
    ACPI_NAMESPACE* pEnd;
    UINT Index = 0;
    UINT nCount = 0;
    if (puChild == NULL) {
        //return 0;
    }
    if (puParent == NULL || puParent[0] == 0) {
        // return the root
        if (puChild != NULL) {
            puChild[0] = 1;
            puChild[1] = ACPI_SIGNATURE('\\', '_', '_', '_');
        }
        return 1;
    }
    else {
        for (Index = 0; Index < puParent[0]; Index++) {
            while (pAcpiNS != NULL) {
                if (pAcpiNS == NULL) {
                    return 0;
                }
                if (pAcpiNS->MethodNameAsUlong == puParent[Index + 1]) {
                    pAcpiNS = pAcpiNS->pChild;
                    break;
                }
                pAcpiNS = pAcpiNS->pNext;
            }
            if (pAcpiNS == NULL) {
                return 0;
            }
        }
        if (pAcpiNS != NULL) {
            // fill all the names...
            pEnd = pAcpiNS;
            do{
                if (puChild != NULL) {
                    puChild[nCount + 1] = pAcpiNS->MethodNameAsUlong;
                }
                nCount++;
                pAcpiNS = pAcpiNS->pNext;
            } while ((pAcpiNS != NULL) && (pAcpiNS != pEnd));
            if (puChild != NULL) {
                puChild[0] = nCount;
            }
        }
    }
    return nCount;
}

// From Path to AcpiNS for internal
ACPI_NS *
APIENTRY
GetAcpiNS(
    TCHAR* pParent,
    USHORT *puType
)

/*++

Routine Description:

    Get the type of acpi name string

Arguments:

    pParent          -- Parent Path
    puType           -- Acpi Type of Name Path

Return Value:

    ACPI_NS          -- The struct of acpi ns

--*/
{
    ACPI_NAMESPACE* pAcpiNS = pLocalAcpiNS;
    size_t          cbLength = 0;
    size_t          iIndex = 0;

    if (pParent == NULL) {
        return NULL;
    }
    if (pAcpiNS == NULL) {
        return NULL;
    }

    if (FAILED(StringCbLength(pParent, MAX_PATH * 4, &cbLength))) {

        return NULL;
    }

    cbLength /= 2;

    for (iIndex = 0; iIndex < cbLength; iIndex++) {
        //pAcpiNS = pAcpiNS->pChild;
        while (pAcpiNS != NULL) {
            if (pParent[iIndex * 4 + 0] == pAcpiNS->MethodName[0] &&
                pParent[iIndex * 4 + 1] == pAcpiNS->MethodName[1] &&
                pParent[iIndex * 4 + 2] == pAcpiNS->MethodName[2] &&
                pParent[iIndex * 4 + 3] == pAcpiNS->MethodName[3])
            {
                if (pParent[iIndex * 4 + 4] == 0) {
                    if (puType != NULL) {
                        // end of device....... // how to know it's and
                        if (pAcpiNS->Type == *puType) {
                            break;
                        }
                    }
                    else {
                        break;
                    }
                }
                else {
                    break;
                }
            }
            pAcpiNS = pAcpiNS->pNext;
        }
        if (pAcpiNS == NULL) {
            return NULL;
        }
        if (pParent[iIndex * 4 + 4] == 0) {
            break;
        }
        pAcpiNS = pAcpiNS->pChild;
    }    
    return pAcpiNS;
}

USHORT
APIENTRY
GetNameType(
    TCHAR* pParent
)

/*++

Routine Description:

    Get the type of acpi name string

Arguments:

    pParent          -- Parent Path

Return Value:

    UINT             -- Type of name string

--*/
{
    // Using reduced code
    ACPI_NS* pAcpiNS = GetAcpiNS(pParent, NULL);
    if (pAcpiNS == NULL) {
        return 0xFFFF;
    }
    return pAcpiNS->Type;

    // Not Using reduced code
    //ACPI_NAMESPACE* pAcpiNS = pLocalAcpiNS;
    //size_t          cbLength = 0;
    //size_t          iIndex = 0;
    //
    //if (pParent == NULL) {
    //    return 0xFFFF;
    //}
    //if (pAcpiNS == NULL) {
    //    return 0xFFFF;
    //}   

    //if (FAILED(StringCbLength(pParent, MAX_PATH * 4, &cbLength))) {
    //    
    //    return 0xFFFF;
    //}
    //
    //cbLength /= 2;

    //for (iIndex = 0; iIndex < cbLength; iIndex++) {
    //    //pAcpiNS = pAcpiNS->pChild;
    //    while (pAcpiNS != NULL) {
    //        if (pParent[iIndex * 4 + 0] == pAcpiNS->MethodName[0] &&
    //            pParent[iIndex * 4 + 1] == pAcpiNS->MethodName[1] &&
    //            pParent[iIndex * 4 + 2] == pAcpiNS->MethodName[2] &&
    //            pParent[iIndex * 4 + 3] == pAcpiNS->MethodName[3])
    //        {                
    //            break;
    //        }
    //        pAcpiNS = pAcpiNS->pNext;
    //    }        
    //    if (pAcpiNS == NULL) {
    //        return 0xFFFF;
    //    }
    //    if (pParent[iIndex * 4 + 4] == 0) {
    //        break;
    //    }
    //    pAcpiNS = pAcpiNS->pChild;
    //}
    //if (pAcpiNS == NULL) {
    //    return 0xFFFF;
    //}
    //return pAcpiNS->Type;
}

int
APIENTRY
GetNamePathFromPath(
    TCHAR* pParent,
    TCHAR* pChild
)
/*++

Routine Description:

    Get NameSeg child from giving Name Path
    NamePath[0] is segcount

Arguments:

    pParent          -- Parent Path
    pChild           -- The child Names

Return Value:

    UINT             -- Length of child names

--*/
{
    ACPI_NAMESPACE* pAcpiNS = pLocalAcpiNS;
    ACPI_NAMESPACE* pEnd;   
    TCHAR*          puChild = pChild;
    UINT32          uNameSeg[MAX_PATH + 1];
    UCHAR           tch[3] = { 0x0,0x0,0x0 };
    UINT            Index;
    UINT            SegCount = 0;
    size_t          cbLength = 0;
    int             nCount = 0;
    
    if (pParent == NULL) {
        return -1;
    }
    if (FAILED(StringCbLength(pParent, MAX_PATH * 4, &cbLength))) {
        return -2;
    }
    //cbLength = cbLength / 2;
    if (cbLength % 4 != 0) {
        return -3;
    }
    if (cbLength > MAX_PATH) {
        return -4;
    }
    memset(uNameSeg, 0, sizeof(uNameSeg));
    // Get the name of path
    WideCharToMultiByte(CP_UTF8, WC_COMPOSITECHECK, pParent, (int)cbLength, (LPSTR)uNameSeg, sizeof(uNameSeg), NULL, NULL);
    SegCount = (UINT)strlen((char *)uNameSeg) / 4;

    for (Index = 0; Index < SegCount; Index++) {
        while (pAcpiNS != NULL) {
            if (pAcpiNS == NULL) {
                return 0;
            }
            if (pAcpiNS->MethodNameAsUlong == uNameSeg[Index]) {
                pAcpiNS = pAcpiNS->pChild;
                break;
            }
            pAcpiNS = pAcpiNS->pNext;
        }
        if (pAcpiNS == NULL) {
            return 0;
        }
    }
    if (pAcpiNS != NULL) {
        // fill all the names...
        pEnd = pAcpiNS;
        do {            
            //puChild[nCount] = pAcpiNS->MethodNameAsUlong;            
            if (puChild != NULL) {
                if (pAcpiNS->MethodNameAsUlong == 0) {
                    *puChild = '-';
                    puChild++;
                    *puChild = '-';
                    puChild++;
                    *puChild = '-';
                    puChild++;
                    *puChild = '-';
                    puChild++;
                }
                else {
                    *puChild = pAcpiNS->MethodName[0];
                    puChild++;
                    *puChild = pAcpiNS->MethodName[1];
                    puChild++;
                    *puChild = pAcpiNS->MethodName[2];
                    puChild++;
                    *puChild = pAcpiNS->MethodName[3];
                    puChild++;
                }
            }
            nCount++;
            pAcpiNS = pAcpiNS->pNext;
        } while ((pAcpiNS != NULL) && (pAcpiNS != pEnd));
        if (puChild != NULL) {
            *puChild = 0;
        }
    }
    return nCount;
}


int
APIENTRY
GetNameAddrFromPath(
    TCHAR* pParent,
    PVOID* pChild
)
/*++

Routine Description:

    Get NameSeg child from giving Name Path
    NamePath[0] is segcount

Arguments:

    pParent          -- Parent Path
    pChild           -- The child Names

Return Value:

    UINT             -- Length of child names

--*/
{
    ACPI_NAMESPACE* pAcpiNS = pLocalAcpiNS;
    ACPI_NAMESPACE* pEnd;
    PVOID*          puChild = pChild;
    UINT32          uNameSeg[MAX_PATH + 1];
    UCHAR           tch[3] = { 0x0,0x0,0x0 };
    UINT            Index;
    UINT            SegCount = 0;
    size_t          cbLength = 0;
    int             nCount = 0;

    if (pParent == NULL) {
        return -1;
    }
    if (FAILED(StringCbLength(pParent, MAX_PATH * 4, &cbLength))) 
    {
        return -2;
    }
    //cbLength = cbLength / 2;
    if (cbLength % 4 != 0) {
        // ACPI NameSeg is 4      
        return -3;
    }
    if (cbLength > MAX_PATH) {
        return -4;
    }
    memset(uNameSeg, 0, sizeof(uNameSeg));
    // Get the name of path
    WideCharToMultiByte(CP_UTF8, WC_COMPOSITECHECK, pParent, (int)cbLength, (LPSTR)uNameSeg, sizeof(uNameSeg), NULL, NULL);
    SegCount = (UINT)strlen((char*)uNameSeg) / 4;

    for (Index = 0; Index < SegCount; Index++) {
        while (pAcpiNS != NULL) {
            if (pAcpiNS == NULL) {
                return 0;
            }
            if (pAcpiNS->MethodNameAsUlong == uNameSeg[Index]) {
                pAcpiNS = pAcpiNS->pChild;
                break;
            }
            pAcpiNS = pAcpiNS->pNext;
        }
        if (pAcpiNS == NULL) {
            return 0;
        }
    }
    if (pAcpiNS != NULL) {
        // fill all the names...
        pEnd = pAcpiNS;
        do {
            //puChild[nCount] = pAcpiNS->MethodNameAsUlong;            
            if (puChild != NULL) {
                *puChild = pAcpiNS;
                puChild++;
            }
            nCount++;
            pAcpiNS = pAcpiNS->pNext;
        } while ((pAcpiNS != NULL) && (pAcpiNS != pEnd));        
    }
    return nCount;
}

BOOLEAN
APIENTRY
GetNameIntValue(
    TCHAR* pParent,
    ULONG64 *uLong64
)

/*++

Routine Description:

    Get the type of acpi name string

Arguments:

    pParent          -- Parent Path

Return Value:

    UINT             -- Type of name string

--*/
{
    // Using reduced code
    USHORT uType = ACPI_TYPE_INTEGER;
    ACPI_NS* pAcpiNS = GetAcpiNS(pParent, &uType);
    if (pAcpiNS == NULL) {
        return FALSE;
    }
    ACPI_OBJ* pAcpiObj = (ACPI_OBJ *)pAcpiNS->pUserContain;
    if (uLong64 != NULL) {
        *uLong64 = pAcpiObj->ObjData.qwDataValue;
    }
    return TRUE;
}

PVOID
PutIntArg(
    PVOID   pArgs,
    UINT64  value
)
/*++

Routine Description:

    Put the Input Args

Arguments:

    pArgs           -- Complexity Argument ptr
    value           -- UInt64 Value

Return Value:

    PVOID           -- Complexity Argument ptr

--*/
{
    ACPI_EVAL_INPUT_BUFFER_COMPLEX* pInputArgs = pArgs;
    ACPI_EVAL_INPUT_BUFFER_COMPLEX* pLocalArgs = pArgs;
    ACPI_METHOD_ARGUMENT* pArg;

    if (pInputArgs == NULL) {
        // allocate the memory buffer for new args
        pInputArgs = malloc(sizeof(ACPI_EVAL_INPUT_BUFFER_COMPLEX) + sizeof(UINT64));
        if (pInputArgs == NULL) {
            return NULL;
        }
        pInputArgs->ArgumentCount = 1;
        pInputArgs->Size = sizeof(ACPI_EVAL_INPUT_BUFFER_COMPLEX) + sizeof(UINT64);
        pInputArgs->Signature = ACPI_EVAL_INPUT_BUFFER_COMPLEX_SIGNATURE;
        pInputArgs->Argument[0].Type = ACPI_METHOD_ARGUMENT_INTEGER;
        pInputArgs->Argument[0].DataLength = sizeof(UINT64);
        memcpy(pInputArgs->Argument[0].Data, &value, sizeof(UINT64));
        pArg = pInputArgs->Argument;
        pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        pInputArgs->Size = (ULONG)((UINT64)pArg - (UINT64)pInputArgs);
    }
    else {
        pInputArgs = malloc(pInputArgs->Size +sizeof(ACPI_METHOD_ARGUMENT) + sizeof(UINT64));
        if (pInputArgs == NULL) {
            return NULL;
        }
        memcpy(pInputArgs, pArgs, pLocalArgs->Size);
        pInputArgs->Size = pInputArgs->Size + sizeof(ACPI_METHOD_ARGUMENT) + sizeof(UINT64);
        pArg = pInputArgs->Argument;
        for (ULONG uIndex = 0; uIndex < pInputArgs->ArgumentCount; uIndex++) {
            pArg = (ACPI_METHOD_ARGUMENT *)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        }
        pArg->Type = ACPI_METHOD_ARGUMENT_INTEGER;
        pInputArgs->ArgumentCount ++;
        pArg->DataLength = sizeof(UINT64);
        memcpy(pArg->Data, &value, sizeof(UINT64));
        pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        pInputArgs->Size = (ULONG)((UINT64)pArg - (UINT64)pInputArgs);
        free(pArgs);
    }
    return pInputArgs;
}

PVOID
PutStringArg(
    PVOID   pArgs,
    UINT    Length,
    TCHAR   *pString
)
/*++

Routine Description:

    Put the Input Args

Arguments:

    pArgs           -- Complexity Argument ptr
    pString         -- Strings

Return Value:

    PVOID           -- Complexity Argument ptr

--*/
{
    ACPI_EVAL_INPUT_BUFFER_COMPLEX* pInputArgs = pArgs;
    ACPI_EVAL_INPUT_BUFFER_COMPLEX* pLocalArgs = pArgs;
    ACPI_METHOD_ARGUMENT* pArg;
    UINT uIndex;

    if (pInputArgs == NULL) {
        // allocate the memory buffer for new args
        pInputArgs = malloc(sizeof(ACPI_EVAL_INPUT_BUFFER_COMPLEX) + Length + 1);
        if (pInputArgs == NULL) {
            return NULL;
        }
        pInputArgs->ArgumentCount = 1;
        pInputArgs->Size = sizeof(ACPI_EVAL_INPUT_BUFFER_COMPLEX) + Length + 1;
        pInputArgs->Signature = ACPI_EVAL_INPUT_BUFFER_COMPLEX_SIGNATURE;
        pInputArgs->Argument[0].Type = ACPI_METHOD_ARGUMENT_STRING;
        pInputArgs->Argument[0].DataLength = Length + 1;
        for (uIndex = 0; uIndex < Length; uIndex++) {
            pInputArgs->Argument[0].Data[uIndex] = (UCHAR)pString[uIndex];
        }
        pInputArgs->Argument[0].Data[uIndex] = 0;
        pArg = pInputArgs->Argument;
        pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        pInputArgs->Size = (ULONG)((UINT64)pArg - (UINT64)pInputArgs);
    }
    else {       
        pInputArgs = malloc(pInputArgs->Size + sizeof(ACPI_METHOD_ARGUMENT) + Length + 1);
        if (pInputArgs == NULL) {
            free(pArgs);
            return NULL;
        }
        memcpy(pInputArgs, pArgs, pLocalArgs->Size);
        pInputArgs->Size = pInputArgs->Size + sizeof(ACPI_METHOD_ARGUMENT) + Length + 1;
        pArg = pInputArgs->Argument;
        for (ULONG uIndex = 0; uIndex < pInputArgs->ArgumentCount; uIndex++) {
            pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        }
        pInputArgs->ArgumentCount++;
        pArg->Type = ACPI_METHOD_ARGUMENT_STRING;
        pArg->DataLength = Length + 1;
        for (uIndex = 0; uIndex < Length; uIndex++) {
            pArg->Data[uIndex] = (UCHAR)pString[uIndex];
        }
        pArg->Data[uIndex] = 0;
        pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        pInputArgs->Size = (ULONG)((UINT64)pArg - (UINT64)pInputArgs);
        free(pArgs);
    }
    return pInputArgs;
}

PVOID
PutBuffArg(
    PVOID   pArgs,
    UINT    Length,
    UCHAR*  pBuf
)
/*++

Routine Description:

    Put the Input Args

Arguments:

    pArgs           -- Complexity Argument ptr
    pBuf            -- Buffer ptr

Return Value:

    PVOID           -- Complexity Argument ptr

--*/
{
    ACPI_EVAL_INPUT_BUFFER_COMPLEX* pInputArgs = pArgs;
    ACPI_EVAL_INPUT_BUFFER_COMPLEX* pLocalArgs = pArgs;
    ACPI_METHOD_ARGUMENT* pArg;

    if (pInputArgs == NULL) {
        // allocate the memory buffer for new args
        pInputArgs = malloc(sizeof(ACPI_EVAL_INPUT_BUFFER_COMPLEX) + Length + 1);
        if (pInputArgs == NULL) {
            return NULL;
        }
        pInputArgs->ArgumentCount = 1;
        pInputArgs->Size = sizeof(ACPI_EVAL_INPUT_BUFFER_COMPLEX) + Length;
        pInputArgs->Signature = ACPI_EVAL_INPUT_BUFFER_COMPLEX_SIGNATURE;
        pInputArgs->Argument[0].Type = ACPI_METHOD_ARGUMENT_BUFFER;
        pInputArgs->Argument[0].DataLength =(USHORT) Length;
        memcpy(pInputArgs->Argument[0].Data, pBuf, Length);
        pArg = pInputArgs->Argument;
        pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        pInputArgs->Size = (ULONG)((UINT64)pArg - (UINT64)pInputArgs);
    }
    else {        
        pInputArgs = malloc(pInputArgs->Size + sizeof(ACPI_METHOD_ARGUMENT) + Length + 1);
        if (pInputArgs == NULL) {
            free(pArgs);
            return NULL;
        }
        memcpy(pInputArgs, pArgs, pLocalArgs->Size);
        pInputArgs->Size = pInputArgs->Size + sizeof(ACPI_METHOD_ARGUMENT) + Length + 1;
        pArg = pInputArgs->Argument;
        for (ULONG uIndex = 0; uIndex < pInputArgs->ArgumentCount; uIndex++) {
            pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        }
        pInputArgs->ArgumentCount++;
        pArg->DataLength = (USHORT)Length;
        pArg->Type = ACPI_METHOD_ARGUMENT_BUFFER;
        memcpy(pArg->Data, pBuf, Length);        
        free(pArgs);
        pArg = (ACPI_METHOD_ARGUMENT*)ACPI_METHOD_NEXT_ARGUMENT(pArg);
        pInputArgs->Size = (ULONG)((UINT64)pArg - (UINT64)pInputArgs);
    }
    return pInputArgs;
}

BOOLEAN
APIENTRY
GetArgsCount(
    TCHAR* pParent,
    ULONG64* uLong64
)
/*++

Routine Description:

    Get the type of acpi name string

Arguments:

    pParent          -- Parent Path

Return Value:

    UINT             -- Type of name string

--*/
{
    // Using reduced code
    USHORT uType = ACPI_TYPE_METHOD;
    ACPI_NS* pAcpiNS = GetAcpiNS(pParent, &uType);
    if (pAcpiNS == NULL) {
        return FALSE;
    }
    if (uLong64 != NULL) {
        *uLong64 = pAcpiNS->ArgCount & 0x7;
    }
    return TRUE;
}

int
APIENTRY
GetNameStringValue(
    TCHAR* pParent,
    TCHAR* pString
)

/*++

Routine Description:

    Get the type of acpi name string

Arguments:

    pParent          -- Parent Path

Return Value:

    UINT             -- Type of name string

--*/
{
    // Using reduced code
    USHORT uType = ACPI_TYPE_STRING;
    ACPI_NS* pAcpiNS = GetAcpiNS(pParent, &uType);
    if (pAcpiNS == NULL) {
        return -1;
    }
    UCHAR* pAcpiObj = (UCHAR*)pAcpiNS->pUserContain;
    if (pString != NULL) {
        //*uLong64 = pAcpiObj->ObjData.qwDataValue;
        for (ULONG uIndex = 0; uIndex < pAcpiNS->Length; uIndex++) {
            pString[uIndex] = pAcpiObj[uIndex];
        }
    }
    return pAcpiNS->Length;
}

PVOID
APIENTRY
GetNameAddr(
    TCHAR* pParent
)

/*++

Routine Description:

    Get the type of acpi name string

Arguments:

    pParent          -- Parent Path

Return Value:

    PVOID            -- Address of name space

--*/
{
    // Using reduced code
    return GetAcpiNS(pParent, NULL);
}

void
APIENTRY
GetNameFromAddr(
    ACPI_NS* pAcpiNS,
    TCHAR* pName
)

/*++

Routine Description:

    Get the name seg of giving ptr

Arguments:

    pAcpiNS          -- acpi name space address
    pName            -- Receive the name

Return Value:

    

--*/
{
    // Using reduced code
    if (pName != NULL && pAcpiNS != NULL) {
        pName[0] = pAcpiNS->MethodName[0];
        pName[1] = pAcpiNS->MethodName[1];
        pName[2] = pAcpiNS->MethodName[2];
        pName[3] = pAcpiNS->MethodName[3];
        pName[4] = 0;
    }
}

UINT64
APIENTRY
AslFromPath(
    TCHAR* pPath,
    TCHAR* pAsl
)
/*++

Routine Description:

    Get the name seg of giving ptr

Arguments:

    pPath          -- Acpi NS Path
    pAsl           -- Asl code

Return Value:

    sizeof of asl code

--*/
{
    if (pAsl != NULL) {
        return CopyAslCode(pAsl);        
    }
    ACPI_NS *pAcpiNS = GetAcpiNS(pPath, NULL);
    if (pAcpiNS != NULL) {
        ResetAmlPrintMem();
        AmlParser(pAcpiNS, NULL);
    }
    return CopyAslCode(pAsl);
}

BOOLEAN 
APIENTRY
NotifyDevice(
	TCHAR	*pchPath,
	ULONG	ulCode
)
/*++

Routine Description:

    Notify the Acpi Device, Processor or thermzal zone

Arguments:

    pPath          -- Acpi NS Path
    ulCode         -- Notification code

Return Value:

    sizeof of asl code

--*/
{
	ACPI_NS					 *pNotify;
	ACPI_NS					 *pDevice;
	TCHAR					 pchNotify[] = _T("\\___MYNT");
	PACPI_EVAL_OUTPUT_BUFFER pAcpiData = NULL;
	DWORD				     ReadCnt;
	DWORD					 ReturnedLength;
	size_t					 cbLength;
	AML_SETUP				 amlSetup = { 0x100, 0xC1 };
	amlSetup.dwOffset = uLocalMethodOffset;
	amlSetup.dwSize = uLocalMethodOffset / 0x100 + 0x100;

	USHORT					 uType = ACPI_TYPE_METHOD;
	pNotify = GetAcpiNS(pchNotify, &uType);
	if (pNotify == NULL) {
		return FALSE;
	}
	pDevice = GetAcpiNS(pchPath, NULL);
	if (pDevice == NULL) {
		return FALSE;
	}
	//
	if (FAILED(StringCbLength(pchPath, MAX_PATH, &cbLength)))
	{
		return FALSE;
;	}
	
	cbLength /= sizeof(TCHAR);

	amlSetup.ulCode = ulCode;
	// tchar size to Name Arg Size
	cbLength /= 4;
	cbLength -= 1;

	if (cbLength < 2) {
		// it's a simple path 4 bytes
		amlSetup.Name[0] = pDevice->MethodName[0];
		amlSetup.Name[1] = pDevice->MethodName[1];
		amlSetup.Name[2] = pDevice->MethodName[2];
		amlSetup.Name[3] = pDevice->MethodName[3];
		amlSetup.Name[4] = 0;
		amlSetup.uNameSize = 4;
	}
	else if (cbLength == 2) {
		// it's a dual name seg, '/
		amlSetup.Name[0] = '\\';
		amlSetup.Name[1] = 0x2E;

		amlSetup.Name[2] = (UCHAR)pchPath[4];
		amlSetup.Name[3] = (UCHAR)pchPath[5];
		amlSetup.Name[4] = (UCHAR)pchPath[6];
		amlSetup.Name[5] = (UCHAR)pchPath[7];
		amlSetup.Name[6] = (UCHAR)pchPath[8];
		amlSetup.Name[7] = (UCHAR)pchPath[9];
		amlSetup.Name[8] = (UCHAR)pchPath[10];
		amlSetup.Name[9] = (UCHAR)pchPath[11];
		amlSetup.Name[10] = 0;
		amlSetup.uNameSize = 10;
	}
	else {
		//  it's a multi name seg
		//	int   NameIndex = 0;
		//ACPI_NAMESPACE* pLocal = gActiveDevice;
		amlSetup.Name[0] = '\\';
		amlSetup.Name[1] = 0x2F;
		amlSetup.Name[2] = (UCHAR)cbLength;
		amlSetup.uNameSize = ((ULONG)cbLength) * 4 + 3;
		for (size_t iIndex = 3; iIndex < cbLength * 4 + 3; iIndex++) {
			amlSetup.Name[iIndex] = (UCHAR)pchPath[iIndex + 1];
		}
		amlSetup.Name[3 + ((ULONG)cbLength) * 4] = 0;		
	}

	if (!DeviceIoControl(
		ghLocalDriver,            // Handle to device
		IOCTL_GPD_LOAD_AML,    // IO Control code for Read
		&amlSetup,        // Buffer to driver.
		sizeof(amlSetup), // Length of buffer in bytes.
		&ReadCnt,       // Buffer from driver.
		sizeof(ReadCnt),      // Length of buffer in bytes.
		&ReturnedLength,    // Bytes placed in DataBuffer.
		NULL                // NULL means wait till op. completes.
	)) {
		return FALSE;
	}

	if (EvalAcpiNS(ghLocalDriver, pNotify, &pAcpiData, NULL)) {
		ParseReturnData(pNotify, pAcpiData);
	}
	if (pAcpiData != NULL) {
		free(pAcpiData);
	}
	return TRUE;
}


int
APIENTRY
GetNSType(
    TCHAR* pchPath
)
/*++

Routine Description:

    Get type of Acpi Name space object

Arguments:

    pPath          -- Acpi NS Path
    bSearch        -- Search for all parent path or just current path

Return Value:

    Type of object

--*/
{
    ACPI_NS *pAcpiNS = GetAcpiNS(pchPath, NULL);
    if (pAcpiNS == NULL) {
        return -1;
    }
    return (int)pAcpiNS->Type;
}

ULONG GetFullNameFromNS(ACPI_NS* pAcpiNS, char *pchFullPath)
{
    ULONG  Length;
    UINT32 NameSeg[256];
    int    NameSegCount = 0;
    char   *pchPath = pchFullPath;
    ACPI_NS* pRoot = pAcpiNS;
    if (pchPath == NULL) {
        return 0;
    }
    while (pRoot != NULL) {
        // push the name seg
        NameSeg[NameSegCount] = pRoot->MethodNameAsUlong;
        pRoot = pRoot->pParent;
        NameSegCount++;
    }
    Length = NameSegCount * 4;
    // now get the full name, push back to system
    NameSegCount--;
    while (NameSegCount >= 0) {        
        // get the name to path
        memcpy(pchPath, &NameSeg[NameSegCount], 4);
        NameSegCount--;
        pchPath += 4;
    }
    *pchPath = '\0';    // end of string
    return Length;
}

VOID*
APIENTRY
GetNSValue(
    TCHAR* pchPath,
    USHORT* pulLength
)
/*++

Routine Description:

    Get the value of giving NS

Arguments:

    pParent          -- Parent Path
    puType           -- Acpi Type of Name Path

Return Value:

    ACPI_NS          -- The struct of acpi ns

--*/
{
    PACPI_EVAL_OUTPUT_BUFFER pAcpiData;
    ACPI_NS* pAcpiNS = GetAcpiNS(pchPath, pulLength);
    if (pAcpiNS == NULL) {
        return NULL;
    }
    if (ghLocalDriver == INVALID_HANDLE_VALUE) {
        switch (pAcpiNS->Type)
        {
       
        case ACPI_TYPE_INTEGER:
        {
            //
            ULONG nSize = FIELD_OFFSET(ACPI_EVAL_OUTPUT_BUFFER, Argument) +
                FIELD_OFFSET(ACPI_METHOD_ARGUMENT_V1, Data) + sizeof(UINT64);
            pAcpiData = malloc(nSize);
            if (pAcpiData != NULL) {
                ACPI_OBJ* pAcpiObj = (ACPI_OBJ*)pAcpiNS->pUserContain;
                memset(pAcpiData, 0, nSize);
                pAcpiData->Signature = ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE;
                pAcpiData->Length = nSize;
                pAcpiData->Count = 1;
                pAcpiData->Argument->Type = ACPI_METHOD_ARGUMENT_INTEGER;
                pAcpiData->Argument->DataLength = sizeof(UINT64);
                memcpy(pAcpiData->Argument->Data, &pAcpiObj->ObjData.qwDataValue, sizeof(UINT64));
            }
            return pAcpiData;
        }
        case ACPI_TYPE_STRING:
        {
            ULONG nSize = FIELD_OFFSET(ACPI_EVAL_OUTPUT_BUFFER, Argument) +
                FIELD_OFFSET(ACPI_METHOD_ARGUMENT_V1, Data) + pAcpiNS->Length;
            pAcpiData = malloc(nSize);
            if (pAcpiData != NULL) {
                //ACPI_OBJ* pAcpiObj = (ACPI_OBJ*)pAcpiNS->pUserContain;
                memset(pAcpiData, 0, nSize);
                pAcpiData->Signature = ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE;
                pAcpiData->Length = nSize;
                pAcpiData->Count = 1;
                pAcpiData->Argument->Type = ACPI_METHOD_ARGUMENT_STRING;
                pAcpiData->Argument->DataLength = (USHORT)(pAcpiNS->Length);
                memcpy(pAcpiData->Argument->Data, pAcpiNS->pUserContain, pAcpiNS->Length);
            }
            return pAcpiData;
        }
        case ACPI_TYPE_BUFFER:
        {
            ULONG nSize = FIELD_OFFSET(ACPI_EVAL_OUTPUT_BUFFER, Argument) +
                FIELD_OFFSET(ACPI_METHOD_ARGUMENT_V1, Data) + pAcpiNS->Length;
            pAcpiData = malloc(nSize);
            if (pAcpiData != NULL) {
                //ACPI_OBJ* pAcpiObj = (ACPI_OBJ*)pAcpiNS->pUserContain;
                memset(pAcpiData, 0, nSize);
                pAcpiData->Signature = ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE;
                pAcpiData->Length = nSize;
                pAcpiData->Count = 1;
                pAcpiData->Argument->Type = ACPI_METHOD_ARGUMENT_BUFFER;
                pAcpiData->Argument->DataLength = (USHORT)(pAcpiNS->Length);
                memcpy(pAcpiData->Argument->Data, pAcpiNS->pUserContain, pAcpiNS->Length);
            }
            return pAcpiData;
        }
        case ACPI_TYPE_PACKAGE: 
        {
            ULONG nSize = pAcpiNS->Length;
            pAcpiData = malloc(nSize);
            if (pAcpiData != NULL) {               
                memcpy(pAcpiData, pAcpiNS->pUserContain, pAcpiNS->Length);
            }
            return pAcpiData;
        }
        case ACPI_TYPE_METHOD:
        {
            ULONG nSize = pAcpiNS->Length;
            pAcpiData = malloc(nSize);
            if (pAcpiData != NULL) {
                memcpy(pAcpiData, pAcpiNS->pUserContain, pAcpiNS->Length);
                pAcpiData->Signature = ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE;
                pAcpiData->Length = nSize;
            }
            return pAcpiData;
        }
        // Conflit with driver loaded type, so just return nothing and for field use a default value
        //case ACPI_TYPE_OPERATION_REG:
        //{
        //    //
        //    ULONG nSize = FIELD_OFFSET(ACPI_EVAL_OUTPUT_BUFFER, Argument) +
        //        FIELD_OFFSET(ACPI_METHOD_ARGUMENT_V1, Data) + pAcpiNS->Length;
        //    pAcpiData = malloc(nSize);
        //    if (pAcpiData != NULL) {
        //        memset(pAcpiData, 0, nSize);
        //        pAcpiData->Signature = ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE;
        //        pAcpiData->Length = nSize;
        //        pAcpiData->Count = 1;
        //        pAcpiData->Argument->Type = ACPI_METHOD_ARGUMENT_BUFFER;
        //        pAcpiData->Argument->DataLength = (USHORT)(pAcpiNS->Length);
        //        memcpy(pAcpiData->Argument->Data, pAcpiNS->pUserContain, pAcpiNS->Length);
        //    }
        //    return pAcpiData;
        //}
        //case ACPI_TYPE_FIELDUNIT:
        //{
        //    ACPI_FIELD_UNIT* pFieldUnit;
        //    ACPI_NAMESPACE* pField;
        //    ACPI_NAMESPACE* pOpRegion;
        //    pFieldUnit = (ACPI_FIELD_UNIT*)pAcpiNS->pUserContain;
        //    if (pFieldUnit->pField != NULL) // point to the field AcpiObj
        //    {
        //        // get the Field object
        //        pField = GetAcpiNsFromNsAddr(pFieldUnit->pField);
        //        // get the OperationRegion
        //        if (pField != NULL) {
        //            pOpRegion = GetAcpiNsFromNsAddr((PVOID)(*(PVOID*)pField->pUserContain));
        //            if (pOpRegion != NULL) {
        //                char chPath[256 * 4];   // max path of acpi name space
        //                chPath[0] = 0;
        //                ULONG nStringLen = GetFullNameFromNS(pOpRegion, chPath);
        //                if (nStringLen == 0) {
        //                    break;
        //                }
        //                nStringLen++;// include end of string
        //                ULONG nSize = FIELD_OFFSET(ACPI_EVAL_OUTPUT_BUFFER, Argument) +
        //                    2 * FIELD_OFFSET(ACPI_METHOD_ARGUMENT_V1, Data) + nStringLen + sizeof(ACPI_FIELD_UNIT);
        //                pAcpiData = malloc(nSize);
        //                if (pAcpiData != NULL) {
        //                    memset(pAcpiData, 0, nSize);
        //                    pAcpiData->Signature = ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE;
        //                    pAcpiData->Length = nSize;
        //                    pAcpiData->Count = 2;
        //                    pAcpiData->Argument->Type = ACPI_METHOD_ARGUMENT_STRING;
        //                    pAcpiData->Argument->DataLength = (USHORT)(pOpRegion->Length);
        //                    memcpy(pAcpiData->Argument->Data, chPath, nStringLen);
        //                    // second args offset 
        //                    PACPI_METHOD_ARGUMENT pArg = ACPI_METHOD_NEXT_ARGUMENT(pAcpiData->Argument);
        //                    pArg->Type = ACPI_METHOD_ARGUMENT_BUFFER;
        //                    pArg->DataLength = sizeof(ACPI_FIELD_UNIT);
        //                    memcpy(pArg->Data, pFieldUnit, sizeof(ACPI_FIELD_UNIT));
        //                }
        //                // get the name of full Path Name OperationRegion

        //                /*ULONG nSize = FIELD_OFFSET(ACPI_EVAL_OUTPUT_BUFFER, Argument) +
        //                    FIELD_OFFSET(ACPI_METHOD_ARGUMENT_V1, Data) + pOpRegion->Length;
        //                pAcpiData = malloc(nSize);
        //                if (pAcpiData != NULL) {
        //                    memset(pAcpiData, 0, nSize);
        //                    pAcpiData->Signature = ACPI_EVAL_OUTPUT_BUFFER_SIGNATURE;
        //                    pAcpiData->Length = nSize;
        //                    pAcpiData->Count = 1;
        //                    pAcpiData->Argument->Type = ACPI_METHOD_ARGUMENT_BUFFER;
        //                    pAcpiData->Argument->DataLength = (USHORT)(pOpRegion->Length);
        //                    memcpy(pAcpiData->Argument->Data, pOpRegion->pUserContain, pOpRegion->Length);                            
        //                }*/
        //                return pAcpiData;
        //            }
        //        }
        //    }   
        //    break;
        //}
        default:
            break;
        }
    }
    else {    
        if (EvalAcpiNS(ghLocalDriver, pAcpiNS, &pAcpiData, NULL)) {
            return pAcpiData;
        }
    }
    return NULL;
}


VOID*
APIENTRY
GetRawData (
    TCHAR* pchPath,
    USHORT* puType,
    ULONG * puLength
)
/*++

Routine Description:

    Get raw data for acpi namespace

Arguments:

    pParent          -- Parent Path
    puType           -- Acpi Type of Name Path
    puLength         -- Raw data lenth

Return Value:

    Raw acpi data point or null

--*/
{
    // type and the 
    ACPI_NS* pAcpiNS = GetAcpiNS(pchPath, puType);
    if (pAcpiNS == NULL) {
        return NULL;
    }
    if (puLength != NULL) {
        *puLength = pAcpiNS->Length;
    }
    return pAcpiNS->pUserContain;
}