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
    class AmlDisassemble
    {
        delegate string AmlParser(ref int offset);
        /// <summary>
        /// Aml Byte Code Handler method
        /// </summary>
        private Dictionary<byte, AmlParser> AmlByteCodeHandler;
        private Dictionary<byte, AmlParser> AmlByteExtCodeHandler;
        private byte[] _AmlBinary;
        private int _ScopeLevel = 0;
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

        private string GetSpace ()
        {
            if (_ScopeLevel == 0)
            {
                return "";
            }
            string temp = "  ";

            for (int idx = 0; idx < _ScopeLevel; idx ++)
            {
                temp += temp;
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

        #region Named Objects Encoding
        /// <summary>
        /// Handle operation region term
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string OpRegionHandler(ref int offset)
        {
            // 4 data, A Name or a OpCode, Method returned data
            offset ++;
            string opregion = string.Format("OperationRegion({0},{1},{2},{3})", NameString(ref offset),
                RegionSpace(ref offset), AmlOpcodeHandler(ref offset), AmlOpcodeHandler(ref offset));
            // now point to Field Handler if had
            // if (_AmlBinary[offset] == (byte)AmlOpCode.ExtOpPrefix && _AmlBinary[offset] == (byte)AmlExtOpCode.FieldOp)
            // {
            //    // it's field then parse the field lists
            //    opregion += FieldHandler(ref offset);
            // }
            return GetSpace() + opregion;
        }
        private string FieldAccessType (byte accessType)
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
            } else if (UpdateRule == 1)
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
        private string BankFieldHandler(ref int offset)
        {
            offset++;
            int Length = GetPackageLength(ref offset);
            int end = offset + Length - 1;

            //string FeildName = NameString(ref offset);
            string strFields = string.Format("{0}BankField({1},{2},{3},{4})", GetSpace(), NameString(ref offset), NameString(ref offset),
                AmlOpcodeHandler(ref offset),FieldFlags(_AmlBinary[offset]));
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
        private string FieldHandler(ref int offset)
        {
            //string strFields = "{\n";
            offset++;
            int Length = GetPackageLength(ref offset);
            int end = offset + Length - 1;
           
            //string FeildName = NameString(ref offset);
            string strFields = string.Format("{0}Field({1},{2})", GetSpace(), NameString(ref offset), FieldFlags(_AmlBinary[offset]));
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
                strFields += string.Format("\n{0}",GetSpace()) + "}";
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

        #endregion
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
        private string NameSpaceModifierObj(ref int offset)
        {
            return AmlOpcodeHandler(ref offset);
        }
        #endregion

        /// <summary>
        /// is a type 1 opcode
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
        /// <returns></returns>
        private bool IsType1Opcode(int offset)
        {
            if (_AmlBinary[offset] == 0x5B)
            {
                string strValidCode = "0x32 0x20 0x27 0x26 0x24 0x22 0x21 ";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset + 1]));
            } else
            {
                string strValidCode = "0xa2 0xa4 0xa5 0xcc 0x9f 0xa1 0xa0 0xa3 0x86";
                return strValidCode.Contains(
                    string.Format("0x{0:x}", _AmlBinary[offset]));
            }
        }
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
        /// is a LocalArgData Objects
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
        /// <returns></returns>
        private bool IsLocalArgData(int offset)
        {
            string strValidCode = "0x60 0x61 0x62 0x63 0x64 0x65 0x66 0x67 0x68 0x69 0x6A 0x6B 0x6C 0x6D 0x6E";
            return strValidCode.Contains(
                string.Format("0x{0:x}", _AmlBinary[offset]));
        }
        /// <summary>
        /// is a named Objects
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
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
        /// is a Objects or opcode
        /// </summary>
        /// <param name="offset">aml byte code offsest</param>
        /// <returns></returns>
        private bool OpcodeOrObjects(int offset)
        {
            bool bOpcodeOrObjects = IsNamespaceModiferObject(offset);
            if (bOpcodeOrObjects)
            {
                return bOpcodeOrObjects;
            }
            bOpcodeOrObjects = IsType1Opcode(offset);
            if (bOpcodeOrObjects)
            {
                return bOpcodeOrObjects;
            }
            bOpcodeOrObjects = IsType2Opcode(offset);
            if (bOpcodeOrObjects)
            {
                return bOpcodeOrObjects;
            }
            bOpcodeOrObjects = IsLocalArgData(offset);
            if (bOpcodeOrObjects)
            {
                return bOpcodeOrObjects;
            }
            bOpcodeOrObjects = IsNamedObjects(offset);
            if (bOpcodeOrObjects)
            {
                return bOpcodeOrObjects;
            }
            if (_AmlBinary[offset] == 0x5B)
            {
                return _AmlBinary[offset + 1] == 0x31;
            }
            return false;
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
        private int GetPackageLength(ref int offset)
        {
            int PkgByte = 1;
            int length = 0;
            if ((_AmlBinary[offset] & 0xC0) == 0)
            {
                // only one byte
                length = (int)(_AmlBinary[offset] & 0x3F);
            } else
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
        
        
        private string NamedObj(ref int offset)
        {
            // handle the named obj
            return AmlOpcodeHandler(ref offset);
        }
        private string Object(ref int offset)
        {
            if (IsNamespaceModiferObject(offset))
            {
                return AmlOpcodeHandler(ref offset);
                //return NameSpaceModifierObj(ref offset);
            } else if (IsNamedObjects(offset))
            {
                return AmlOpcodeHandler(ref offset);
                //return NamedObj(ref offset);
            }
            throw new InvalidOperationException("Objected Type Required");
        }
        private string Type1Opcode(ref int offset)
        {
            return AmlOpcodeHandler(ref offset);
        }
        private string Type2Opcode(ref int offset)
        {
            return AmlOpcodeHandler(ref offset);
        }
        private string DebugOpcodeHandler(ref int offset)
        {
            if (!IsDataObjects(offset))
            {
                throw new InvalidOperationException("Not a debug opcode");
            }
            offset += 2;
            return "Debug()";
        }
        private string LocalArgOpcodeHandler(ref int offset)
        {
            if (!IsLocalArgData(offset))
            {
                throw new InvalidOperationException("Not a local or arg opcode");
            }
            offset++;
            if (_AmlBinary[offset] >= 0x68)
            {
                return "Arg" + (_AmlBinary[offset] - 0x68).ToString();
            }
            else
            {
                return "Local" + (_AmlBinary[offset] - 0x60).ToString();
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
        /// Disassemble a zero opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string ZeroOpcodeHandler(ref int offset)
        {
            offset++;
            return "0";
        }
        /// <summary>
        /// Disassemble a one opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string OneOpcodeHandler(ref int offset)
        {
            offset++;
            return "1";
        }
        /// <summary>
        /// Disassemble a ones opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string OnesOpcodeHandler(ref int offset)
        {
            offset++;
            return "0xFFFFFFFFFFFFFFFF";
        }
        private string DataRefObj(ref int offset)
        {
            // TODO: Nickels
            return "DataRefObj";
        }
        /// <summary>
        /// Disassemble a name opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string NameOpcodeHandler(ref int offset)
        {
            offset++;
            return string.Format("Name({0}, {1})", NameString(ref offset),
               DataRefObj(ref offset));
        }

        /// <summary>
        /// Disassemble a byte opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string ByteOpcodeHandler(ref int offset)
        {
            offset += 2;
            return string.Format("0x{0:X}", _AmlBinary[offset - 1]);
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
            return string.Format("0x{0:X}", word);
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
            return string.Format("0x{0:X}", dword);
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
            return string.Format("0x{0:X}", qword);
        }

        /// <summary>
        /// Disassemble a string opcode
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private string StringOpcodeHandler(ref int offset)
        {
            offset++;
            string value = BitConverter.ToString(_AmlBinary, offset);
            offset += value.Length + 1;
            return value;
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
            int PkgLength = GetPackageLength(ref offset);
            int PkgEnd = offset + PkgLength;
            scope = string.Format("Scope({0}) {\n", NameString(ref offset));
            while (offset < PkgEnd)
            {
                scope += TermArgList(ref offset);
            }
            scope += "\n}";
            return scope;
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
            int PkgLength = GetPackageLength(ref offset);
            int PkgEnd = offset + PkgLength;
            strAsl = string.Format("Buffer({0}) {\n", NameString(ref offset));
            while (offset < PkgEnd)
            {
                strAsl += TermArgList(ref offset);
            }
            strAsl += "\n}";
            return strAsl;
        }
        #region Name Objects Encoding
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
                return @"\" + NameString(ref offset);
            }
            else if (_AmlBinary[offset] == (byte)AmlOpCode.ParentPrefixChar)
            {
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
            if (_AmlBinary[offset] == (byte)AmlOpCode.ExternalOp
                && _AmlBinary[offset + 1] == (byte)AmlExtOpCode.DebugOp)
            {
                offset += 2;
                return "Debug()";
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
        #endregion
        #region Term Objects Encoding
        private bool IsObject(int offset)
        {
            return IsNamespaceModiferObject(offset) || IsNamedObjects (offset);
        }
        private bool IsTermObj (int offset)
        {
            return IsObject(offset) || IsType1Opcode(offset) || IsType2Opcode(offset);
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
                        strTermList += TermObj(ref offset);
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
        //private bool IsTermObj(int offset)
        //{
        //    if (IsType1Opcode(offset))
        //    {
        //        return true;
        //    }
        //    else if (IsType2Opcode(offset))
        //    {
        //        return true;
        //    }
        //    else if (IsNamespaceModiferObject(offset))
        //    {
        //        return true;
        //    }
        //    else if (IsNamedObjects(offset))
        //    {
        //        return true;
        //    }
        //    return false;
        //}
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
            return "TermArgList";
        }
        private string TermArg(ref int offset)
        {
            //Type2Opcode | DataObject | ArgObj | LocalObj
            return "TermArg";
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
            AmlByteCodeHandler[0x5b] = AmlExtHandler;
            for (byte index = 0x60; index <= 0x6E; index++)
            {
                AmlByteCodeHandler[index] = LocalArgOpcodeHandler;
            }

            // Ext Opcode Handler
            AmlByteCodeHandler[(byte)AmlOpCode.OnesOp] = OnesOpcodeHandler;
            AmlByteExtCodeHandler = new Dictionary<byte, AmlParser>();
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.OpRegionOp] = OpRegionHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.FieldOp] = FieldHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.BankFieldOp] = BankFieldHandler;
            AmlByteExtCodeHandler[(byte)AmlExtOpCode.DebugOp] = DebugOpcodeHandler;
        }
        /// <summary>
        /// Aml Opcode lookup table
        /// </summary>
        /// <param name="offset">offset of the aml binary</param>
        /// <returns></returns>
        private string AmlOpcodeHandler(ref int offset)
        {
            // handle a single operation
            AmlParser AmlParser;
            if (AmlByteCodeHandler.TryGetValue(_AmlBinary[offset], out AmlParser))
            {
                return AmlParser(ref offset);
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
