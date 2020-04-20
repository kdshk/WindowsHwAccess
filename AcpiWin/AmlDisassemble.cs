using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    enum AmlOpCode
    {
        ZeroOp = 0,
        OneOp,
        AliasOp = 6,
        NameOp = 8,
        BytePrefix = 0xA,
        WordPrefix,
        DWordPrefix,
        StringPrefix,
        QWordPrefix,
        ScopeOp = 0x10,
        BufferOp,
        PackageOp,
        VarPackageOp,
        MethodOp,
        ExternalOp,
        DualNamePrefix = 0x2E,
        MultiNamePrefix,
        ExtOpPrefix = 0x5B,
        RootChar = 0x5C,
        ParentPrefixChar = 0x5E,
        NameChar,
        Local0Op,
        Local1Op,
        Local2Op,
        Local3Op,
        Local4Op,
        Local5Op,
        Local6Op,
        Local7Op,
        Arg0Op,
        Arg1Op,
        Arg2Op,
        Arg3Op,
        Arg4Op,
        Arg5Op,
        Arg6Op,
        StoreOp = 0x70,
        RefOfOp,
        AddOp,
        ConcatOp,
        SubtractOp,
        IncrementOp,
        DecrementOp,
        MultiplyOp,
        DivideOp,
        ShiftLeftOp,
        ShiftRightOp,
        AndOp,
        NandOp,
        OrOp,
        NorOp,
        XorOp,
        NotOp,
        FindSetLeftBitOp,
        FindSetRightBitOp,
        DerefOfOp,
        ConcatResOp,
        ModOp,
        NotifyOp,
        SizeOfOp,
        IndexOp,
        MatchOp,
        CreateDWordFieldOp,
        CreateWordFieldOp,
        CreateByteFieldOp,
        CreateBitFieldOp,
        ObjectTypeOp,
        CreateQWordFieldOp,
        LandOp,
        LorOp,
        LnotOp,
        LEqualOp,
        LGreaterOp,
        LLessOp,
        ToBufferOp,
        ToDecimalStringOp,
        ToHexStringOp,
        ToIntegerOp,
        ToStringOp = 0x9C,
        CopyObjectOp,
        MidOp,
        ContinueOp,
        IfOp,
        ElseOp,
        WhileOp,
        NoopOp,
        ReturnOp,
        BreakOp,
        BreakPointOp = 0xCC,
        OnesOp = 0xFF
    }
    enum AmlExtOpCode
    {
        MutexOp = 1,
        EventOp,
        CondRefOfOp = 0x12,
        CreateFieldOp,
        LoadTableOp = 0x1F,
        LoadOp,
        StallOp,
        SleepOp,
        AcquireOp,
        SignalOp,
        WaitOp,
        ResetOp,
        ReleaseOp,
        FromBCDOp,
        ToBCDOp,
        UnloadOp = 0x2A,
        RevisionOp = 0x30,
        DebugOp,
        FatalOp,
        TimerOp,
        OpRegionOp = 0x80,
        FieldOp,
        DeviceOp,
        ProcessorOp,
        PowerResOp,
        ThermalZoneOp,
        IndexFieldOp,
        BankFieldOp,
        DataRegionOp
    }
    enum AmlScopeType
    {
        Scope,
        Device,
        ThermalZone,
        Method,
        Processor,
        PowerRes
    }
    class AmlDisassemble
    {
        struct AcpiPath
        {
            public string AcpiPathName;
            public AmlScopeType NameType;
            /// <summary>
            /// Construct 
            /// </summary>
            /// <param name="AcpiPathName">Acpi Path Name of stack</param>
            /// <param name="NameType">Named type of amlscope type</param>
            public AcpiPath (string AcpiPathName, AmlScopeType NameType)
            {
                this.AcpiPathName = AcpiPathName;
                this.NameType = NameType;
            }
        }
        /// <summary>
        /// Path stack
        /// </summary>
        private Stack<AcpiPath> AcpiPathStack = new Stack<AcpiPath>();
        /// <summary>
        /// Define the method collect prototype
        /// </summary>
        /// <param name="path">path of current defined method</param>
        /// <param name="methodName">method name</param>
        /// <param name="argCount">number of method args</param>
        public delegate void AmlMethodCollect(string path, string methodName, int argCount);
        /// <summary>
        /// defint the parser type of byte opcode handler
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        delegate string AmlParser(ref int offset);
        /// <summary>
        /// Aml Byte Code Handler method
        /// </summary>
        private Dictionary<byte, AmlParser> AmlByteCodeHandler;
        /// <summary>
        /// 0x5B Aml Extended opcode
        /// </summary>
        private Dictionary<byte, AmlParser> AmlByteExtCodeHandler;
        /// <summary>
        /// Aml Raw Binary
        /// </summary>
        private byte[] _AmlBinary;
        /// <summary>
        /// Used for disply aligment of scope
        /// </summary>
        private int _ScopeLevel = 1;
        /// <summary>
        /// method builder to query user defined method or superstring
        /// </summary>
        private AmlMethodBuilder _amlMethodBuilder;
        /// <summary>
        /// a callback function to collector method information
        /// </summary>
        public AmlMethodCollect MethodCollector;
        
        /// <summary>
        /// constructor
        /// </summary>
        public AmlDisassemble()
        {
            _AmlBinary = null;
            AmlByteCodeHandlerInitialize();
        }
        /// <summary>
        /// Decode aml to acpi table
        /// </summary>
        /// <param name="Table">acpi table include aml code</param>
        /// <returns>asl code</returns>
        public string DecodeAmlFromTable(byte[] Table)
        {
            //_AmlBinary = Aml;
            if (Table.Length > 0x24)
            {
                // a valid length of acpi aml code table, 0x24 is the standard acpi table header length
                _AmlBinary = Table.Skip(0x24).Take(Table.Length - 0x24).ToArray();

            }
            return ToString();
        }
        /// <summary>
        /// Decode aml to asl code 
        /// </summary>
        /// <param name="AmlBinary">aml binary code</param>
        /// <returns>asl code</returns>
        public string DecodeAml(byte[] AmlBinary)
        {
            _AmlBinary = AmlBinary;
            return ToString();
        }
        /// <summary>
        /// Override the to string based to disassemble the aml code
        /// </summary>
        /// <returns>Acpi source language code</returns>
        public override string ToString()
        {
            // used for code style aligment
            string strAslCode = "";
            // aml byte code offset
            int offset = 0;
            if (_AmlBinary == null)
            {
                return "Not a valid acpi machine language";
            }
            strAslCode += DecodeAml(ref offset);
            return strAslCode;
        }
        /// <summary>
        /// Decode aml to asl
        /// </summary>
        /// <param name="offset">starting offset of aml code</param>
        /// <param name="level">level of asl code alignment for easy view</param>
        /// <returns>asl code</returns>
        private string DecodeAml(ref int offset)
        {
            int length = _AmlBinary.Length - offset;
            return TermList(ref offset, ref length);
        }
        /// <summary>
        /// setup the aml method builder attributes
        /// </summary>
        /// <param name="amlMethodBuilder">aml builder obj to set</param>
        public void SetAmlMethodBuilder(AmlMethodBuilder amlMethodBuilder)
        {
            _amlMethodBuilder = amlMethodBuilder;
        }
        /// <summary>
        /// Get acpi path from acpi name stack
        /// </summary>
        /// <returns></returns>
        private string GetAcpiPath()
        {
            //AcpiPathStack.Peek();
            //return AcpiPathStack.Peek().AcpiPathName;
            string path = "";
            for (int idx = 0; idx < AcpiPathStack.Count; idx++)
            {
                path = AcpiPathStack.ElementAt(idx).AcpiPathName + path;
                if (AcpiPathStack.ElementAt(idx).NameType == AmlScopeType.Scope)
                {
                    break;
                }
                // 

            }
            return path;
        }
        /// <summary>
        /// Get number of space based on scope level
        /// </summary>
        /// <returns></returns>
        private string GetSpace ()
        {
            if (_ScopeLevel == 0)
            {
                return "";
            }
            string temp = "";           
            for (int idx = 0; idx < _ScopeLevel; idx ++)
            {
                temp += "    ";
            }
            return temp;
        }
        /// <summary>
        /// save the unknow aml code for debug and analyze
        /// </summary>
        /// <param name="data">full aml code</param>
        /// <param name="offset">offset that can not be recongnized</param>
        private void SaveUnknowAml(byte[] data, int offset)
        {
            string FileName = "AmlCode" + ".bin";
            if (File.Exists(FileName))
            {
                File.Delete(FileName);
            }
            using (BinaryWriter binWriter =
                new BinaryWriter(File.Open(FileName, FileMode.Create)))
            {
                // Write string   
                binWriter.Write(Encoding.ASCII.GetBytes("At Offset = " + offset.ToString()));
                binWriter.Write(data);
                binWriter.Close();
            }
        }
        private int GetPackageLength(ref int offset)
        {
            int PkgByte = 1;
            int length = 0;
            if ((_AmlBinary[offset] & 0xC0) == 0)
            {
                // only one byte
                length = (int)(_AmlBinary[offset] & 0x3F);
            }
            else
            {
                // multiple length
                PkgByte = (int)(_AmlBinary[offset] >> 6) + 1;
                switch (PkgByte)
                {
                    case 2:
                        length = (int)(_AmlBinary[offset] & 0xF)
                            + (int)(_AmlBinary[offset + 1]) * 0x10;
                        break;
                    case 3:
                        length = (int)(_AmlBinary[offset] & 0xF)
                            + (int)(_AmlBinary[offset + 1]) * 0x10
                            + (int)(_AmlBinary[offset + 2]) * 0x1000;
                        break;
                    case 4:
                        length = (int)(_AmlBinary[offset] & 0xF)
                            + (int)(_AmlBinary[offset + 1]) * 0x10
                            + (int)(_AmlBinary[offset + 2]) * 0x1000
                            + (int)(_AmlBinary[offset + 3]) * 0x100000;
                        break;
                }
            }
            offset += PkgByte;
            return length;
        }
        /// <summary>
        /// Generic function for all scope term arglist loop
        /// </summary>
        /// <param name="offset">offset of aml binary</param>
        /// <param name="PkgEnd">end offset of termarglist</param>
        /// <returns></returns>
        private string ScopeTermArgListLoop(ref int offset, int PkgEnd)
        {

            int LocalOffset = offset;
            string strTermArgList = "";
            if (offset < PkgEnd)
            {
                byte[] tempdata = new byte[PkgEnd - offset];
                tempdata = _AmlBinary.Skip(offset).Take(tempdata.Length).ToArray();
                _ScopeLevel++;
                while (offset < PkgEnd)
                {
                    strTermArgList += GetSpace();
                    strTermArgList += TermArgList(ref offset);
                    strTermArgList += "\n";
                }
                _ScopeLevel--;
            }
            return strTermArgList;
        }

        #region Namespace Modifier Objects Encoding
        /// <summary>
        /// is a name space modifyer
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
        /// <returns></returns>
        private bool IsNamespaceModiferObject(int offset)
        {
            string strValidCode = "0x6 0x8 0x10";
            return strValidCode.Contains(
                string.Format("0x{0:x}", _AmlBinary[offset]));
        }
        /// <summary>
        /// Disassemble a alias opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string AliasOpcodeHandler(ref int offset)
        {
            // Alias one string to another
            offset++;
            return string.Format("Alias({0}, {1})", NameString(ref offset),
                NameString(ref offset));
        }
        /// <summary>
        /// Disassemble a scope opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string ScopeOpcodeHandler(ref int offset)
        {
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            string Name = NameString(ref offset);
            scope = string.Format("Scope({0})", Name) + "{\n";
            // push a scope change.....
            // current scope 
            AcpiPathStack.Push(new AcpiPath(Name, AmlScopeType.Scope));
            scope += ScopeTermArgListLoop(ref offset, PkgEnd);
            scope += GetSpace() + "}";
            AcpiPathStack.Pop();
            return scope;
        }
        /// <summary>
        /// Disassemble a data ref object
        /// </summary>
        /// <param name="offset">offset of raw aml binary</param>
        /// <returns></returns>
        private string DataRefObj(ref int offset)
        {
            return AmlOpcodeHandler(ref offset);
        }
        /// <summary>
        /// Disassemble a name opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string NameOpcodeHandler(ref int offset)
        {
            
            offset++;
            string strName = NameString(ref offset);
            //int debug = 0;
            //if (strName == "_UID")
            //{
            //    debug = 1;
            //}
            return string.Format("Name({0}, {1})", strName,
               DataRefObj(ref offset));
        }
        #endregion

        #region Named Objects Encoding
        /// <summary>
        /// is named Objects or not
        /// </summary>
        /// <param name="offset">offset or raw aml binary</param>
        /// <returns></returns>
        private bool IsNamedObjects(int offset)
        {
            if (_AmlBinary[offset] == 0x5B)
            {
                string strValidCode = "0x83 0x85 0x87 0x88 0x82 0x02 0x81 0x86 0x01 0x80 0x84";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset + 1]));
            }
            else
            {
                string strValidCode = "0x8D 0x8C 0x8A 0x8F 0x8B 0x15 0x14";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset]));
            }
        }
        /// <summary>
        /// parser bankfield op
        /// </summary>
        /// <param name="offset">offset or raw aml binary</param>
        /// <returns>asl code</returns>
        private string BankFieldOpcodeHandler(ref int offset)
        {
            offset++;
            int Length = GetPackageLength(ref offset);
            int end = offset + Length - 1;

            //string FeildName = NameString(ref offset);
            string strFields = string.Format("{0}BankField({1},{2},{3},{4})", "", NameString(ref offset), NameString(ref offset),
                TermArg(ref offset), FieldFlags(_AmlBinary[offset]));
            offset++;
            // point to field lists now
            if (offset < end)
            {
                string Format = "\n{0}{1},{2}";
                string strField = "";
                strFields += "\n" + GetSpace() + "{";
                _ScopeLevel++;
                strFields += FieldList(ref offset, end);
                _ScopeLevel--;
                strFields += string.Format("\n{0}", GetSpace()) + "}";
            }
            return strFields;
        }
        private string CreateDWordOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("CreateDWordField({0},{1},{2})", TermArg(ref offset), TermArg(ref offset), NameString(ref offset));
        }
        private string CreateWordOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("CreateWordField({0},{1},{2})", TermArg(ref offset), TermArg(ref offset), NameString(ref offset));
        }
        private string CreateBitOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("CreateBitField({0},{1},{2})", TermArg(ref offset), TermArg(ref offset), NameString(ref offset));
        }
        private string CreateByteOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("CreateByteField({0},{1},{2})", TermArg(ref offset), TermArg(ref offset), NameString(ref offset));
        }
        private string CreateQWordOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("CreateQWordField({0},{1},{2})", TermArg(ref offset), TermArg(ref offset), NameString(ref offset));
        }
        private string CreateFieldOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("CreateField({0},{1},{2},{3})", TermArg(ref offset), TermArg(ref offset), TermArg(ref offset), NameString(ref offset));
        }
        private string DataRegionOpcodeHandler(ref int offset)
        {
            // 4 data, A Name or a OpCode, Method returned data
            offset++;
            string opregion = string.Format("DataRegion({0},{1},{2},{3})", NameString(ref offset),
                TermArg(ref offset), TermArg(ref offset), TermArg(ref offset));
            return opregion;
        }
        private string GetExternalObjectType(ref int offset)
        {
            byte value = _AmlBinary[offset];
            string[] ObjectType = {"UnknowObj","IntObj", "StrObj", "BuffObj","PackageObj","FieldUnitObj","DeviceObj","EventObj",
            "MethodObj","MutexObj","OpRegionObj","PowerResObj","ProcessorObj","ThermalZoneObj","BuffFieldObj","DDBHandleObh"};
            offset++;
            if (ObjectType.Length > value)
            {
                if (value == 8)
                {
                    offset++;
                    return ObjectType[value] + ",ArgCount<" + _AmlBinary[offset - 1].ToString() + ">";
                }
                offset++;
                return ObjectType[value];
            }
            else
            {
                offset++;
                return _AmlBinary[offset - 2].ToString();
            }
        }
        private string ExternalOpcodeHandler(ref int offset)
        {
            offset++;
            string Name = NameString(ref offset);
            byte ExternalType = _AmlBinary[offset];
            if (ExternalType == 8)
            {
                int argCount = _AmlBinary[offset + 1];
                //if(Name == "\\HIWC")
                //{
                //    argCount = _AmlBinary[offset + 1];
                //}
                MethodCollector?.Invoke(GetAcpiPath(), Name, argCount);
            }
            string strOp = string.Format("External({0},{1})", Name, GetExternalObjectType(ref offset));
            return strOp;
        }
        private string ThermalZoneOpcodeHandler(ref int offset)
        {
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            string Name = NameString(ref offset);
            scope = string.Format("ThermalZone({0})", Name) + "{\n";
            AcpiPathStack.Push(new AcpiPath(Name, AmlScopeType.ThermalZone));
            scope += ScopeTermArgListLoop(ref offset, PkgEnd);
            scope += GetSpace() + "}";
            AcpiPathStack.Pop();
            return scope;
        }
        private string DeviceOpcodeHandler(ref int offset)
        {
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            string Name = NameString(ref offset);
            scope = string.Format("Device({0})", Name) + "{\n";
            AcpiPathStack.Push(new AcpiPath(Name, AmlScopeType.Device));
            scope += ScopeTermArgListLoop(ref offset, PkgEnd);
            scope += GetSpace() + "}";
            AcpiPathStack.Pop();
            return scope;
        }
        private string ProcessorOpcodeHandler(ref int offset)
        {
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            string Name = NameString(ref offset);
            byte ProcId = _AmlBinary[offset];
            offset++;
            UInt32 PblkAddr = BitConverter.ToUInt32(_AmlBinary, offset);
            offset += 4;
            byte PblkLen = _AmlBinary[offset];
            offset++;
            scope = string.Format("Processor({0}, 0x{1:X2}, 0x{2:X8}, 0x{3:X2})", Name, ProcId, PblkAddr, PblkLen) + "{\n";
            AcpiPathStack.Push(new AcpiPath(Name, AmlScopeType.Processor));
            scope += ScopeTermArgListLoop(ref offset, PkgEnd);
            scope += GetSpace() + "}";
            AcpiPathStack.Pop();
            return scope;
        }
        private string EventOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Event({0})", SuperName(ref offset));
        }
        private string OpRegionOpcodeHandler(ref int offset)
        {
            // 4 data, A Name or a OpCode, Method returned data
            offset++;
            string opregion = string.Format("OperationRegion({0},{1},{2},{3})", NameString(ref offset),
                RegionSpace(ref offset), TermArg(ref offset), TermArg(ref offset));
            // now point to Field Handler if had
            // if (_AmlBinary[offset] == (byte)AmlOpCode.ExtOpPrefix && _AmlBinary[offset] == (byte)AmlExtOpCode.FieldOp)
            // {
            //    // it's field then parse the field lists
            //    opregion += FieldHandler(ref offset);
            // }
            return opregion;
        }
        private string FieldAccessType(byte accessType)
        {
            switch (accessType)
            {
                case 0:
                    return "AnyAcc";
                case 1:
                    return "ByteAcc";
                case 2:
                    return "WordAcc";
                case 3:
                    return "DWordAcc";
                case 4:
                    return "QWordAcc";
                case 5:
                    return "BufferAcc";
                default:
                    return "Reserved";
            }
        }
        private string FieldFlags(byte flags)
        {
            byte AccessType = (byte)(flags & 0xF);
            if ((flags & 0x80) != 0)
            {
                throw new InvalidOperationException("Inavlid field flag on bit 7, must be zero");
            }
            string strFlags = FieldAccessType(AccessType) + ",";
            //LockRule 
            if ((flags & 0x01) == 0)
            {
                strFlags += "NoLock,";
            }
            else
            {
                strFlags += "Lock,";
            }
            byte UpdateRule = (byte)((flags >> 5) & 0x3);
            if (UpdateRule == 0)
            {
                strFlags += "Preserve";
            }
            else if (UpdateRule == 1)
            {
                strFlags += "WriteAsOnes";
            }
            else if (UpdateRule == 2)
            {
                strFlags += "WriteAsZeros";
            }
            return strFlags;
        }
        private string FieldList(ref int offset, int end)
        {
            string Format = "\n{0}{1},{2}";
            string strFields = "";
            string strField = "";
            while (offset < end)
            {
                //FieldElement := NamedField | ReservedField | AccessField | ExtendedAccessField |
                //ConnectField
                switch (_AmlBinary[offset])
                {
                    case 0:
                        // ReservedField
                        offset++;
                        strField = string.Format(Format, GetSpace(), "", GetPackageLength(ref offset));
                        break;
                    case 1:
                        // AccessField
                        {
                            offset++;
                            //byte accessType =
                            //strField = string.Format(Format, GetSpace(), FieldAccessType(, GetPackageLength(ref offset));
                        }
                        break;
                    case 2:
                        // ConnectField
                        offset++;
                        break;
                    case 3:
                        //ExtendedAccessField
                        offset++;
                        break;
                    default:
                        if (IsNameObject(offset))
                        {
                            // It's a valid NamedField 
                            //strField = NameString(ref offset);
                            strField = string.Format(Format, GetSpace(), NameString(ref offset), GetPackageLength(ref offset));
                        }
                        else
                        {
                            throw new InvalidOperationException("An invalid field type found in field lists");
                        }
                        break;
                }
                Format = ",\n{0}{1},{2}";
                strFields += strField;

            }
            return strFields;
        }
        private string IndexFieldOpcodeHandler(ref int offset)
        {
            offset++;
            int Length = GetPackageLength(ref offset);
            int end = offset + Length - 1;

            //string FeildName = NameString(ref offset);
            string strFields = string.Format("{0}IndexField({1},{2},{3},{4})", "", NameString(ref offset), NameString(ref offset),
                FieldFlags(_AmlBinary[offset]));
            offset++;
            // point to field lists now
            if (offset < end)
            {
                string Format = "\n{0}{1},{2}";
                string strField = "";
                strFields += "\n" + GetSpace() + "{";
                _ScopeLevel++;
                strFields += FieldList(ref offset, end);
                _ScopeLevel--;
                strFields += string.Format("\n{0}", GetSpace()) + "}";
            }
            return strFields;
        }
        private string FieldOpcodeHandler(ref int offset)
        {
            //string strFields = "{\n";
            offset++;
            int end = offset;
            int Length = GetPackageLength(ref offset);
            end += Length;

            //string FeildName = NameString(ref offset);
            string strFields = string.Format("Field({0},{1})", NameString(ref offset), FieldFlags(_AmlBinary[offset]));
            offset++;
            // point to field lists now
            if (offset < end)
            {
                string Format = "\n{0}{1},{2}";
                string strField = "";
                strFields += "\n" + GetSpace() + "{";
                _ScopeLevel++;
                strFields += FieldList(ref offset, end);
                _ScopeLevel--;
                strFields += string.Format("\n{0}", GetSpace()) + "}";
            }
            return strFields;
        }
        private string ResourceType(byte resType)
        {
            switch (resType)
            {
                case 0:
                    return "SystemMemory";
                case 1:
                    return "SystemIO";
                case 2:
                    return "PCI_Config";
                case 3:
                    return "EmbeddedControl";
                case 4:
                    return "SMBus";
                case 5:
                    return "SystemCMOS";
                case 6:
                    return "PciBarTarget";
                case 7:
                    return "IPMI";
                case 8:
                    return "GeneralPurposeIO";
                case 9:
                    return "GenericSerialBus";
                case 0xA:
                    return "PCC";
                default:
                    if (resType >= 0x80)
                    {
                        return "OEM Defined";
                    }
                    break;
            }
            return "Reserved";
        }
        private string RegionSpace(ref int offset)
        {
            byte space = _AmlBinary[offset];
            offset++;

            return ResourceType(space);
        }
        private string GetMethodFlag(ref int offset)
        {
            byte flag = _AmlBinary[offset];
            offset++;
            return string.Format("{0},{1}, {2}", flag & 0x7, (flag & 0x8) == 0 ? "NotSerialized" : "Serialized", flag >> 4);
        }
        
        /// <summary>
        /// Disassemble a method opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// 
        private string MethodOpcodeHandler(ref int offset)
        {
            int LocalOffset = offset;
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            string MethodName = NameString(ref offset);
            int argCount = _AmlBinary[offset] & 0x7;
            //if (MethodName.Contains("KTOC"))
            //{
            //    MethodCollector?.Invoke(GetAcpiPath(), MethodName, argCount);
            //}
            MethodCollector?.Invoke(GetAcpiPath(), MethodName, argCount);
            AcpiPathStack.Push(new AcpiPath(MethodName, AmlScopeType.Method));
            scope = string.Format("Method({0},{1})", MethodName, GetMethodFlag(ref offset)) + "{\n";
            scope += ScopeTermArgListLoop(ref offset, PkgEnd);
            scope += GetSpace() + "}";
            AcpiPathStack.Pop();
            return scope;
        }
        private string MutexfOpcodeHandler(ref int offset)
        {
            offset++;
            string Name = SuperName(ref offset);
            byte value = _AmlBinary[offset];
            offset++;
            return string.Format("Mutext({0}, {1})", Name, value & 0xF);
        }
        private string PowerResOpcodeHandler(ref int offset)
        {
            string strTermArg = "";
            offset++;
            int PkgEnd = offset;
            int Length = GetPackageLength(ref offset);
            PkgEnd += Length;
            string Name = NameString(ref offset);
            byte SystemLevel = _AmlBinary[offset];
            offset++;
            UInt16 ResourceOrder = BitConverter.ToUInt16(_AmlBinary, offset);
            offset += 2;
            strTermArg = string.Format("PowerRes({0},{1},{2}){3}", Name, SystemLevel, ResourceOrder, "{\n");
            AcpiPathStack.Push(new AcpiPath(Name, AmlScopeType.PowerRes));
            strTermArg += ScopeTermArgListLoop(ref offset, PkgEnd);
            strTermArg += GetSpace() + "}";
            AcpiPathStack.Pop();
            return strTermArg;
        }
        #endregion

        #region Type 1 Opcodes Encodig
        /// <summary>
        /// is a type 1 opcode
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
        /// <returns></returns>
        private bool IsType1Opcode(int offset)
        {
            if (_AmlBinary[offset] == 0x5B)
            {
                string strValidCode = "0x32 0x20 0x27 0x26 0x24 0x22 0x21 0x2a";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset + 1]));
            }
            else
            {
                string strValidCode = "0xa2 0xa4 0xa5 0xcc 0x9f 0xa1 0xa0 0xa3 0x86";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset]));
            }
        }
        private string BreakOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Break");
        }
        private string WhileOpcodeHandler(ref int offset)
        {
            string strTermArg = "";
            offset++;
            int PkgEnd = offset;
            int Length = GetPackageLength(ref offset);
            PkgEnd += Length;
            strTermArg = string.Format("While({0}){1}", TermArg(ref offset), " {\n");
            strTermArg += ScopeTermArgListLoop(ref offset, PkgEnd);
            strTermArg += GetSpace() + "}";
            return strTermArg;
        }
        private string StallOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Stall({0})", TermArg(ref offset));
        }
        private string SleepOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Sleep({0})", TermArg(ref offset));
        }
        private string SignalOpcodeHandler(ref int offset)
        {
            offset++;
            string Name = SuperName(ref offset);
            return string.Format("Signal({0})", Name);
        }
        private string ReturnOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Return({0})", TermArg(ref offset));
        }
        private string ResetOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Reset({0})", SuperName(ref offset));
        }
        private string ReleaseOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Release({0})", SuperName(ref offset));
        }
        private string NotifyOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Notify({0},{1})", SuperName(ref offset), TermArg(ref offset));
        }
        private string NoopOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Noop()");
        }
        private string LoadOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Load({0},{1})", NameString(ref offset), SuperName(ref offset));
        }
        private string UnloadOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Unload({0})", SuperName(ref offset));
        }
        /// <summary>
        /// Disassemble a if opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string IfOpcodeHandler(ref int offset)
        {
            string IfOp = "";
            offset++;
            int PkgEnd = offset;
            int Length = GetPackageLength(ref offset);
            PkgEnd += Length;
            IfOp = string.Format("If({0}){1}", TermArg(ref offset), " {\n");
            //if (offset < PkgEnd)
            //{
            //    _ScopeLevel++;
            //    while (offset < PkgEnd)
            //    {
            //        IfOp += GetSpace();
            //        IfOp += TermArgList(ref offset);
            //        IfOp += "\n";
            //    }
            //    _ScopeLevel--;
            //} 
            IfOp += ScopeTermArgListLoop(ref offset, PkgEnd);
            IfOp += GetSpace() + "}";
            return IfOp;
        }
        private string ElseOpcodeHandler(ref int offset)
        {
            string strTermArg = "";
            offset++;
            int PkgEnd = offset;
            int Length = GetPackageLength(ref offset);
            PkgEnd += Length;
            if (_AmlBinary[offset] == (byte)AmlOpCode.IfOp)
            {
                strTermArg = "Else " + IfOpcodeHandler(ref offset);
            }
            else
            {
                strTermArg = "Else{\n";
                strTermArg += ScopeTermArgListLoop(ref offset, PkgEnd);
                strTermArg += GetSpace() + "}";
            }
            return strTermArg;
        }
        private string FatalOpodeHandler(ref int offset)
        {
            offset++;
            byte FatalType = _AmlBinary[offset];
            offset++;
            UInt16 FatalCode = BitConverter.ToUInt16(_AmlBinary, offset);
            offset += 2;
            return string.Format("Fatal({0}, {1}, {2})", FatalType, FatalCode, TermArg(ref offset));  // Optional: Change to Revision()
        }
        private string ContinueOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Continue");
        }
        private string BreakPointOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("BreakPoint()");
        }

        #endregion

        #region Type 2 Opcodes Encodig
        /// <summary>
        /// is a type 2 opcode
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
        /// <returns></returns>
        private bool IsType2Opcode(int offset)
        {
            if (_AmlBinary[offset] == 0x5B)
            {
                string strValidCode = "0x23 0x12 0x28 0x1f 0x33 0x25";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset + 1]));
            }
            else
            {
                string strValidCode = "0x99 0x9c 0x7f 0x96 0x97 0x98 0x87 0x70 0x74 0x79 0x7a 0x12 0x13 0x71 0x85 0x77 0x7c 0x7e 0x80 0x8e 0x7d 0x91 0x89 0x9e 0x75 0x88 0x90 0x93 0x94 0x95 0x92 0x81 0x82 0x78 0x83 0x76 0x72 0x7b 0x11 0x73 0x84 0x9d";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset]));
            }
        }
        /// <summary>
        /// Is Type 3 opcode - Type 3 opcode return in Integer value and can be 
        /// used in an expression that evaluates to a constant 
        /// </summary>
        /// <param name="offset">offset of amlcode</param>
        /// <returns></returns>
        private bool IsType3Opcode(int offset)
        {
            if (_AmlBinary[offset] == 0x5B)
            {
                byte[] extOpcode = new byte[] {
                    (byte)AmlExtOpCode.FromBCDOp,
                    (byte)AmlExtOpCode.ToBCDOp
                };
                return extOpcode.Contains(_AmlBinary[offset]);
            }
            else
            {
                byte[] Opcode = new byte[] {
                    (byte)AmlOpCode.AddOp,
                    (byte)AmlOpCode.AndOp,
                    (byte)AmlOpCode.DecrementOp,
                    (byte)AmlOpCode.DerefOfOp,
                    (byte)AmlOpCode.DivideOp,
                    (byte)AmlOpCode.FindSetLeftBitOp,
                    (byte)AmlOpCode.FindSetRightBitOp,
                    (byte)AmlOpCode.IncrementOp,
                    (byte)AmlOpCode.LandOp,
                    (byte)AmlOpCode.LEqualOp,
                    (byte)AmlOpCode.LGreaterOp,
                    (byte)AmlOpCode.LLessOp,
                    (byte)AmlOpCode.LnotOp,
                    (byte)AmlOpCode.LorOp,
                    (byte)AmlOpCode.MatchOp,
                    (byte)AmlOpCode.ModOp,
                    (byte)AmlOpCode.MultiplyOp,
                    (byte)AmlOpCode.NandOp,
                    (byte)AmlOpCode.NorOp,
                    (byte)AmlOpCode.NotOp,
                    (byte)AmlOpCode.OrOp,
                    (byte)AmlOpCode.ShiftLeftOp,
                    (byte)AmlOpCode.ShiftRightOp,
                    (byte)AmlOpCode.SubtractOp,
                    (byte)AmlOpCode.ToIntegerOp,
                    (byte)AmlOpCode.XorOp
                };
                return Opcode.Contains(_AmlBinary[offset]);
            }
        }
        /// <summary>
        /// Is Type 4 opcode - Type 4 opcode return in String value and can be 
        /// used in an expression that evaluates to a constant 
        /// </summary>
        /// <param name="offset">offset of amlcode</param>
        /// <returns></returns>
        private bool IsType4Opcode(int offset)
        {
            // printf term
            {
                byte[] Opcode = new byte[] {
                    (byte)AmlOpCode.ConcatOp,
                    (byte)AmlOpCode.ConcatResOp,
                    (byte)AmlOpCode.DerefOfOp,
                    (byte)AmlOpCode.MidOp,
                    (byte)AmlOpCode.ToDecimalStringOp,
                    (byte)AmlOpCode.ToHexStringOp,
                    (byte)AmlOpCode.ToStringOp
                };
                return Opcode.Contains(_AmlBinary[offset]);
            }
        }

        /// <summary>
        /// Is Type 5 opcode - Type 5 opcode return in Buffer value and can be 
        /// used in an expression that evaluates to a constant 
        /// </summary>
        /// <param name="offset">offset of amlcode</param>
        /// <returns></returns>
        private bool IsType5Opcode(int offset)
        {
            // Resource template term, kind of buffer
            // To PLD Term, To UUID Term, Uncode Term
            {
                byte[] Opcode = new byte[] {
                    (byte)AmlOpCode.ConcatOp,
                    (byte)AmlOpCode.ConcatResOp,
                    (byte)AmlOpCode.DerefOfOp,
                    (byte)AmlOpCode.MidOp,
                    (byte)AmlOpCode.ToBufferOp,
                    //(byte)AmlOpCode.BufferOp,
                    //(byte)AmlOpCode.PackageOp,
                    //(byte)AmlOpCode.VarPackageOp
                };
                return Opcode.Contains(_AmlBinary[offset]);
            }
        }
        private string AcquireOpcodeHandler(ref int offset)
        {
            offset++;
            string Name = SuperName(ref offset);
            UInt16 Timeout = BitConverter.ToUInt16(_AmlBinary, offset);
            offset += 2;
            return string.Format("Acquire({0},{1})", Name, Timeout);
        }
        private string SimpleDataOpcodeHandler(string op, ref int offset)
        {
            offset++;
            string arg1 = TermArg(ref offset);
            if (IsNullName(offset))
            {
                offset++;
                return string.Format("{0}({1})", op, arg1);
            }
            if (IsTarget(offset))
            {
                return string.Format("{0}({1}, {2})", op, arg1, SuperName(ref offset));
            }
            return string.Format("{0}({1}, {2})", op, arg1, TermArg(ref offset));
        }
        private string MathOpcodeHandler(string operate, ref int offset)
        {
            offset++;
            string arg1 = TermArg(ref offset);
            string arg2 = TermArg(ref offset);
            if (IsNullName(offset))
            {
                offset++;
                return string.Format("{0}({1}, {2})", operate, arg1, arg2);
            }
            if (IsTarget(offset))
            {
                // not a target, possible a user defined path of arg1....
                return string.Format("{0}({1}, {2},{3})", operate, arg1, arg2, SuperName(ref offset));
            }
            return string.Format("{0}({1}, {2},{3})", operate, arg1, arg2, TermArg(ref offset));
        }
        private string AddOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Add", ref offset);
        }
        private string AndOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("And", ref offset);
        }
        /// <summary>
        /// Disassemble a buffer opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string BufferOpcodeHandler(ref int offset)
        {
            string strAsl = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            strAsl = string.Format("Buffer({0}) ", TermArg(ref offset)) + "{\n";
            //byte[] buffer = M
            if (MethodCollector == null)
            {
                int idx = 0;
                _ScopeLevel++;
                strAsl += GetSpace();
                while (offset < PkgEnd)
                {
                    //strAsl += TermArgList(ref offset);


                    strAsl += string.Format("0x{0:X2}", _AmlBinary[offset]);
                    idx++;
                    offset++;
                    if (offset != PkgEnd)
                    {
                        if (idx % 8 == 0)
                        {
                            strAsl += ",\n" + GetSpace();
                        }
                        else
                        {
                            strAsl += ", ";
                        }
                    }
                }
                _ScopeLevel--;
            }
            offset = PkgEnd;
            strAsl += "\n" + GetSpace() + "}";
            return strAsl;
        }
        private string ConcatOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Concat", ref offset);
        }
        private string ConcatResOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("ConcatRes", ref offset);
        }
        private string CondRefOfOpcodeHandler(ref int offset)
        {
            offset++;
            string Name = SuperName(ref offset);
            if (IsNullName(offset))
            {
                offset++;
                return string.Format("CondRefOf({0})", Name);
            }
            else
            {
                return string.Format("CondRefOf({0}, {1})", Name, SuperName(ref offset));
            }
        }
        private string CopyObjectOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("CopyObject", ref offset);
        }
        private string DecrementOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Increment({0})", SuperName(ref offset));
        }
        private string DivideOpcodeHandler(ref int offset)
        {
            // divide has 2 target which is different from other math op
            //DivideOp Dividend Divisor Remainder Quotient
            //return MathOpcodeHandler("Divide", ref offset);
            offset++;
            string operate = "Divide";
            string arg1 = TermArg(ref offset);
            string arg2 = TermArg(ref offset);
            string arg3 = "";
            string arg4 = "";
            if (IsNullName(offset))
            {
                offset++;
                arg3 = "";
                //return string.Format("{0}({1}, {2})", operate, arg1, arg2);
            }
            else
            {
                arg3 = SuperName(ref offset);
            }
            if (IsNullName(offset))
            {
                offset++;
                arg4 = "";
                //return string.Format("{0}({1}, {2})", operate, arg1, arg2);
            }
            else
            {
                arg4 = SuperName(ref offset);
            }
            if (arg3.Length == 0 || arg4.Length == 0)
            {
                if (arg4.Length == 0 && arg3.Length != 0)
                {
                    return string.Format("{0}({1}, {2}, {3})", operate, arg1, arg2, arg3);
                }
                else
                {
                    return string.Format("{0}({1}, {2})", operate, arg1, arg2);
                }
            }
            else
            {
                return string.Format("{0}({1}, {2}, {3}, {4})", operate, arg1, arg2, arg3, arg4);
            }
        }
        private string FindSetLeftBitOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("FindSetLeftBit", ref offset);
        }
        private string FindSetRightBitOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("FindSetRightBit", ref offset);
        }
        private string FromBCDOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("FromBCD", ref offset);
        }
        private string IncrementOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Increment({0})", SuperName(ref offset));
        }
        private string LCompareOpcodeHandler(string operate, ref int offset)
        {
            return string.Format("{0}({1}, {2})", operate, AmlOpcodeHandler(ref offset), AmlOpcodeHandler(ref offset));
        }
        private string LLOpcodeHandler(ref int offset)
        {
            byte opcode = _AmlBinary[offset];
            offset++;
            if (opcode == (byte)AmlOpCode.LandOp)
            {
                return LCompareOpcodeHandler("LAnd", ref offset);
            }
            else if (opcode == (byte)AmlOpCode.LorOp)
            {
                return LCompareOpcodeHandler("LOr", ref offset);
            }
            else if (opcode == (byte)AmlOpCode.LEqualOp)
            {
                return LCompareOpcodeHandler("LEqual", ref offset);
            }
            else if (opcode == (byte)AmlOpCode.LGreaterOp)
            {
                return LCompareOpcodeHandler("LGreater", ref offset);
            }
            else if (opcode == (byte)AmlOpCode.LLessOp)
            {
                return LCompareOpcodeHandler("LLess", ref offset);
            }
            else
            {
                throw new InvalidOperationException("Invliad Logical Opcode");
            }
        }
        private string LNotOpcodeHandler(ref int offset)
        {
            string strOp = "";
            offset++;
            if (_AmlBinary[offset] == (byte)AmlOpCode.LEqualOp)
            {
                offset++;
                strOp = LCompareOpcodeHandler("LNotEqual", ref offset);//string.Format("LNotEqual({0})", AmlOpcodeHandler(ref offset));
            }
            else if (_AmlBinary[offset] == (byte)AmlOpCode.LGreaterOp)
            {
                offset++;
                strOp = strOp = LCompareOpcodeHandler("LLessEqual", ref offset); //string.Format("LLessEqual({0})", AmlOpcodeHandler(ref offset));
            }
            else if (_AmlBinary[offset] == (byte)AmlOpCode.LLessOp)
            {
                offset++;
                strOp = strOp = strOp = LCompareOpcodeHandler("LGreaterEqual", ref offset); //string.Format("LGreaterEqual({0})", AmlOpcodeHandler(ref offset));
            }
            else
            {
                strOp = string.Format("LNot({0})", AmlOpcodeHandler(ref offset));
            }
            return strOp;
        }
        private string LoadTableOpcodeHandler(ref int offset)
        {
            offset++;
            string termarg1 = TermArg(ref offset);
            string termarg2 = TermArg(ref offset);
            string termarg3 = TermArg(ref offset);
            string termarg4 = TermArg(ref offset);
            string termarg5 = TermArg(ref offset);
            string termarg6 = TermArg(ref offset);
            return string.Format("LoadTable({0}, {1}，{2}，{3}，{4}，{5})", termarg1, termarg2, termarg3, termarg4, termarg5, termarg6);
        }
        private string MatchOpcodeHandler(ref int offset)
        {
            offset++;
            string termarg1 = TermArg(ref offset);
            byte bytedata1 = _AmlBinary[offset];
            offset++;
            string termarg2 = TermArg(ref offset);
            byte bytedata2 = _AmlBinary[offset];
            offset++;
            string termarg3 = TermArg(ref offset);
            string termarg4 = TermArg(ref offset);
            return string.Format("Match({0}, {1}, {2}, {3}, {4}, {5})", termarg1, bytedata1, termarg2, bytedata2, termarg3, termarg4);
        }
        private string MidOpcodeHandler(ref int offset)
        {
            offset++;
            string termarg1 = TermArg(ref offset);
            string termarg2 = TermArg(ref offset);
            string termarg3 = TermArg(ref offset);
            if (IsNullName(offset))
            {
                return string.Format("Mid({0}, {1}, {2})", termarg1, termarg2, termarg3);
            }
            if (IsTarget(offset))
            {
                return string.Format("Mid({0}, {1}, {2}, {3})", termarg1, termarg2, termarg3, SuperName(ref offset));
            }
            return string.Format("Mid({0}, {1}, {2}, {3})", termarg1, termarg2, termarg3, TermArg(ref offset));
        }
        private string ModOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Mod", ref offset);
        }
        private string MultiplyOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Multiply", ref offset);
        }
        private string NandOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("NAnd", ref offset);
        }
        private string NorOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("NOr", ref offset);
        }
        private string NotOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("Not", ref offset);
        }
        private string ObjectTypeOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("ObjectTypeOp({0})", SuperName(ref offset));
        }
        private string OrOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Or", ref offset);
        }
        private string PackageOpcodeHandler(ref int offset)
        {
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            // size of package length            
            byte Length = _AmlBinary[offset];
            string strTermArg = string.Format("Package(0x{0:X2}){1}\n", Length,"{");
            offset++;
            if (MethodCollector == null)
            {
                // handle something here
                //strTermArg += ScopeTermArgListLoop(ref offset, PkgEnd);
                //strTermArg += GetSpace() + "}";
                int LocalOffset = offset;
                string strTermArgList = "";
                if (offset < PkgEnd)
                {
                    byte[] tempdata = new byte[PkgEnd - offset];
                    tempdata = _AmlBinary.Skip(offset).Take(tempdata.Length).ToArray();
                    _ScopeLevel++;
                    while (offset < PkgEnd)
                    {
                        strTermArgList += GetSpace();
                        strTermArgList += TermArgList(ref offset);
                        if (offset != PkgEnd)
                        {
                            strTermArgList += ",\n";
                        } else
                        {
                            strTermArgList += "\n";
                        }
                        
                    }
                    _ScopeLevel--;
                }
                strTermArg += strTermArgList;
                strTermArg += GetSpace() + "}";
            }
            offset = PkgEnd;
            return strTermArg;
        }
        private string VarPackageOpcodeHandler(ref int offset)
        {
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            // size of package length   
            string strTermArg = string.Format("Package(0x{0:X2}){1}\n", TermArg(ref offset), " {");
            offset++;
            if (MethodCollector == null)
            {
                // handle something here
                //strTermArg += ScopeTermArgListLoop(ref offset, PkgEnd);
                //strTermArg += GetSpace() + "}";
                int LocalOffset = offset;
                string strTermArgList = "";
                if (offset < PkgEnd)
                {
                    byte[] tempdata = new byte[PkgEnd - offset];
                    tempdata = _AmlBinary.Skip(offset).Take(tempdata.Length).ToArray();
                    _ScopeLevel++;
                    while (offset < PkgEnd)
                    {
                        strTermArgList += GetSpace();
                        strTermArgList += TermArgList(ref offset);
                        if (offset != PkgEnd)
                        {
                            strTermArgList += ",\n";
                        }
                        else
                        {
                            strTermArgList += "\n";
                        }

                    }
                    _ScopeLevel--;
                }
                strTermArg += strTermArgList;
                strTermArg += GetSpace() + "}";
            }
            offset = PkgEnd;
            return strTermArg;
        }
        private string DefaultPkgHandler(ref int offset)
        {
            string scope = "";
            offset++;
            int PkgEnd = offset;
            int PkgLength = GetPackageLength(ref offset);
            PkgEnd += PkgLength;
            offset = PkgEnd;
            return string.Format("Package({0})", PkgLength);
        }
        private string ShiftLeftOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("ShiftLeft", ref offset);
        }
        private string ShiftRightOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("ShiftRight", ref offset);
        }
        private string SizeOfOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Sizeof({0})", SuperName(ref offset));
        }
        private string StoreOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("Store", ref offset);
        }
        private string SubtractOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Subtract", ref offset);
        }
        private string TimerOpodeHandler(ref int offset)
        {
            offset++;
            return "Timer()";
        }
        private string ToBCDOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("ToBCD", ref offset);
        }
        private string ToBufferOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("ToBuffer", ref offset);
        }       
        private string ToDecimalStringOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("ToDecimalString", ref offset);
        }
        private string ToHexStringOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("ToHexString", ref offset);
        }
        private string ToIntegerOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("ToInteger", ref offset);
        }
        private string ToStringOpcodeHandler(ref int offset)
        {
            return SimpleDataOpcodeHandler("ToString", ref offset);
        }
        private string WaitOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Wait({0},{1})", SuperName(ref offset), TermArg(ref offset));
        }
        private string XorOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Xor", ref offset);
        }
        /// <summary>
        /// Is Type 6 opcode - Type 6 opcode return Reference value and can be 
        /// used in an expression that evaluates to a constant 
        /// </summary>
        /// <param name="offset">offset of amlcode</param>
        /// <returns></returns>
        private bool IsType6Opcode(int offset)
        {
            // IndexSymbolicTerm
            // User Term Obj
            {
                byte[] Opcode = new byte[] {
                    (byte)AmlOpCode.RefOfOp,
                    (byte)AmlOpCode.DerefOfOp,
                    (byte)AmlOpCode.IndexOp
                    //(byte)AmlOpCode.BufferOp,
                    //(byte)AmlOpCode.PackageOp,
                    //(byte)AmlOpCode.VarPackageOp
                };
                return Opcode.Contains(_AmlBinary[offset]);
            }
        }
        private string DerefOfOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("DerefOf({0})", TermArg(ref offset));
        }
        private string RefOfOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("RefOf({0})", SuperName(ref offset));
            //return SimpleDataOpcodeHandler("RefOf", ref offset);
        }
        private string IndexOpcodeHandler(ref int offset)
        {
            return MathOpcodeHandler("Index", ref offset);
        }
        #endregion

        #region Miscellaneous Objects Encoding
        /// <summary>
        /// is a LocalArgData Objects
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
        /// <returns></returns>
        private bool IsLocalArgData(int offset)
        {
            //string strValidCode = "0x60 0x61 0x62 0x63 0x64 0x65 0x66 0x67 0x68 0x69 0x6A 0x6B 0x6C 0x6D 0x6E";
            //return strValidCode.Contains(
            //    string.Format("0x{0:X}", _AmlBinary[offset]));
            if (_AmlBinary[offset] >= 0x60 && _AmlBinary[offset] <= 0x6E)
            {
                return true;
            }
            return false;
        }
        private string LocalArgOpcodeHandler(ref int offset)
        {
            if (!IsLocalArgData(offset))
            {
                throw new InvalidOperationException("Not a local or arg opcode");
            }
            offset++;
            if (_AmlBinary[offset - 1] >= 0x68)
            {
                //offset++;
                return "Arg" + (_AmlBinary[offset - 1] - 0x68).ToString();
            }
            else
            {
                //offset++;
                return "Local" + (_AmlBinary[offset - 1] - 0x60).ToString();
            }
        }
        private bool IsDataObjects(int offset)
        {
            if (_AmlBinary[offset] == 0x5B)
            {
                // aml Revision
                return _AmlBinary[offset + 1] == 0x30;
            }
            string strValidCode = "0xa 0xb 0xc 0xe 0xd 0x0 0x1 0xff";
            return strValidCode.Contains(
                string.Format("0x{0:x}", _AmlBinary[offset]));
        }
        private string DebugOpcodeHandler(ref int offset)
        {
            if (!IsDataObjects(offset))
            {
                throw new InvalidOperationException("Not a debug opcode");
            }
            offset++;
            return "Debug()";
        }
        #endregion
                       
        #region Name Objects Encoding
        private bool IsDebugObject(int offset)
        {
            if (_AmlBinary[offset] == (byte)AmlOpCode.ExtOpPrefix
                && _AmlBinary[offset + 1] == (byte)AmlExtOpCode.DebugOp)
            {
                return true;
            }
            return false;
        }
        private bool IsLeadChar(int offset)
        {
            char ch = (char)_AmlBinary[offset];
            if ((ch >= 'A' && ch <= 'Z') || ch == '_')
            {
                return true;
            }
            return false;
        }
        private bool IsNameString(int offset)
        {
            if (_AmlBinary[offset] == (byte)AmlOpCode.RootChar || _AmlBinary[offset] == (byte)AmlOpCode.ParentPrefixChar)
            {
                return true;
            }
            if (_AmlBinary[offset] == (byte)AmlOpCode.DualNamePrefix || _AmlBinary[offset] == (byte)AmlOpCode.MultiNamePrefix)
            {
                return true;
            }
            if (IsLeadChar(offset))
            {
                return true;
            }
            return false;
        }
        private bool IsSimpleName(int offset)
        {
            if (IsLocalArgData(offset))
            {
                return true;
            }
            if (IsNameString(offset))
            {
                return true;
            }
            return false;
        }
        private bool IsTarget(int offset)
        {
            if (IsNullName(offset))
            {
                return true;
            }
            if (IsType6Opcode(offset))
            {
                return true;
            }
            if (IsDebugObject(offset))
            {
                return true;
            }
            if (IsSimpleName(offset))
            {
                return true;
            }
            return false;
        }
        private bool IsNameObject(int offset)
        {
            char val = (char)_AmlBinary[offset];
            string strValidCode = @"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_\^./";
            return strValidCode.IndexOf(val) >= 0;
        }
        private bool IsNullName(int offset)
        {
            return _AmlBinary[offset] == 0;
        }
        private string NameSeg(ref int offset)
        {
            byte[] NameSeg = new byte[] { _AmlBinary[offset],
                _AmlBinary[offset+1],_AmlBinary[offset+2],_AmlBinary[offset+3] };
            offset += 4;
            return Encoding.ASCII.GetString(NameSeg);
        }
        private string DualNamePath(ref int offset)
        {
            offset++;
            // point to first name seg
            //string nameSeg = NameSeg(ref offset);
            //nameSeg += "." + NameSeg(ref offset);
            //return nameSeg;
            return NameSeg(ref offset) + "." + NameSeg(ref offset);
        }
        private string MultiNamePath(ref int offset)
        {
            offset++;
            byte count = _AmlBinary[offset];
            offset++;
            string nameSeg = "";
            for (byte idx = 0; idx < count; idx ++)
            {
                if (idx != 0)
                {
                    nameSeg += ".";
                }
                nameSeg += NameSeg(ref offset);
            }
            return nameSeg;
        }
        private string NameString(ref int offset)
        {
            //return AmlOpcodeHandler(ref offset);
            if (_AmlBinary[offset] == (byte)AmlOpCode.RootChar)
            {
                offset++;
                if (IsNullName(offset))
                {
                    offset++;
                    return @"\";
                }
                return @"\" + NameString(ref offset);
            }
            else if (_AmlBinary[offset] == (byte)AmlOpCode.ParentPrefixChar)
            {
                offset++;
                return "^" + NameString(ref offset);
            }
            else if (_AmlBinary[offset] == (byte)AmlOpCode.DualNamePrefix)
            {
                return DualNamePath(ref offset);
            }
            else if (_AmlBinary[offset] == (byte)AmlOpCode.MultiNamePrefix)
            {
                return MultiNamePath(ref offset);
            } else if (_AmlBinary[offset] == 0)
            {
                // NullName or Nothing
                return "";
            }
            else
            {
                // Name Path, 4 CHAR
                return NameSeg(ref offset);
            }
        }
        private string SimpleName(ref int offset)
        {
            if (IsLocalArgData(offset))
            {
                return LocalArgOpcodeHandler(ref offset);
            }
            return NameString(ref offset);
        }
        private string SuperName(ref int offset)
        {
            if (_AmlBinary[offset] == (byte)AmlOpCode.ExtOpPrefix
                && _AmlBinary[offset + 1] == (byte)AmlExtOpCode.DebugOp)
            {
                offset += 2;
                return "Debug";
            }
            if (IsType6Opcode(offset))
            {
                // Do a Type6 Code decoding
                return AmlOpcodeHandler(ref offset);
            }
            return SimpleName(ref offset);
        }
        private string NullName(ref int offset)
        {
            if (_AmlBinary[offset] == 0)
            {
                offset++;
                return "";
            }
            return "";
        }
        private string Target(ref int offset)
        {
            if (_AmlBinary[offset] == 0)
            {
                // a NullName
                offset++;
                return "";
            }
            return SuperName(ref offset);
        }
        #endregion

        #region Data Objects Encoding
        private bool IsComputationalData(int offset)
        {
            if (_AmlBinary[offset] == (byte)AmlOpCode.ExternalOp
                && _AmlBinary[offset + 1] == (byte)AmlExtOpCode.RevisionOp)
            {
                return true;
            }
            byte[] Opcode = new byte[] {
                    (byte)AmlOpCode.BytePrefix,
                    (byte)AmlOpCode.WordPrefix,
                    (byte)AmlOpCode.DWordPrefix,
                    (byte)AmlOpCode.QWordPrefix,
                    (byte)AmlOpCode.StringPrefix,
                    (byte)AmlOpCode.ZeroOp,
                    (byte)AmlOpCode.OneOp,
                    (byte)AmlOpCode.OnesOp,
                    (byte)AmlOpCode.BufferOp
                };
            return Opcode.Contains(_AmlBinary[offset]);
        }
        private bool IsDataObject(int offset)
        {
            if (_AmlBinary[offset] == (byte)AmlOpCode.PackageOp ||
                _AmlBinary[offset] == (byte)AmlOpCode.VarPackageOp)
            {
                return true;
            }
            return IsComputationalData(offset);
        }
        private string DataRefObject(ref int offset)
        {
            // DDB Handle is from LoadTable or LoadOp, must be a Name
            if (IsDataObject(offset))
            {
                // TODO Handle Data Obj..
                return AmlOpcodeHandler(ref offset);
            }            
            if (IsNameObject(offset))
            {
                // DDB Handler or Reference object
                return NameString(ref offset);
            }
            return AmlOpcodeHandler(ref offset);
        }
        /// <summary>
        /// Disassemble a byte opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string ByteOpcodeHandler(ref int offset)
        {
            offset += 2;
            return string.Format("0x{0:X2}", _AmlBinary[offset - 1]);
        }

        /// <summary>
        /// Disassemble a word opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string WordOpcodeHandler(ref int offset)
        {
            offset += 3;
            UInt16 word = (UInt16)BitConverter.ToInt16(_AmlBinary, offset - 2);
            return string.Format("0x{0:X4}", word);
        }

        /// <summary>
        /// Disassemble a dword opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string DWordOpcodeHandler(ref int offset)
        {
            offset += 5;
            UInt32 dword = (UInt32)BitConverter.ToInt32(_AmlBinary, offset - 4);
            return string.Format("0x{0:X8}", dword);
        }

        /// <summary>
        /// Disassemble a qword opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string QWordOpcodeHandler(ref int offset)
        {
            offset += 9;
            UInt64 qword = (UInt64)BitConverter.ToInt32(_AmlBinary, offset - 8);
            return string.Format("0x{0:X16}", qword);
        }
        /// <summary>
        /// Disassemble a string opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string StringOpcodeHandler(ref int offset)
        {
            offset++;
            int Length = 0;
            while (_AmlBinary[Length + offset] != 0)
            {
                Length++;
            }
            string value = Encoding.ASCII.GetString(_AmlBinary, offset, Length);
            //string value = BitConverter.ToString(_AmlBinary, offset, Length);
            offset += Length + 1;
            return "\"" + value + "\"";
        }
        private string RevisionOpcodeHandler(ref int offset)
        {
            offset++;
            return "_REV";  // Optional: Change to Revision()
        }
        /// <summary>
        /// Disassemble a zero opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string ZeroOpcodeHandler(ref int offset)
        {
            offset++;
            return "Zero";
        }
        /// <summary>
        /// Disassemble a one opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string OneOpcodeHandler(ref int offset)
        {
            offset++;
            return "One";
        }
        /// <summary>
        /// Disassemble a ones opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string OnesOpcodeHandler(ref int offset)
        {
            offset++;
            return "Ones";
        }
        #endregion

        #region Term Objects Encoding
        private string Type1Opcode(ref int offset)
        {
            return AmlOpcodeHandler(ref offset);
        }
        private string Type2Opcode(ref int offset)
        {
            return AmlOpcodeHandler(ref offset);
        }
        private bool IsObject(int offset)
        {
            return IsNamespaceModiferObject(offset) || IsNamedObjects (offset);
        }
        private bool IsTermObj (int offset)
        {
            return IsObject(offset) || IsType1Opcode(offset) || IsType2Opcode(offset);
        }
        private bool IsTermArg(int offset)
        {
            return IsType2Opcode(offset) || IsLocalArgData(offset) || IsDataObject(offset);
        }
        private string TermList(ref int offset, ref int length)
        {
            string strTermList = "";
            int end = length + offset;
            // if length == 0
            if (length == 0)
            {
                return "";// nothing;
            }
            else
            {
                // term list or term obj
                while (offset < end)
                {
                    if (IsTermObj(offset))
                    {
                        strTermList += GetSpace() + TermObj(ref offset);
                    }
                    else
                    {
                        strTermList += TermList(ref offset, ref length);
                    }
                    strTermList += "\n";
                }
            }
            return strTermList;
        }
        private string Object(ref int offset)
        {
            if (IsNamespaceModiferObject(offset))
            {
                return AmlOpcodeHandler(ref offset);
                //return NameSpaceModifierObj(ref offset);
            }
            else if (IsNamedObjects(offset))
            {
                return AmlOpcodeHandler(ref offset);
                //return NamedObj(ref offset);
            }
            throw new InvalidOperationException("Objected Type Required");
        }
        private string TermObj(ref int offset)
        {
            if (IsType1Opcode(offset))
            {
                return (Type1Opcode(ref offset));
            }
            else if (IsType2Opcode(offset))
            {
                return (Type2Opcode(ref offset));
            }
            return Object(ref offset);
        }
        private string TermArgList(ref int offset)
        {
            //int LocalOffset = offset;
            //if (IsNameObject(offset))
            //{
            //    return SuperName(ref offset);
            //}
            //return AmlOpcodeHandler(ref offset);
            return TermArg(ref offset);
        }
        /// <summary>
        /// parser termarg
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string TermArg(ref int offset)
        {
            if (IsNameString(offset))
            {
                if (_amlMethodBuilder != null)
                {
                    int localOffset = offset;
                    string Name = NameString(ref offset);                    
                    int argcount = _amlMethodBuilder.GetArgCount(GetAcpiPath(), Name);
                    if (argcount == -1)
                    {
                        offset = localOffset;
                    } else
                    {
                        //System.Diagnostics.Debug.WriteLine(Name + " " + argcount.ToString());
                        string strTermArg = Name + "(";
                        for (int idx = 0; idx < argcount; idx ++)
                        {
                            if (idx != 0)
                            {
                                strTermArg += ",";
                            }
                            strTermArg += TermArg(ref offset);
                        }
                        strTermArg += ")";
                        return strTermArg;
                    }
                }
            }
            if (IsNameObject (offset))
            {
                return SuperName(ref offset);
            }
            return AmlOpcodeHandler(ref offset);
        }
        private string MethodInvocation(ref int offset, ref int length)
        {
            //if (NameStringObj())
            return "MethodInvocation";
        }
        #endregion

        #region Byte Code Register and lookup function
        /// <summary>
        /// Initialize the aml handler map
        /// </summary>
        private void AmlByteCodeHandlerInitialize()
        {
            // Opcode Handler
            AmlByteCodeHandler = new Dictionary<byte, AmlParser>();
            AmlByteCodeHandler[(byte)AmlOpCode.ZeroOp] = ZeroOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.OneOp] = OneOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.AliasOp] = AliasOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.NameOp] = NameOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.BytePrefix] = ByteOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.WordPrefix] = WordOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.DWordPrefix] = DWordOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.StringPrefix] = StringOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.QWordPrefix] = QWordOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ScopeOp] = ScopeOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.BufferOp] = BufferOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.PackageOp] = PackageOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.VarPackageOp] = VarPackageOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.MethodOp] = MethodOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ExternalOp] = ExternalOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ExtOpPrefix] = AmlExtHandler;
            for (byte index = 0x60; index <= 0x6E; index++)
            {
                AmlByteCodeHandler[index] = LocalArgOpcodeHandler;
            }
            AmlByteCodeHandler[(byte)AmlOpCode.StoreOp] = StoreOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.RefOfOp] = RefOfOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.AddOp] = AddOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ConcatOp] = ConcatOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.SubtractOp] = SubtractOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.IncrementOp] = IncrementOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.DecrementOp] = DecrementOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.MultiplyOp] = MultiplyOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.DivideOp] = DivideOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ShiftLeftOp] = ShiftLeftOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ShiftRightOp] = ShiftRightOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.AndOp] = AndOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.NandOp] = NandOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.OrOp] = OrOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.NorOp] = NorOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.XorOp] = XorOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.NotOp] = NotOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.FindSetLeftBitOp] = FindSetLeftBitOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.FindSetRightBitOp] = FindSetRightBitOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.DerefOfOp] = DerefOfOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ConcatResOp] = ConcatResOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ModOp] = ModOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.NotifyOp] = NotifyOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.SizeOfOp] = SizeOfOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.IndexOp] = IndexOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.MatchOp] = MatchOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.CreateDWordFieldOp] = CreateDWordOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.CreateWordFieldOp] = CreateWordOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.CreateByteFieldOp] = CreateByteOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.CreateBitFieldOp] = CreateBitOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ObjectTypeOp] = ObjectTypeOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.CreateQWordFieldOp] = CreateQWordOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.LandOp] = LLOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.LorOp] = LLOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.LnotOp] = LNotOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.LEqualOp] = LLOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.LGreaterOp] = LLOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.LLessOp] = LLOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ToBufferOp] = ToBufferOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ToDecimalStringOp] = ToDecimalStringOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ToHexStringOp] = ToHexStringOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ToIntegerOp] = ToIntegerOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ToStringOp] = ToStringOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.CopyObjectOp] = CopyObjectOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.MidOp] = MidOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ContinueOp] = ContinueOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.IfOp] = IfOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ElseOp] = ElseOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.WhileOp] = WhileOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.NoopOp] = NoopOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.ReturnOp] = ReturnOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.BreakOp] = BreakOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.BreakPointOp] = BreakPointOpcodeHandler;
            AmlByteCodeHandler[(byte)AmlOpCode.OnesOp] = OnesOpcodeHandler;

            // Ext Opcode Handler
            AmlByteExtCodeHandler = new Dictionary<byte, AmlParser>();
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.MutexOp] = MutexfOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.EventOp] = EventOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.CondRefOfOp] = CondRefOfOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.CreateFieldOp] = CreateFieldOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.LoadTableOp] = LoadTableOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.LoadOp] = LoadOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.StallOp] = StallOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.SleepOp] = SleepOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.AcquireOp] = AcquireOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.SignalOp] = SignalOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.WaitOp] = WaitOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.ResetOp] = ResetOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.ReleaseOp] = ReleaseOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.FromBCDOp] = FromBCDOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.ToBCDOp] = ToBCDOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.UnloadOp] = UnloadOpcodeHandler; 
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.RevisionOp] = RevisionOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.DebugOp] = DebugOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.FatalOp] = FatalOpodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.TimerOp] = TimerOpodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.OpRegionOp] = OpRegionOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.FieldOp] = FieldOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.DeviceOp] = DeviceOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.ProcessorOp] = ProcessorOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.PowerResOp] = PowerResOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.ThermalZoneOp] = ThermalZoneOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.IndexFieldOp] = IndexFieldOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.BankFieldOp] = BankFieldOpcodeHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.DataRegionOp] = DataRegionOpcodeHandler;  
        }
        /// <summary>
        /// Aml Opcode lookup table
        /// </summary>
        /// <param name="offset">offset of the aml binary</param>
        /// <returns></returns>
        private string AmlOpcodeHandler(ref int offset)
        {
            // handle a single operation
            int LocalOffset = offset;
            AmlParser AmlParser;
            if (AmlByteCodeHandler.TryGetValue(_AmlBinary[offset], out AmlParser))
            {
                return AmlParser(ref offset);
            }
            else if (IsNameObject(offset))
            {
                return SuperName(ref offset);
            }
            return "Unsupported Aml Opcode found";
        }
        /// <summary>
        /// Aml Extention Code Handler
        /// </summary>
        /// <param name="offset">offset of data</param>
        /// <returns>asl code from aml or an InvalidOperationException indicate wrong aml code</returns>
        private string AmlExtHandler(ref int offset)
        {
            AmlParser amlParser;
            if (AmlByteExtCodeHandler.TryGetValue(_AmlBinary[offset + 1], out amlParser))
            {
                offset+=1;
                return amlParser(ref offset);
            }
            throw new InvalidOperationException();
        }
        #endregion
    }
}
