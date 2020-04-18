using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

namespace AcpiWin
{
    public class AcpiLib
    {
        //[DllImport("testdllwm6.dll", EntryPoint = "Connect")]
        //public static extern int Connect([MarshalAs(UnmanagedType.LPStr)] string lpPostData);
        static string[] strTypes =
            {
                "Scope",
                "Integer",
                "String",
                "Buffer",
                "Package",
                "Field Unit",
                "Device",
                "Sync Object",
                "Method",
                "Mutex",
                "Operation Region",
                "Power Source",
                "Processor",
                "Thermal Zone",
                "Buffer Unit",
                "DDB Handle",
                "Debug",
                "Alias",
                "Data Alias",
                "Bank Field",
                "Field",
                "Index Field",
                "Data",
                "Data Field",
                "Data Object",
                "Revision",
                "Create Field",
                "Extern",
                "Invalid"
            };
        [DllImport("AcpiLib.dll")]
        public static extern IntPtr OpenAcpiService();

        [DllImport("AcpiLib.dll")]
        public static extern void CloseAcpiService(IntPtr hHandle);

        [DllImport("AcpiLib.dll", EntryPoint = "LoadAcpiObjects", CharSet = CharSet.Unicode)]
        public static extern void LoadAcpiObjects(StringBuilder pString);

