using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    public class AcpiData
    {
        public AcpiDataType Type; // Type
        public UInt64 Value ;       // Int
        public string strValue ;    // String Data
        public byte[] bpValue ;     // Package or Buffer Data
        //public AcpiFieldUnit fieldUnit ;    // Field value
        public string Tag ;         // Tag name from asl code
        public string Name ;        // Name from asl code
        // Only set for a method arg for identify the arg name        
        //public string ArgName ;
        public AcpiLib acpiLib ;
        public AcpiPackage Pkg ;
        public AcpiData Alias ;
        public AcpiField Field ;

        //private const int AcpiMethodDataOffset = 4;
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="Value">initialize a integer type of acpi data</param>
        public AcpiData(UInt64 Value)
        {
            this.Type = AcpiDataType.Int;
            this.Value = Value;
        }
        /// <summary>
        /// default constructor
        /// </summary>
        public AcpiData()
        {

        }
        /// <summary>
        /// constructor - duplicate a acpi data
        /// </summary>
        /// <param name="data">source of data</param>
        public AcpiData(AcpiData data)
        {
            this.Name = data.Name;
            Duplicate(data);
        }
        /// <summary>
        /// Alias to a acpi data
        /// </summary>
        /// <param name="Alias">source of data to alias</param>
        public void SetAlias(AcpiData Alias)
        {
            this.Alias = Alias;
            this.Type  = AcpiDataType.Alias;
        }
        ///// <summary>
        ///// Get data from path - not used 
        ///// </summary>
        ///// <param name="path">full path</param>
        //public void GetDataFromPath(string path)
        //{
        //    //acpiLib.GetValue();
        //}
        /// <summary>
        /// Constructor acpi data from a method arg
        /// </summary>
        /// <param name="arg">arg</param>
        public AcpiData(AcpiMethodArg arg)
        {
            switch (arg.Type)
            {
                case 0:
                    this.Type = AcpiDataType.Int;
                    this.Value = arg.ulValue;
                    break;
                case 1:
                    Type = AcpiDataType.String;
                    this.strValue = new string(arg.strValue.ToArray());
                    break;
                case 2:
                    Type = AcpiDataType.Buffer;
                    this.bpValue = new byte[arg.bValue.Length];
                    Array.Copy(arg.bValue, 0, this.bpValue, 0, arg.bValue.Length);
                    break;
                case 3:
                    Type = AcpiDataType.Packge;
                    this.bpValue = new byte[arg.bValue.Length];
                    Array.Copy(arg.bValue, 0, this.bpValue, 0, arg.bValue.Length);
                    break;
                default:
                    //System.Diagnostics.Debug.Assert(false);
                    Log.Logs("public AcpiData(AcpiMethodArg arg)");
                    break;
            }
        }
        /// <summary>
        ///  Constructor acpi data from a string
        /// </summary>
        /// <param name="Value">string value</param>
        public AcpiData(String Value)
        {
            this.Type = AcpiDataType.String;
            this.strValue = Value;
        }
        /// <summary>
        ///  Constructor acpi data from a buffer or pacakge
        /// </summary>
        /// <param name="Value">raw data</param>
        /// <param name="bPackage">true:buffer/false:pacakge</param>
        public AcpiData(byte[] Value, Boolean bPackage)
        {
            if (bPackage) {
                this.Type = AcpiDataType.Packge;
            }
            else {
                this.Type = AcpiDataType.Buffer;
            }
            
            this.bpValue = Value;
        }
        /// <summary>
        /// Set a field
        /// </summary>
        /// <param name="Path">path of field</param>
        /// <param name="Name">name of field</param>
        public void SetField(string Path, string Name)
        {
            this.Type = AcpiDataType.FieldUnit;
            this.Name = Name;

        }
        /// <summary>
        /// constructor a acpi data from a acpi name
        /// </summary>
        /// <param name="apcilib">acpi libary</param>
        /// <param name="Path">path of name</param>
        /// <param name="Name">acpi name</param>
        public AcpiData(AcpiLib apcilib, string Path, string Name)
        {
            this.acpiLib = apcilib;
            QueryData(Path, Name);
        }
        /// <summary>
        /// constructor a acpi data from a acpi name space
        /// </summary>
        /// <param name="acpiNs">acpi name space</param>
        public AcpiData(AmlBuilder.AcpiNS acpiNs)
        {
            Type = AcpiDataType.Unknown;
            switch (acpiNs.Type)
            {
                case "Integer":
                    this.Type = AcpiDataType.Int;
                    Value = acpiNs.ulValue;
                    break;
                case "String":
                    this.Type = AcpiDataType.String;
                    strValue = new string(acpiNs.strValue.ToArray());
                    break;
                case "Buffer":
                    this.Type = AcpiDataType.Buffer;
                    this.bpValue = new byte[acpiNs.pbValue.Length];
                    Array.Copy(acpiNs.pbValue, 0, this.bpValue, 0, acpiNs.pbValue.Length);
                    break;
                case "Package":
                    this.Type = AcpiDataType.Packge;
                    this.bpValue = new byte[acpiNs.pbValue.Length];
                    Array.Copy(acpiNs.pbValue, 0, this.bpValue, 0, acpiNs.pbValue.Length);
                    break;
            }
        }           
        /// <summary>
        /// query a acpi data 
        /// </summary>
        /// <param name="Path">path of acpi name</param>
        /// <param name="Name">acpi name</param>
        public void QueryData(string Path, string Name)
        {
            string Root = Path;
            // when driver loaded just to a evaluation to get the value in runtime
            if (acpiLib == null)
            {
                return;
            }

            int nameType = acpiLib.GetTypeFromPath(ref Root, Name);
            if (nameType == -1)
            {
                // did not find the AcpiNS from current and uplevel
                return;
            }
            // TODO: NSKNKS Enhance the acpi type code
            switch (nameType)
            {
                case 1:
                    GetInt(Root + Name);
                    break;
                case 2:
                    GetString(Root + Name);
                    break;
                case 3:
                    GetBuffer(Root + Name);
                    break;
                case 4:
                    GetPackage(Root + Name);
                    break;
                case 0xE:   // buffer field
                case 5:     // operation region feild
                    this.Type = AcpiDataType.Int;
                    if (acpiLib != null && acpiLib.DriverLoaded())
                    {
                        // Driver is loaded get the value
                        IntPtr output = acpiLib.GetEvalOutput(Root + Name);
                        if (output != IntPtr.Zero)
                        {
                            FromAcpiOutput(output);
                        }                       

                        //if (GetBuffer(Root + Name, 5))
                        //{
                        //    // get the returned package from driver...
                        //    // display if possible
                        //    Log.Logs(this.bpValue);
                        //    DbgMessage("ERR 1");
                        //} else
                        //{
                        //    DbgMessage(Root + Name);
                        //}

                        //GetPackage(Root + Name);
                        //string result = "";
                        //if (acpiLib.GetEvalResult(Root + Name, ref result))
                        //{
                        //    //
                        //    result = result.Replace(" ", "");
                        //    result = result.Replace("\t", " ");
                        //    result = result.Replace("\r", " ");
                        //    result = result.Replace("\n", " ");
                        //    result = result.Replace("\t", " ");
                        //    // clear text
                        //    string[] values = result.Split(new char[] { ':' });
                        //    if (values != null && values.Length > 1)
                        //    {
                        //        switch (values[0])
                        //        {
                        //            // it's a integer
                        //            case "Integer":
                        //                UInt64 intValue = 0;
                        //                if (values[1].Contains("("))
                        //                {
                        //                    values[1] = values[1].Substring(0, values[1].IndexOf('('));
                        //                }
                        //                if (!IsIntString(values[1], ref intValue))
                        //                {
                        //                    DbgMessage("Not a integer");
                        //                } else
                        //                {
                        //                    this.Value = intValue;
                        //                }
                        //                break;
                        //            case "String":
                        //                this.strValue = values[1];
                        //                break;
                        //            default:
                        //                DbgMessage("ERR 2" + result);
                        //                break;
                        //        }
                        //    } else
                        //    {
                        //        if (values == null)
                        //        {
                        //            DbgMessage("ERR 1" + result);
                        //        } else
                        //        {
                        //            DbgMessage("ERR 1" + values.Length.ToString());
                        //        }
                        //    }
                        //}                        
                    }
                    break;
                case 9:
                    this.Type = AcpiDataType.Mutex;
                    break;
            }
            //if (nameType > 0 && nameType < 5)
            //{
            //    // Get Int, String, Buffer and Package data
            //    ushort nType = (ushort)nameType;
            //    IntPtr intPtr = acpiLib.GetValue(Root + Name, ref nType);

            //}

            if (acpiLib != null && acpiLib.DriverLoaded())
            {
                if (Type == AcpiDataType.FieldUnit)
                {
                    // it's field unit, need to get the refer Field and 
                    // OpRegion information for method step debug at offline mode
                    // OpRegion need to put a virtual value for offline debug mode
                    // get the FieldRoot, off line mode only care about the width and offset
                    // dont need to care about the access mode
                    // int nameType = acpiLib.GetTypeFromPath(ref Root, Name);
                }
            } 
        }
        /// <summary>
        /// get acpi data from a acpi output of acpilib
        /// </summary>
        /// <param name="intPtr"></param>
        /// <returns></returns>
        public Boolean FromAcpiOutput(IntPtr intPtr)
        {
            UInt32 val = (UInt32)Marshal.ReadInt32(intPtr);
            if (val != 0x426F6541)
            {
                return false;
            }
            //
            // Check the data length..
            //
            int Length = Marshal.ReadInt32(intPtr + 0x4);
            int Count = Marshal.ReadInt32(intPtr + 0x8);    // 0xC is type and Element Data Length;
            //DbgMessage("acpi output arg count " + Count.ToString());
            short Type = Marshal.ReadInt16(intPtr + 0xC);
            if (Count > 1)
            {
                Type = 3;   // count is more than 1, it's a package
            }
            if (Count > 0)
            {   
                // get the type of data
                
                short DataLength = Marshal.ReadInt16(intPtr + 0xE);
                switch (Type)
                {
                    case 0:
                        this.Value = (UInt64)Marshal.ReadInt64(intPtr + 0x10);
                        this.Type = AcpiDataType.Int;
                        break;
                    case 1:
                        this.Type = AcpiDataType.String;
                        this.strValue = Marshal.PtrToStringAnsi(intPtr + 0x10);
                        break;
                    case 2:
                        this.Type = AcpiDataType.Buffer;                        
                        bpValue = new byte[DataLength];
                        Marshal.Copy(intPtr+0x10, bpValue, 0, DataLength);
                        break;
                    case 3:
                        this.Type = AcpiDataType.Packge;
                        bpValue = new byte[Length];
                        Marshal.Copy(intPtr, bpValue, 0, Length);
                        if (ValidAcpiOutput())
                        {
                            Pkg = new AcpiPackage();
                            UInt32 pkgLength = BitConverter.ToUInt32(bpValue, 4);
                            byte[] pkgData = new byte[pkgLength - 12];
                            Array.Copy(bpValue, 12, pkgData, 0, pkgLength - 12);
                            AcpiMethodArg(Pkg, pkgData);
                        }
                        break;                        
                    case 4:
                        this.Type = AcpiDataType.Packge;
                        DbgMessage("PACKAGE EX TYPE");
                        bpValue = new byte[Length];
                        Marshal.Copy(intPtr, bpValue, 0, Length);
                        if (ValidAcpiOutput())
                        {
                            Pkg = new AcpiPackage();
                            UInt32 pkgLength = BitConverter.ToUInt32(bpValue, 4);
                            byte[] pkgData = new byte[pkgLength - 12];
                            Array.Copy(bpValue, 12, pkgData, 0, pkgLength - 12);
                            AcpiMethodArg(Pkg, pkgData);
                        }                        
                        break;
                    default:
                        DbgMessage("Not a valid data type");
                        break;
                }
            }
            return true;
        }
        /// <summary>
        /// get a integer value
        /// </summary>
        /// <param name="fullPath">full path of acpi name</param>
        private void GetInt(string fullPath)
        {
            ushort utype = 1;
            IntPtr intPtr = acpiLib.GetValue(fullPath, ref utype);
            if (intPtr != IntPtr.Zero)
            {
                FromAcpiOutput(intPtr);
                acpiLib.FreeArg(intPtr);
                this.Type = AcpiDataType.Int;
            }            
        }
        /// <summary>
        /// get a string value
        /// </summary>
        /// <param name="fullPath">full path of acpi name</param>
        private void GetString(string fullPath)
        {
            ushort utype = 2;
            IntPtr intPtr = acpiLib.GetValue(fullPath, ref utype);
            if (intPtr != IntPtr.Zero)
            {
                // acpi string is all ansi code
                FromAcpiOutput(intPtr);
                acpiLib.FreeArg(intPtr);
                this.Type = AcpiDataType.String;
            }
        }
        /// <summary>
        /// get a buffer or package value
        /// </summary>
        /// <param name="fullPath">full path of acpi name</param>
        /// <param name="type">buffer or package type</param>
        private Boolean GetBuffer(string fullPath, int type = 3)
        {
            ushort utype = (ushort)type;
            IntPtr intPtr = acpiLib.GetValue(fullPath, ref utype);
            if (intPtr != IntPtr.Zero)
            {
                FromAcpiOutput(intPtr);
                acpiLib.FreeArg(intPtr);                
                return true;
            }
            return false;
        }        
        /// <summary>
        /// validate the acpi output buffer
        /// </summary>
        /// <returns></returns>
        private Boolean ValidAcpiOutput()
        {
            UInt32 val = BitConverter.ToUInt32(bpValue, 0);
            if (val == 0x426F6541)
            {
                return true;
            }
            return false;
        }       
        /// <summary>
        /// parse a acpi method arguments
        /// </summary>
        /// <param name="pkg">data package</param>
        /// <param name="PkgByte">raw data</param>
        private void AcpiMethodArg(AcpiPackage pkg, byte[] PkgByte)
        {
            int Offset = 0;
            int Length = PkgByte.Length;
            AcpiPackage val = null;
            // handle the pkg
            while (Offset < Length)
            {
                UInt16 Type = BitConverter.ToUInt16(PkgByte, Offset);
                Offset += 2;
                UInt16 DataLength = BitConverter.ToUInt16(PkgByte, Offset);
                if (DataLength < 4)
                {
                    /*
                    from acpiioct.h, at least 4 bytes for union type combined with ULONG and UCHAR
                    typedef struct _ACPI_METHOD_ARGUMENT_V1 {
                        USHORT      Type;
                        USHORT      DataLength;
                        union {
                            ULONG   Argument;
                            _Field_size_bytes_(DataLength)
                            UCHAR       Data[ANYSIZE_ARRAY];
                        } DUMMYUNIONNAME;
                    } ACPI_METHOD_ARGUMENT_V1;
                    */

                    DataLength = 4;
                }
                Offset += 2;
                // handle package
                switch (Type)
                {
                    case 0:
                        if (Length < 8)
                        {
                            val = new AcpiPackage(
                                (UInt64)BitConverter.ToUInt32(PkgByte, Offset));
                        }
                        else
                        {
                            val = new AcpiPackage(
                                BitConverter.ToUInt64(PkgByte, Offset));
                        }
                        pkg.pkgs.Add(val);
                        break;
                    case 1:
                        val = new AcpiPackage(BitConverter.ToString(PkgByte,
                            Offset, DataLength));
                        pkg.pkgs.Add(val);
                        break;
                    case 2:
                        byte[] bufferData = new byte[DataLength];
                        Array.Copy(PkgByte, Offset,
                            bufferData, 0, DataLength);
                        val = new AcpiPackage(bufferData);
                        pkg.pkgs.Add(val);
                        break;
                    case 3:
                    case 4:
                        byte[] packageData = new byte[DataLength];
                        Array.Copy(PkgByte, Offset,
                            packageData, 0, DataLength);
                        val = new AcpiPackage();
                        val.Type = AcpiDataType.Packge;
                        pkg.pkgs.Add(val);
                        AcpiMethodArg(val, packageData);
                        break;
                }
                Offset += DataLength;
                //ACPI_METHOD_NEXT_ARGUMENT
            }
        }        
        
        /// <summary>
        /// get the pacakge data of acpi name
        /// </summary>
        /// <param name="fullPath">full path of acpi name</param>
        private void GetPackage(string fullPath)
        {
            GetBuffer(fullPath, 4);
        }

        //public object Get()
        //{
        //    switch (Type)
        //    {
        //        case AcpiDataType.Int:
        //            return Value;
        //        case AcpiDataType.String:
        //            return strValue;
        //        case AcpiDataType.Buffer:
        //            return bpValue;
        //        case AcpiDataType.Packge:
        //            return Pkg;
        //        default:
        //            return null;
        //    }            
        //}
        /// <summary>
        /// Duplicate a acpi data
        /// </summary>
        /// <param name="acpiData">acpi data</param>
        private void Duplicate(AcpiData acpiData)
        {
            this.Type = acpiData.Type;
            this.Value = acpiData.Value;
            if (acpiData.strValue != null)
            {
                this.strValue = new string(acpiData.strValue.ToArray());
            }
            if (acpiData.bpValue != null)
            {
                this.bpValue = new byte[acpiData.bpValue.Length];
                Array.Copy(acpiData.bpValue, 0,
                    this.bpValue, 0, acpiData.bpValue.Length);
            }
            this.Pkg = acpiData.Pkg;
        }
        /// <summary>
        /// assign the new value of acpi data
        /// </summary>
        /// <param name="acpiData">data</param>
        public void Assign(AcpiData acpiData)
        {
            if (acpiData == null)
            {
                return;
            }
            if (this.Name != null)
            {
                if (this.Name == "Result" ||
                    this.Name.StartsWith("InternalLocalData") ||
                    this.Name.StartsWith("InternalMethodArg"))
                {
                    // set the value normally
                    Duplicate(acpiData);
                }
            }
            else
            {
                Duplicate(acpiData);
            }     
        }
        /// <summary>
        /// set new value of acpi data with same type
        /// </summary>
        /// <param name="acpiData"></param>
        public void SetValue (AcpiData acpiData)
        {
            if (this.Type == AcpiDataType.FieldUnit)
            {
                // TODO: Write the field ojbect...     
                this.Value = acpiData.Value;
            }
            else
            {
                // Local Data can be assigned to any type, if not the type must be mathcing
                if (this.Name != null)
                {
                    if (this.Name.StartsWith("InternalLocalData") || this.Name.StartsWith("InternalMethodArg"))
                    {
                        this.Type = acpiData.Type;
                    }
                }
                // Int To Int 
                // String to string
                // String to Buffer
                // Buffer to String
                // Package to Buffer
                // Buffer to Package
                if (this.Type == acpiData.Type)
                {
                    this.Value = acpiData.Value;
                    if (acpiData.strValue != null)
                    {
                        this.strValue = new string(acpiData.strValue.ToArray());
                    }
                    if (acpiData.bpValue != null)
                    {
                        this.bpValue = new byte[acpiData.bpValue.Length];
                        Array.Copy(acpiData.bpValue, 0,
                            this.bpValue, 0, acpiData.bpValue.Length);
                    }
                    this.Pkg = acpiData.Pkg;
                }
                else if (this.Type == AcpiDataType.String && acpiData.Type == AcpiDataType.Buffer)
                {
                    // Buffer to string
                    this.strValue = BitConverter.ToString(acpiData.bpValue);
                }
                else if ((this.Type == AcpiDataType.Packge && acpiData.Type == AcpiDataType.Buffer) ||
                    acpiData.Type == AcpiDataType.Packge && this.Type == AcpiDataType.Buffer)
                {
                    // Buffer to Package
                    // or package to buffer
                    this.bpValue = new byte[acpiData.bpValue.Length];
                    Array.Copy(acpiData.bpValue, 0,
                        this.bpValue, 0, acpiData.bpValue.Length);
                }
                else if (acpiData.Type == AcpiDataType.String && this.Type == AcpiDataType.Buffer)
                {
                    // string to buffer
                    //this.bpValue = new byte[acpiData.strValue.Length];

                    this.bpValue = ASCIIEncoding.ASCII.GetBytes(acpiData.strValue);
                }
                else
                {
                    //System.Diagnostics.Debug.Assert(false, "Wrong ACPI Data Type Assignment" + this.ToString() + acpiData.ToString());
                    Log.Logs("Wrong ACPI Data Type Assignment" + this.ToString() + acpiData.ToString());
                }

            }
        }
        /// <summary>
        /// find the first non zero bit from left to right
        /// </summary>
        /// <returns>most left non zero bit</returns>
        public AcpiData FindLeft()
        {
            if (this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            UInt64 value = data.Value;
            UInt64 index = 0;
            for (index = 0; index < 64; index++)
            {
                if (((value >> (int)index) & 0x1) != 0)
                {
                    break;
                }
            }
            data.Value = index;
            return data;
        }
        /// <summary>
        /// find the first non zero bit from right to left
        /// </summary>
        /// <returns>most right non zero bit</returns>
        public AcpiData FindRight()
        {

            if (this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            UInt64 value = data.Value;
            int index = 0;
            for (index = 63; index >= 0; index--)
            {
                if ((value & ((UInt64)(1 << (int)index))) != 0)
                {
                    break;
                }
            }
            data.Value = (UInt64)index;
            return data;
        }
        /// <summary>
        /// Divide operation
        /// </summary>
        /// <param name="amlData">divisor</param>
        /// <returns>quoteient value</returns>
        public AcpiData Divide(AcpiData amlData)
        {
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            data.Value /= amlData.Value;            
            return data;
        }
        /// <summary>
        /// return the Remaider from a divide operation
        /// </summary>
        /// <param name="amlData">divisor</param>
        /// <returns>remaider value</returns>
        public AcpiData Mod(AcpiData amlData)
        {
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            data.Value = data.Value % amlData.Value;
            return data;
        }
        /// <summary>
        /// Add opertions
        /// </summary>
        /// <param name="amlData">add value</param>
        /// <returns>add result </returns>
        public AcpiData Add(AcpiData amlData)
        {
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            data.Value += amlData.Value;
            return data;
        }
        public AcpiData Multiple(AcpiData amlData)
        {
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            data.Value *= amlData.Value;
            return data;
        }
        public AcpiData Sub(AcpiData amlData)
        {
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            data.Value -= amlData.Value;
            return data;
        }
        public AcpiData And(AcpiData amlData)
        {
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            data.Value = data.Value & amlData.Value;
            return data;
        }        
        public AcpiData Or(AcpiData amlData)
        {
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            data.Value = data.Value | amlData.Value;
            return data;
        }
        private byte[] StringToBytes(string str)
        {
            return ASCIIEncoding.ASCII.GetBytes(str);
        }
        private byte[] ToBytes()
        {
            switch (Type)
            {
                case AcpiDataType.Int:
                    return BitConverter.GetBytes(Value);
                case AcpiDataType.String:
                    return StringToBytes(strValue);
                case AcpiDataType.Buffer:
                case AcpiDataType.Packge:
                    return bpValue;
            }
            return null;
        }
        public AcpiData LOp(AcpiData amlData, string OpName)
        {

            if (OpName == "LNot" && this.Type == AcpiDataType.Int)
            {
                AcpiData data = new AcpiData(this.Value);
                data.Value = BoolToUint64(amlData.Value == 0);
                return data;
            }
            if (this.Type != amlData.Type || this.Type != AcpiDataType.Int)
            {
                return null;
            }

            if (this.Type == AcpiDataType.Int)
            {
                AcpiData data = new AcpiData(this);
                byte[] src = amlData.ToBytes();
                byte[] dst = data.ToBytes();
                if (OpName == "LEqual")
                {
                    data.Value = BoolToUint64(amlData.Value == data.Value);
                }
                else if (OpName == "LNotEqual")
                {
                    data.Value = BoolToUint64(amlData.Value != data.Value);
                }
                else if (OpName == "LGreater")
                {
                    data.Value = BoolToUint64(amlData.Value > data.Value);
                }
                else if (OpName == "LGreaterEqual")
                {
                    data.Value = BoolToUint64(amlData.Value >= data.Value);
                }
                else if (OpName == "LLess")
                {
                    data.Value = BoolToUint64(amlData.Value < data.Value);
                }
                else if (OpName == "LLessEqual")
                {
                    data.Value = BoolToUint64(amlData.Value <= data.Value);
                }
                else if (OpName == "LOr")
                {
                    data.Value = BoolToUint64((amlData.Value | data.Value) > 0);
                }
                else if (OpName == "LAnd")
                {
                    data.Value = BoolToUint64((amlData.Value & data.Value) > 0);
                }
                return data;
            }
            return null;
        }
        public AcpiData Op(AcpiData amlData, string OpName)
        {
            AcpiData data = new AcpiData();
            if (OpName == "Not")
            {
                data.Assign(this);
                data.Value = ~amlData.Value;
                return data;
            }
            if (this.Type != amlData.Type)
            {

                return null;
            }
            if (this.Type == AcpiDataType.Int)
            {
                data.Assign(this);

                if (OpName == "Or")
                {
                    data.Value = amlData.Value | data.Value;
                }
                else if (OpName == "And")
                {
                    data.Value = amlData.Value & data.Value;
                }
                else if (OpName == "NAnd")
                {
                    data.Value = ~(amlData.Value & data.Value);
                }
                else if (OpName == "XOr")
                {
                    data.Value = amlData.Value ^ data.Value;
                }
            }
            return data;
        }
        public AcpiData Concat(AcpiData amlData)
        {
            AcpiData data = new AcpiData();

            if (this.Type != amlData.Type)
            {
                return null;
            }
            if (this.Type == AcpiDataType.Buffer)
            {
                data.Assign(this);
                //data.Value = amlData.Value & data.Value;
                byte[] bytes = new byte[data.bpValue.Length + amlData.bpValue.Length];
                data.bpValue.CopyTo(bytes, 0);
                amlData.bpValue.CopyTo(bytes, data.bpValue.Length);
                data.bpValue = bytes;
            }
            return data;
        }
        public AcpiData Copy(AcpiData amlData)
        {
            AcpiData data = new AcpiData();

            if (this.Type != amlData.Type)
            {
                return null;
            }
            this.Assign(amlData);
            return this;
        }
        public AcpiData ToString(int Length)
        {
            if (this.Type != AcpiDataType.Buffer)
            {
                return null;
            }
            if (Length > this.bpValue.Length)
            {
                return null;
            }
            AcpiData data = new AcpiData();
            data.Assign(this);
            data.Type = AcpiDataType.String;
            data.strValue = BitConverter.ToString(data.bpValue, 0, Length);
            return data;
        }
        public AcpiData ShiftLeft(int bits)
        {
            if (this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData();
            data.Assign(this);
            data.Value = data.Value << bits;
            return data;
        }
        public AcpiData ShiftRight(int bits)
        {
            if (this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this);
            data.Value = data.Value >> bits;
            return data;
        }
        public AcpiData ToBCD()
        {
            if (this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this);
            data.Type = AcpiDataType.Int;
            byte[] bytes = new byte[8];
            string Value = data.Value.ToString();
            int Length = Value.Length < 8 ? (int)Value.Length : 8;
            for (int Index = 0; Index < 8; Index++)
            {
                // fill the bytes
                if (Index % 1 == 0)
                {
                    bytes[Index / 2] = (byte)(Value[Index] - 0x30);
                }
                else
                {
                    bytes[Index / 2] = (byte)((Value[Index] - 0x30) << 4);
                }
            }
            data.Value = BitConverter.ToUInt64(bytes, 0);
            return data;
        }
        private Boolean IsIntString(string strValue, ref UInt64 iValue)
        {
            try
            {
                if (strValue.Equals("Zero"))
                {
                    iValue = 0;
                    return true;
                }
                else if (strValue.Equals("One"))
                {
                    iValue = 1;
                    return true;
                }
                else if (strValue.Equals("Ones"))
                {
                    iValue = 0xFFFFFFFFFFFFFFFF;
                    return true;
                }
                else if (strValue.StartsWith("0x"))
                {
                    iValue = UInt64.Parse(strValue.Substring(2), System.Globalization.NumberStyles.HexNumber);
                    return true;
                }
                iValue = UInt64.Parse(strValue, System.Globalization.NumberStyles.Integer);
                return true;
            }
            catch (Exception ex)
            {
                Log.Logs(ex.Message);
                return false;
            }
        }
        public AcpiData ToHexString()
        {
            if (this.Type != AcpiDataType.Buffer)
            {
                return null;
            }
            AcpiData data = new AcpiData(this);
            if (this.Type == AcpiDataType.Int)
            {
                data.strValue = string.Format("{0:X}", data.Value);
            }
            else if (this.Type == AcpiDataType.Buffer)
            {
                string val = "";
                foreach (byte bt in data.bpValue)
                {
                    string strBt = string.Format("{0:X}", bt);
                    val = strBt + val;
                }
                data.strValue = val;
            }
            else if (this.Type == AcpiDataType.String)
            {
                string val = "";
                char[] bytes = data.strValue.ToArray();
                foreach (char bt in data.bpValue)
                {
                    string strBt = string.Format("{0:X}", bt);
                    val = strBt + val;
                }
                data.strValue = val;
            }
            data.Type = AcpiDataType.String;
            return data;
        }
        public AcpiData ToDecString()
        {
            if (this.Type != AcpiDataType.Buffer)
            {
                return null;
            }
            AcpiData data = new AcpiData(this);
            if (this.Type == AcpiDataType.Int)
            {
                data.strValue = string.Format("{0}", data.Value);
            }
            else if (this.Type == AcpiDataType.Buffer)
            {
                int Length = data.bpValue.Length > 8 ? 8 : data.bpValue.Length;
                byte[] bytes = new byte[8];
                Array.Copy(data.bpValue, 0, bytes, 0, Length);
                UInt64 val = BitConverter.ToUInt64(bytes, 0);
                data.strValue = string.Format("{0}", val);
            }

            data.Type = AcpiDataType.String;
            return data;
        }
        public AcpiData ToBuffer()
        {
            if (this.Type != AcpiDataType.Buffer)
            {
                return null;
            }
            AcpiData data = new AcpiData(this);
            //data.Type = DataType.String;
            if (this.Type == AcpiDataType.Int)
            {
                if (data.Value < 0x100)
                {
                    data.bpValue = BitConverter.GetBytes((byte)data.Value);
                }
                else if (data.Value < 0x10000)
                {
                    data.bpValue = BitConverter.GetBytes((UInt32)data.Value);
                }
                else
                {
                    data.bpValue = BitConverter.GetBytes(data.Value);
                }
            }
            else if (this.Type == AcpiDataType.String)
            {
                data.bpValue = ASCIIEncoding.ASCII.GetBytes(data.strValue);
            }

            data.Type = AcpiDataType.Buffer;
            return data;
        }
        public AcpiData ToString()
        {
            if (this.Type != AcpiDataType.Buffer)
            {
                return null;
            }
            return ToString(this.bpValue.Length);
        }
        public Boolean Equal(AcpiData amlData)
        {
            if (Type != amlData.Type)
            {
                return false;
            }
            if (Type == AcpiDataType.Int)
            {
                return Value == amlData.Value;
            }
            else if (Type == AcpiDataType.String)
            {
                return strValue == amlData.strValue;
            }
            else if (Type == AcpiDataType.Buffer)
            {
                return bpValue == amlData.bpValue;
            }
            return false;
        }
        public AcpiData Mid(int Index, int Length)
        {
            AcpiData data = new AcpiData(this);
            if (this.Type == AcpiDataType.String)
            {
                data.strValue = data.strValue.Substring(Index, Length);
            }
            else if (this.Type == AcpiDataType.Buffer)
            {
                byte[] bytes = new byte[Length];
                Array.Copy(data.bpValue, Index, bytes, 0, Length);
                data.bpValue = bytes;
            }
            else
            {
                return null;
            }
            return data;
        }
        public AcpiData Size()
        {            
            if (this.Type == AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this);
            if (this.Type == AcpiDataType.String)
            {
                data.Value = (UInt64)data.strValue.Length;
            }
            else
            {
                data.Value = (UInt64)data.bpValue.Length;
            }
            data.Type = AcpiDataType.Int;
            return data;
        }
        public int IndexOf(string item)
        {
            //
            return 0;
        }
        public AcpiData Index(int Index)
        {
            AcpiData data = new AcpiData(this);
            if (this.Type == AcpiDataType.String)
            {
                data.Type = AcpiDataType.Int;
                data.Value = (UInt64)data.strValue[Index];
            }
            else if (this.Type == AcpiDataType.Packge)
            {
                // TODO, Package may be different type of data
                //System.Diagnostics.Debug.Assert(false);
                Log.Logs("public AcpiData Index(int Index)");
                data.Type = AcpiDataType.Int;
                data.Value = (UInt64)data.bpValue[Index];
            }
            else if (this.Type == AcpiDataType.Buffer)
            {
                data.Type = AcpiDataType.Int;
                data.Value = (UInt64)data.bpValue[Index];
            }
            else
            {
                return null;
            }
            return data;
        }
        public AcpiData FromBCD()
        {
            if (this.Type != AcpiDataType.Int)
            {
                return null;
            }
            AcpiData data = new AcpiData(this.Value);
            UInt64 bcd = 0;
            UInt64 mul = 1;
            byte[] bytes = BitConverter.GetBytes(data.Value);
            foreach (byte bt in bytes)
            {
                bcd *= 100;
                bcd += (10 * ((UInt64)(bt >> 4)));
                bcd += (UInt64)(bt & 0xf);
            }
            data.Value = bcd;
            return data;
        }
        private UInt64 BoolToUint64(Boolean val)
        {
            if (val)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
        public void PutValue(UInt64 value)
        {
            Value = value;
            strValue = string.Format("0x{0:X}", Value);
        }
        public void PutValue(string value)
        {
            strValue = value;
        }
        public void PutValue(byte[] value)
        {
            //bpVal = value;
            int Length = value.Length;
            bpValue = new byte[Length];
            Array.Copy(value, 0, bpValue, 0, Length);            
        }
        public void GetValue(ref UInt64 value)
        {
            value = Value;
        }
        public void GetValue(ref String value)
        {
            value = strValue;
        }
        public void GetValue(ref byte[] value)
        {
            value = bpValue;
        }
        public string GetDisplayValue()
        {
            return strValue;
        }
        private void DbgMessage(
            string message)
        {
            //System.Diagnostics.Debug.Assert(false);
            Log.Logs("");
        }
    }
    public class AcpiField
    {
        public AcpiOpRegion    opRegion;
        public FieldAccessType accType;
        public string Name;
        public List<AcpiFieldUnit> fieldUnits;
        public AcpiField (AcpiOpRegion acpiOpRegion)
        {
            opRegion = acpiOpRegion;
            Name = acpiOpRegion.Name;
            fieldUnits = new List<AcpiFieldUnit>();
        }
        public AcpiField()
        {
            fieldUnits = new List<AcpiFieldUnit>();
        }

        public void SetType(string strType)
        {
            try
            {
                Enum.TryParse(strType, out accType);
            }
            catch (Exception ex)
            {
                accType = FieldAccessType.AnyAcc;
                Log.Logs(ex.Message);
            }
            
        }
    }
    public class AcpiFieldUnit
    {
        //private AcpiField opField;
        public string Name;
        public UInt64 Offset;
        public UInt64 Width;
        private AcpiData acpiData;  // Operation Region or a buffer or a pacakge.
        public AcpiFieldUnit(string Name, AcpiData refer)
        {
            this.Name = Name;
            this.acpiData = refer;
            // access type and value
        }

        public AcpiFieldUnit()
        {
           
        }
    }
    public class AcpiBufferUnit
    {
        private AcpiField opField;
        public string Name;
        public UInt64 Offset;
        public UInt64 Width;
        private AcpiData acpiData;  // Operation Region or a buffer or a pacakge.
        public AcpiBufferUnit(string Name, AcpiData refer)
        {
            this.Name = Name;
            this.acpiData = refer;
            // access type and value
        }
    }
    public class AcpiOpRegion
    {
        public AcpiRegionType Type;
        public string Name;
        public UInt64 Address;
        public UInt64 Width;
        public AcpiOpRegion(string strType)
        {           
            try
            {
                Enum.TryParse(strType, out Type);
            }catch(Exception ex)
            {
                Log.Logs(ex.Message);
                Type = AcpiRegionType.Others;
            }
        }
    }
    public class AcpiPackage
    {
        public AcpiDataType Type ;  // Type
        public UInt64 Value;                    // Int
        public string strValue ;    // String Data
        public byte[] bpValue ;     // Package or Buffer Data
        public List<AcpiPackage> pkgs;
        public AcpiPackage()
        {
            pkgs = new List<AcpiPackage>();
        }
        public AcpiPackage(UInt64 val)
        {
            Value = val;
            Type = AcpiDataType.Int;
        }
        public AcpiPackage(string val)
        {
            strValue = val;
            Type = AcpiDataType.String;
        }
        public AcpiPackage(byte[] val)
        {
            bpValue = val;
            Type = AcpiDataType.Buffer;
        }
    }
    public enum AcpiDataType
    {
        Unknown = 0,
        Int = 1,
        String,
        Buffer,
        Packge,
        FieldUnit,
        Device,
        Event,
        Method,
        Mutex,
        OpRegion,
        PowerRes,
        Processor,
        ThermalZone,
        BufferField,
        DDBHandle,
        Timer,
        Debug,
        Alias = 0x80,
        DataAlias,
        BankField,
        Field,
        IndexField,
        Data,
        DataField,
        DataObj,
        Rev,
        External = 0x89
    };
    public enum AcpiArgType
    {        
        Int = 0,
        String,
        Buffer,
        Packge,
        PackgeEx,
    };
    public enum AcpiRegionType
    {
        SystemMemory,
        SystemIO,
        PCI_Config,
        EmbeddedControl,
        SMBus,
        SystemCMOS,
        PciBarTarget,
        IPMI,
        GeneralPurposeIO,
        GenericSerialBus,
        PCC,
        Others
    };

    public enum FieldAccessType { 
        AnyAcc, 
        ByteAcc, 
        WordAcc, 
        DWordAcc, 
        QWordAcc, 
        BufferAcc 
    };
}