        [DllImport("AcpiLib.dll", EntryPoint = "SaveAcpiObjects", CharSet = CharSet.Unicode)]
        public static extern void SaveAcpiObjects(StringBuilder pString);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNamePathFromPath", CharSet = CharSet.Unicode)]
        public static extern int GetNamePathFromPath(StringBuilder pParent, StringBuilder pChild);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNameAddrFromPath", CharSet = CharSet.Unicode)]
        public static extern int GetNameAddrFromPath(StringBuilder pParent, IntPtr[] intPtrs);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNameType", CharSet = CharSet.Unicode)]
        public static extern ushort GetNameType(StringBuilder pName);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNameIntValue", CharSet = CharSet.Unicode)]
        public static extern Boolean GetNameIntValue(StringBuilder pName, ref UInt64 pValue);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNameStringValue", CharSet = CharSet.Unicode)]
        public static extern int GetNameStringValue(StringBuilder pParent, StringBuilder pChild);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNameAddr", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetNameAddr(StringBuilder pParent);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNameFromAddr", CharSet = CharSet.Unicode)]
        public static extern void GetNameFromAddr(IntPtr ptr, StringBuilder Name);

        [DllImport("AcpiLib.dll", EntryPoint = "AslFromPath", CharSet = CharSet.Unicode)]
        public static extern UInt64 AslFromPath(StringBuilder NsPath, StringBuilder AslCode);

        [DllImport("AcpiLib.dll", EntryPoint = "QueryAcpiNS", CharSet = CharSet.Unicode)]
        public static extern Boolean QueryAcpiNS(IntPtr hDriver, IntPtr pAcpiNS, uint MethodOff);

        [DllImport("AcpiLib.dll", EntryPoint = "EvalAcpiNSAndParse", CharSet = CharSet.Unicode)]
        public static extern UInt64 EvalAcpiNSAndParse(StringBuilder NsPath, StringBuilder AslCode);

        [DllImport("AcpiLib.dll", EntryPoint = "GetArgsCount", CharSet = CharSet.Unicode)]
        public static extern Boolean GetArgsCount(StringBuilder pName, ref UInt64 pValue);

        [DllImport("AcpiLib.dll", EntryPoint = "PutBuffArg", CharSet = CharSet.Unicode)]
        public static extern IntPtr PutBuffArg(IntPtr pArgs, UInt64 Length, byte[] data);

        [DllImport("AcpiLib.dll", EntryPoint = "PutIntArg", CharSet = CharSet.Unicode)]
        public static extern IntPtr PutIntArg(IntPtr pArgs, UInt64 Value);

        [DllImport("AcpiLib.dll", EntryPoint = "PutStringArg", CharSet = CharSet.Unicode)]
        public static extern IntPtr PutStringArg(IntPtr pArgs, UInt64 Length, StringBuilder pString);

        [DllImport("AcpiLib.dll", EntryPoint = "EvalAcpiNSArgAndParse", CharSet = CharSet.Unicode)]
        public static extern UInt64 EvalAcpiNSArgAndParse(StringBuilder NsPath, IntPtr pArg, StringBuilder AslCode);

        [DllImport("AcpiLib.dll", EntryPoint = "EvalAcpiNSArgOutput", CharSet = CharSet.Unicode)]
        public static extern IntPtr EvalAcpiNSArgOutput(StringBuilder NsPath, IntPtr pArg);

        [DllImport("AcpiLib.dll", EntryPoint = "EvalAcpiNSOutput", CharSet = CharSet.Unicode)]
        public static extern IntPtr EvalAcpiNSOutput(StringBuilder NsPath);

        [DllImport("AcpiLib.dll", EntryPoint = "FreeMemory", CharSet = CharSet.Unicode)]
        public static extern void FreeMemory(IntPtr pArgs);

        [DllImport("AcpiLib.dll", EntryPoint = "NotifyDevice", CharSet = CharSet.Unicode)]
        public static extern Boolean NotifyDevice(StringBuilder pString, ulong Length);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNSType", CharSet = CharSet.Unicode)]
        public static extern int GetNSType(StringBuilder pString);

        [DllImport("AcpiLib.dll", EntryPoint = "GetNSValue", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetNSValue(StringBuilder pString, ref ushort type);

        [DllImport("AcpiLib.dll", EntryPoint = "GetRawData", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetRawData(StringBuilder pString, ref ushort type, ref long length);

        
        private IntPtr hDriver;

        /// <summary>
        /// Constructor to initialize acpi lib and load the acpi name space from file or runtime
        /// </summary>
        public AcpiLib()
        {
            // check if the dll is exist
            if (!File.Exists ("acpilib.dll"))
            {
                // alert that acpilib.dll is not existing
                return;
            }

            hDriver = IntPtr.Zero;
            for (int nRetry = 0; nRetry < 10; nRetry ++)
            {
                hDriver = OpenAcpiService();
                if (hDriver.ToInt64() != -1)
                {
                    break;
                }
            }
            if (hDriver.ToInt64() == -1)
            {
                StringBuilder sb = new StringBuilder("acpins.bin");
                LoadAcpiObjects(sb);
                // Interface Testing code
                //byte[] bArray = new byte[5];
                //for (int i = 0; i< bArray.Length; i ++)
                //{
                //    bArray[i] = (byte)i;
                //}
                //IntPtr pArg = ArgPutBuffer(IntPtr.Zero, bArray);
                //if (pArg!= IntPtr.Zero)
                //{
                //    string value = "";
                //    if (GetEvalArgResult ("\\____OSI", pArg, ref value))
                //    {

                //    }
                //    FreeMemory(pArg);
                //}

                //IntPtr ptr = GetNameAddr(new StringBuilder("\\____GPE"));
                //if (ptr != IntPtr.Zero)
                //{
                //    StringBuilder sValue = new StringBuilder(10);
                //    GetNameFromAddr(ptr, sValue);
                //}
                //int Num = GetNameAddrFromPath(new StringBuilder("\\____GPE"), null);
                //if (Num > 0)
                //{
                //    IntPtr[] intPtrs = new IntPtr[Num];
                //    Num = GetNameAddrFromPath(new StringBuilder("\\____GPE"), intPtrs);
                //}
            } else
            {
                //Load dynamic AcpiNamespace from Acpi Driver, 0xC1 for method offset of Win10 after 1903
                if (QueryAcpiNS())
                {
                    StringBuilder sb = new StringBuilder("acpins.bin");
                    SaveAcpiObjects(sb);
                }
            }
        }
        /// <summary>
        /// Check if acpi libary is ready to load
        /// </summary>
        /// <returns></returns>
        public Boolean AcpiLibValid()
        {
            return File.Exists("acpilib.dll");
        }
        /// <summary>
        /// Get type of acpi name space from path
        /// </summary>
        /// <param name="path">full name path of acpi</param>
        /// <returns>type of acpi name space</returns>
        public int GetType(string path)
        {
            StringBuilder sValue = new StringBuilder(path);
            return GetNSType(sValue);
        }
        /// <summary>
        /// Get acpi name space value
        /// </summary>
        /// <param name="path">full name path of acpi</param>
        /// <param name="type">type of name space to receive</param>
        /// <returns>buffer point to value</returns>
        public IntPtr GetValue(string path, ref ushort type)
        {
            StringBuilder sValue = new StringBuilder(path);
            return GetNSValue(sValue, ref type);
        }
        /// <summary>
        /// Get acpi name space raw data
        /// </summary>
        /// <param name="path">full name path of acpi</param>
        /// <param name="type">type of name space to receive</param>
        /// <param name="length">length of raw data</param>
        /// <returns>buffer point to value</returns>
        public IntPtr GetAcpiContent(string path, ref ushort type, ref long length)
        {
            return GetRawData(new StringBuilder(path), ref type, ref length);
        }
        /// <summary>
        /// Notify the acpi node
        /// </summary>
        /// <param name="path">full name path of acpi</param>
        /// <param name="code">notify code</param>
        /// <returns>succesfully to call mynt or not</returns>
        public Boolean Notify (string path, UInt64 code)
        {
            StringBuilder sValue = new StringBuilder(path);
            return NotifyDevice(sValue, (ulong)code);
        }
        /// <summary>
        /// Free args memory that allocated by acpi lib
        /// </summary>
        /// <param name="pArg">point to arg buffer</param>
        public void FreeArg (IntPtr pArg)
        {
            if (pArg != IntPtr.Zero)
            {
                FreeMemory(pArg);
            }
        }
        /// <summary>
        /// Put Integer 64 value in arglist
        /// </summary>
        /// <param name="pArg">point to arglist or empty</param>
        /// <param name="value">integer value</param>
        /// <returns>point to arglist</returns>
        public IntPtr ArgPutUInt64(IntPtr pArg, UInt64 value)
        {
            return PutIntArg(pArg, value);
        }
        /// <summary>
        /// Put string value in arglist
        /// </summary>
        /// <param name="pArg">point to arglist or empty</param>
        /// <param name="value">string value</param>
        /// <returns>point to arglist</returns>
        public IntPtr ArgPutString(IntPtr pArg, string value)
        {
            StringBuilder sValue = new StringBuilder(value);
            return PutStringArg(pArg, (ulong)value.Length, sValue);
        }
        /// <summary>
        /// Put buffer value in arglist
        /// </summary>
        /// <param name="pArg">point to arglist or empty</param>
        /// <param name="value">buffer value</param>
        /// <returns>point to arglist</returns>
        public IntPtr ArgPutBuffer(IntPtr pArg, byte[] value)
        {
            return PutBuffArg(pArg, (ulong)value.Length, value);
        }
        /// <summary>
        /// Get arg count of acpi method path
        /// </summary>
        /// <param name="path">method path</param>
        /// <param name="value">value to receive arg count</param>
        /// <returns>succesfully get the count or path is not a method</returns>
        public Boolean GetMethodArgCount(string path, ref UInt64 value)
        {
            return GetArgsCount(new StringBuilder(path), ref value);
        }
        /// <summary>
        /// Get int value for acpi name space path
        /// </summary>
        /// <param name="path">full path of name space</param>
        /// <param name="value"receive the integer value</param>
        /// <returns>succesfully value not a integer type</returns>
        public Boolean GetIntValue (string path, ref UInt64 value)
        {
            return GetNameIntValue(new StringBuilder(path), ref value);
        }
        /// <summary>
        /// Get the driver load status
        /// </summary>
        /// <returns>driver is loaded/not, online/offline mode</returns>
        public Boolean DriverLoaded ()
        {
            return !(hDriver.ToInt64() == -1);
        }
        /// <summary>
        /// Query acpi data from driver
        /// </summary>
        /// <returns></returns>
        public Boolean QueryAcpiNS()
        {
            if (hDriver.ToInt64() != -1)
            {
                return QueryAcpiNS(hDriver, IntPtr.Zero, 0xC1);
            }
            return false;        
        }
        /// <summary>
        /// Run acpi method/or other data type with arglist
        /// </summary>
        /// <param name="path">full path of acpi name space</param>
        /// <param name="pArg">point to arglist</param>
        /// <returns>result</returns>
        public IntPtr GetEvalArgOutput(string path, IntPtr pArg)
        {
            if (path == null)
            {
                return IntPtr.Zero;
            }
            return EvalAcpiNSArgOutput(new StringBuilder(path), pArg);
        }
        /// <summary>
        /// Run acpi method/or other data type
        /// </summary>
        /// <param name="path">full path of acpi name space</param>
        /// <returns>result</returns>
        public IntPtr GetEvalOutput(string path)
        {
            if (path == null)
            {
                return IntPtr.Zero;
            }
            return EvalAcpiNSOutput(new StringBuilder(path));
        }
        /// <summary>
        /// Run acpi method/or other data type with arglist
        /// </summary>
        /// <param name="path">full path of acpi name space</param>
        /// <param name="pArg">point to arglist</param>
        /// <param name="value">receive the parsed string result</param>
        /// <returns>acpi name space run succesfully or not</returns>
        public Boolean GetEvalArgResult(string path, IntPtr pArg, ref string value)
        {
            if (path == null)
            {
                return false;
            }
            UInt64 length = EvalAcpiNSArgAndParse(new StringBuilder(path), pArg, null);
            if (length <= 0)
            {
                return false;
            }
            StringBuilder sValue = new StringBuilder((int)(length) * 2 + 2);
            length = EvalAcpiNSArgAndParse(new StringBuilder(path), pArg, sValue);
            value = sValue.ToString();
            return length > 0;
        }
        /// <summary>
        /// Run acpi method/or other data type
        /// </summary>
        /// <param name="path">full path of acpi name space</param>
        /// <param name="value">receive the parsed string result</param>
        /// <returns>acpi name space run succesfully or not</returns>
        public Boolean GetEvalResult(string path, ref string value)
        {
            if (path == null)
            {
                return false;
            }
            UInt64 length = EvalAcpiNSAndParse(new StringBuilder(path), null);
            if (length <= 0)
            {
                return false;
            }
            StringBuilder sValue = new StringBuilder((int)(length) * 2 + 2);
            length = EvalAcpiNSAndParse(new StringBuilder(path), sValue);
            value = sValue.ToString();
            return length > 0;

        }
        /// <summary>
        /// Get disassembled asl code 
        /// </summary>
        /// <param name="path">path of acpi name space</param>
        /// <param name="value">receive the asl code</param>
        /// <returns>acpi name space disassemble succesfully or not</returns>
        public Boolean GetAslCode(string path, ref string value)
        {
            if (path == null)
            {
                return false;                
            }
            UInt64 length = AslFromPath(new StringBuilder(path), null);
            if (length <= 0)
            {
                return false;
            }
            StringBuilder sValue = new StringBuilder((int)(length) * 2 + 2);
            length = AslFromPath(new StringBuilder(path), sValue);
            value = sValue.ToString();
            return length > 0;
        }
        /// <summary>
        /// Get string type acpi name space value
        /// </summary>
        /// <param name="path">path of name space</param>
        /// <param name="value">receive string data</param>
        /// <returns>get string succesfully or not</returns>
        public Boolean GetStringValue(string path, ref string value)
        {
            int length = GetNameStringValue(new StringBuilder(path), null);
            if (length <= 0)
            {
                return false;
            }
            StringBuilder sValue = new StringBuilder(length * 8 + 8);
            length = GetNameStringValue(new StringBuilder(path), sValue);
            value = sValue.ToString();
            return length > 0;
        }
        /// <summary>
        /// Get the type of acpi name space
        /// </summary>
        /// <param name="path">path of acpi name space</param>
        /// <returns>type of acpi name space</returns>
        public ushort GetTypeByName(string path)
        {
            return GetNameType(new StringBuilder(path));
        }
        /// <summary>
        /// Get type in string
        /// </summary>
        /// <param name="path">path of acpi name space</param>
        /// <param name="Type">type to pass or receive</param>
        /// <returns>string of type</returns>
        public string GetTypeStringByName(string path, ref ushort Type)
        {
            ushort nType = GetNameType(new StringBuilder(path));

            Type = nType;

            if (nType < 0x11)
            {
                return strTypes[nType];
                //return strTypes[28];
            } else if (nType > 0x7F && nType < 0x8B)
            {
                return strTypes[nType - 0x80 + 0x11];
            } else
            {
                return strTypes[28];
            }
        }
        /// <summary>
        /// Get the name path
        /// </summary>
        /// <param name="Path"></param>
        /// <param name="Names"></param>
        /// <returns>length of name</returns>
        public int GetNamePath(string Path, ref string Names)
        {
            int length = GetNamePathFromPath(new StringBuilder(Path), null);
            if (length > 0)
            {
                StringBuilder sNames = new StringBuilder(length * 8 + 8);
                length = GetNamePathFromPath(new StringBuilder(Path), sNames);
                Names = sNames.ToString();
            }
            return length;
        }
        /// <summary>
        /// Get type from path and name space
        /// </summary>
        /// <param name="Path">Path of name</param>
        /// <param name="Name">Name of acpi name space</param>
        /// <returns>type of name space</returns>
        public int GetTypeFromPath(ref string Path, string Name)
        {
            // while name is valid
            int Type = -1;
            string root = Path;
            while (true)
            {
                if ((Type = GetTypeByName(Path + Name)) != -1)
                {
                    break;
                }         
                if (root.Length > 4)
                {
                    root.Substring(0, root.Length - 4);
                } else
                {
                    break;
                }
            }
            if (Type != -1)
            {
                Path = root;
            }
            return Type;
        }
        /// <summary>
        /// Clean consumed resource
        /// </summary>
        public void Dispose()
        {
            // Dynamic libary will release the driver
            // CloseAcpiService(hDriver);
        }
    }
}
