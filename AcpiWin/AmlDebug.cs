using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    class AmlDebug
    {
        //private string   
        private Stack stack = new Stack();
        private AcpiData[] Arg = new AcpiData[8];
        private AcpiData[] Local = new AcpiData[8];
        private Dictionary<string, int> OpCodeMin = new Dictionary<string, int>();
        private Dictionary<string, int> OpCodeMax = new Dictionary<string, int>();
        private Dictionary<string, int> OpCodeScope = new Dictionary<string, int>();
        private AcpiData Result;
        private List<AmlOp> amlOps = new List<AmlOp>();
        private List<AcpiData> amlLocalDataList = new List<AcpiData>();
        public string AcpiNS;   // Method string only, used for using name space address
        public AcpiLib localAcpiLib = null;
        private Dictionary<string, int> amlLocalDataMap = new Dictionary<string, int>();
        private Boolean IsIntString(string strValue)
        {
            try
            {
                if (strValue.StartsWith ("0x"))
                {
                    return true;
                }
                UInt64 bValue = UInt64.Parse(strValue, System.Globalization.NumberStyles.HexNumber);
                return true;
            }catch(Exception)
            {
                return false;
            }
        }
        private void ParseSubData(List<AcpiData> AcpiDatas, AmlOp amlOp)
        {
            if (amlOp.amlDatas.Count > 0)
            {
                foreach (AcpiData amlData in amlOp.amlDatas)
                {                    
                    try
                    {                        
                        if (amlLocalDataMap[amlData.Name] == 1)
                        {
                            continue;   // if the name is already exiting
                        }
                        
                    } catch (Exception) {
                        if (amlData.Name.Equals("Zero"))
                        {
                            continue;
                        }
                        if (amlData.Name.Equals("One"))
                        {
                            continue;
                        }
                        if (amlData.Name.Equals("Ones"))
                        {
                            continue;
                        }
                        if (IsIntString(amlData.Name))
                        {
                            continue;
                        }
                        // test is it a hex or decimal code
                        if (amlData.Type != AcpiDataType.Int)
                        {
                            amlData.QueryData(AcpiNS, amlData.Name);
                            //if (localAcpiLib != null)
                            //{
                            //    string Path = AcpiNS;
                            //    int nType = localAcpiLib.GetType(Path + amlData.Name);
                            //    while (nType == -1)
                            //    {
                            //        Path = Path.Substring(0, Path.Length - 4);
                            //        if (Path.Length < 4)
                            //        {
                            //            break;
                            //        }
                            //        nType = localAcpiLib.GetType(Path + amlData.Name);
                            //    }
                            //    if (nType != -1)
                            //    {
                            //        ushort Type = (ushort)nType;
                            //        amlData.Type = (AcpiDataType)nType;
                            //        // get the data directory
                            //        amlData.QueryData(Path, amlData.Name);
                            //        //IntPtr intPtr = localAcpiLib.GetValue(Path + amlData.Name, ref Type);
                            //        //if (intPtr != IntPtr.Zero)
                            //        //{
                            //        //    if (Type == (ushort)AcpiDataType.Int)
                            //        //    {                                        // get the data..
                            //        //        amlData.Value = ((UInt64)Marshal.ReadInt64(intPtr + 0x10));

                            //        //    }
                            //        //    else
                            //        //    {
                            //        //        // TODO ....
                            //        //        amlData.Value = ((UInt64)Marshal.ReadInt64(intPtr + 0x10));
                            //        //    }
                            //        //    localAcpiLib.FreeArg(intPtr);
                            //        //}
                            //    }
                            //}
                        }
                        AcpiDatas.Add(amlData);
                        amlLocalDataMap[amlData.Name] = 1;
                    }
                }
            }
            if (amlOp.SubOp.Count > 0)
            {
                foreach (AmlOp subOp in amlOp.SubOp)
                {
                    ParseSubData(AcpiDatas, subOp);
                }
            }
        }

        public List<AcpiData> GetLocalDataList()
        {
            return amlLocalDataList;
        }

        private void InitDebugData()
        {            
            amlLocalDataList.Clear();
            amlLocalDataMap.Clear();
            foreach (AmlOp amlOp in amlOps)
            {
                ParseSubData(amlLocalDataList, amlOp);
            }
            return;
        }
        public void InitializeData(AcpiLib acpilib)
        {
            localAcpiLib = acpilib;
            if (localAcpiLib == null)
            {
                return;
            }
            InitDebugData();
        }
        public void ResetDebug ()
        {
            // reset all data
            for (int i = 0;i < 8; i ++)
            {
                Arg[i] = null;
                Local[i] = null;
            }
            Result = null;
            amlOps.Clear();
        }

        public void AslAnalyze(string aslCode, AcpiData[] Args)
        {
            for (int i = 0; i < 8; i++)
            {
                if (aslCode.ToLower().Contains("local" + i.ToString()))
                {
                    Local[i] = new AcpiData();
                    Local[i].Name = "Local" + i.ToString();
                }
            }
            for (int i = 0; i < Args.Length; i++)
            {
                Arg[i] = Args[i];
            }
            // parse the used name space one by one now.....
        }

        const string ValidString = "_\\.0123456789abcdefghijklmnopqrstuvwxyz^";
        private Boolean ValidOpCode(string opCode)
        {
            string opCodeL = opCode.ToLower();
            foreach (char opCh in opCodeL)
            {
                if (ValidString.IndexOf(opCh) == -1)
                {
                    return false;
                }
            }
            return true;
        }
        private string[] GetWords(string Line)
        {
            string strLine = Line.Trim();
            List<string> strList = new List<string>();
            int WordStart = 0;
            int WordEnd = -1;
            int idx = 0;
            while (idx <= strLine.Length)
            {
                WordEnd = strLine.IndexOfAny (new char[] { ' ',',',')','(' }, WordStart);
                if (WordEnd == -1 && strLine.Length > 0)
                {
                    strList.Add(strLine.Trim());
                    break;
                }
                if (WordEnd > 0)
                {
                    strList.Add(strLine.Substring(0, WordEnd));
                }

                if (strLine.IndexOf(')') == WordEnd)
                {
                    strList.Add(")");
                }
                if (strLine.IndexOf('(') == WordEnd)
                {
                    strList.Add("(");
                }
                if (WordEnd + 1 >= strLine.Length)
                {
                    break;
                }
                strLine = strLine.Substring(WordEnd + 1).Trim();
            }
            strList.Add(" ");   // for parser check end
            return strList.ToArray();
        }
        private Boolean BuildArgs(ref string Line, ref AmlOp amlOp)
        {
            string strInner = Line.Trim();
            strInner = strInner.Substring(strInner.IndexOf("(") + 1).Trim();
            strInner = strInner.Substring(0,strInner.LastIndexOf(")")).Trim();
            int nSubArgs = NumOfChar(strInner, '(');
            // number of Sub Args....
            // string Subs = strInner;            
            string[] Subs = GetWords(strInner);
            AmlOp currentArg = amlOp;
            for (int idx = 0; idx < Subs.Length; idx ++)
            {
                if (Subs[idx].Equals(" "))
                {
                    break;
                }

                if (Subs[idx].Equals(")") && idx +2 == Subs.Length)
                {
                    break;
                }
                if (Subs[idx +1].Equals("("))
                {
                    // this word is a operation
                    AmlOp subOp = new AmlOp(Subs[idx]);
                    //subOp.AddSubOp.
                    currentArg.AddSubOp(subOp);
                    currentArg = subOp;
                    idx++;

                } else if (Subs[idx + 1].Equals(")"))
                {
                    // End of sub operations
                    currentArg.AddArgs(Subs[idx], Subs[idx]);
                    currentArg = currentArg.ParentOp;
                    idx++;
                }
                else
                {
                    // this word is a args
                    currentArg.AddArgs(Subs[idx],Subs[idx]);
                }
            }
            return true;
        }        
        private int NumOfChar(string str, char value)
        {
            string temp = str;
            int Num = 0;
            try
            {
                while (temp.IndexOf(value) >= 0)
                {
                    Num++;
                    temp = temp.Remove(temp.IndexOf(value));
                }
            }
            catch(Exception)
            {

            }            
            return Num;
        }

        private Boolean ValidArgString (string Args)
        {
            int open_brakcet = NumOfChar(Args, '(');
            int close_brakcet = NumOfChar(Args, ')');
            if (open_brakcet == close_brakcet)
            {
                return true;
            }
            return false;
        }

        private string GetOpCode(ref string line)
        {
            string opCode = "";
            int iStart = line.IndexOf("(");
            if (iStart == -1)
            {
                // there is no parameter for opcode, to end then
                opCode = line.Trim();
                if (!ValidOpCode(opCode))
                {
                    return null;
                }
                line = "";
                return opCode;
            }
            // has args
            int iEnd = line.LastIndexOf(")");
            if (iEnd == -1)
            {
                // no close bracket, something wrong
                return null;
            }
            if (iEnd <= iStart)
            {
                // invalid asl code line
                return null;
            }
            opCode = line.Substring(0, iStart).Trim();
            string ArgsString = line.Substring(iStart, iEnd - iStart + 1);            
            line = ArgsString;
            return opCode;
        }
        private string GetWord1(ref string line)
        {
            string word = "";
            if (line == null || line.Length <1)
            {
                return null;
            }
            if (line[0] == '(' || line[0] == ')' || line[0] == '{' || line[0] == '}' || line[0] == ',')
            {
                word = line.Substring(0, 1);
                line = line.Substring(1);
                line.Trim();
                return word;
            }
            int Start = line.IndexOf(' ');
            int Test = line.IndexOf('(');
            if (Test != -1 && Test < Start)
            {
                Start = Test;
            }
            //Test = line.IndexOf('(');
            if (Start == -1)
            {
                Start = line.IndexOf(',');
            }
            if (Start == -1)
            {
                Start = line.IndexOf(')');
            }
            if (Start == -1)
            {
                Start = line.IndexOf('{');
            }
            if (Start == -1)
            {
                Start = line.IndexOf('}');
            }
            if (Start == -1)
            {
                Start = line.IndexOf('}');
            }
            if (Start == -1)
            {
                if (line.Length > 1)
                {
                    line = line.Trim();
                    word = line;
                }
                return word;
            }
            word = line.Substring(0, Start);
            line = line.Substring(Start);
            line = line.Trim();
            return word;
        }
        
        private Boolean GetMaxMin (string Name, ref int Min, ref int Max)
        {
            try
            {
                Max = OpCodeMax[Name];
                Min = OpCodeMin[Name];
                return true;
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return false;
            }
            return false;
        }

        private int SubOp(string Line)
        {
            // get number of sub operations
            int subs = -1;
            int offset = -1;
            string subString = Line.Trim();
            try
            {
                while ((offset = subString.IndexOf("(")) >= 0)
                {
                    subs++;
                    offset++;
                    subString = subString.Substring(offset);
                }
            }
            catch (Exception)  {

            }
            return subs;
            
        }
        private Boolean GetArgs(string Word, ref string Line, ref AmlOp amlOp)
        {
            int Min, Max;
            Min = 0;
            Max = 0;
            if (!GetMaxMin(Word, ref Min, ref Max))
            {
                return false;
            }
            if (Max == -2)
            {
                // simple operation without brackets such as Noop, Debug, Break etc
                return true;
            }

            return BuildArgs(ref Line, ref amlOp);
        }
        AmlOp amlCurrent;
        public void ScanForDefinedNS (AcpiLib acpiLib, string aslCode)
        {            
            string[] Lines = aslCode.Split(new char[] { '\n' });
            
            for (int i = 4; i < Lines.Length; i ++)
            {
                // parse every line
                // Lines[i] // what is this line
                string line = Lines[i].Trim();
                string opCode = GetOpCode(ref line);
                if (opCode != null && line.Length > 0 && ValidArgString(line))
                {
                    //AmlOp amlOp = new AmlOp(opCode, i);
                    //amlCurrent = amlOp;
                    AmlOp amlCurrent = new AmlOp(opCode, i);                  
                    if (!GetArgs(opCode, ref line, ref amlCurrent))
                    {   
                        //
                        // TODO: debug assert
                    }
                    amlOps.Add(amlCurrent);
                    //amlCurrent = amlOps.Last();
                }
            }
            InitializeData(acpiLib);
            return;
        }

        private Boolean IsOpCode (string opCode)
        {
            try
            {
                if (OpCodeMax[opCode] >= 0)
                {
                    return true;
                }
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return false;
            }
            return false;
        }

        public void AddOpCode (string name, int min, int max)
        {
            OpCodeMin[name] = min;
            OpCodeMax[name] = max;
        }
        public void AddOpCode(string name)  // none para
        {
            //OpCodeMax[name] = min;
            OpCodeMax[name] = 0;
            OpCodeMin[name] = 0;
        }

        public void AddScopeOpCode(string name)  // none para
        {
            OpCodeScope[name] = 1;
        }

        public void AddOpCode(string name, int m)  // fixed para
        {
            OpCodeMax[name] = m;
            OpCodeMin[name] = m;
        }

        public AmlDebug()
        {
            AddOpCode("OSI", 1);    // OS defined method
            AddOpCode("Zero",-1);
            AddOpCode("One",-1);
            AddOpCode("Ones",-1);
            AddOpCode("Alias", 2);
            AddOpCode("Name", 2);
            AddOpCode("Scope", 1);
            AddOpCode("Method", 3);
            AddOpCode("External", 2,4);
            AddOpCode("Mutex", 2);
            AddOpCode("Event", 1);
            AddOpCode("CondRefOf", 1);
            AddOpCode("CreateField", 4);
            AddOpCode("LoadTable", 2, 3);
            AddOpCode("Load", 2);
            AddOpCode("Stall", 1);
            AddOpCode("Sleep", 1);
            AddOpCode("Acquire", 2);
            AddOpCode("Signal", 1);
            AddOpCode("Wait",2);
            AddOpCode("Reset", 1);
            AddOpCode("Release", 1);
            AddOpCode("FromBCD", 2);
            AddOpCode("ToBCD", 2);
            AddOpCode("Revision");
            AddOpCode("Debug");
            AddOpCode("Fata", 3);
            AddOpCode("Timer");
            AddOpCode("OperationRegion", 4);
            AddOpCode("Field", 4);
            AddOpCode("OperationRegion", 4);
            AddOpCode("Processor", 4);
            AddOpCode("PowerRes", 3);
            AddOpCode("ThermalZone", 1);
            AddOpCode("IndexField", 4);
            AddOpCode("BankField", 4);
            AddOpCode("DataRegion", 4);
            AddOpCode("Store", 2);
            AddOpCode("RefOf", 1);
            AddOpCode("Add", 2,3);
            AddOpCode("Concat", 2, 3);
            AddOpCode("Subtract", 2, 3);
            AddOpCode("Increment", 1);
            AddOpCode("Decrement", 1);
            AddOpCode("Multiply", 2, 3);
            AddOpCode("Divide", 2, 3);
            AddOpCode("ShiftLeft", 2, 3);
            AddOpCode("ShiftRight", 2, 3);
            AddOpCode("And", 2, 3);
            AddOpCode("Nand", 2, 3);
            AddOpCode("Or", 2, 3);
            AddOpCode("Nor", 2, 3);
            AddOpCode("Xor", 2, 3);
            AddOpCode("Not", 1);            
            AddOpCode("FindSetLeftBit", 1, 2);
            AddOpCode("FindSetRightBit", 1, 2);
            AddOpCode("DerefOf", 1);
            AddOpCode("ConcatRes", 2, 3);
            AddOpCode("Mod", 2, 3);
            AddOpCode("Notify", 2);
            AddOpCode("SizeOf", 1);
            AddOpCode("Index", 2,3);
            AddOpCode("Match", 2, 3);
            AddOpCode("CreateDWordField", 3);
            AddOpCode("CreateWordField", 3);
            AddOpCode("CreateByteField", 3);
            AddOpCode("CreateBitField", 3);
            AddOpCode("CreateQWordField", 3);
            AddOpCode("ObjectType", 1);
            AddOpCode("LAnd", 2);
            AddOpCode("LOr", 2);
            AddOpCode("LNot", 1);
            AddOpCode("LNotEqual", 2);
            AddOpCode("LLessEqual", 2);
            AddOpCode("LGreaterEqual", 2);
            AddOpCode("LEqual", 2);
            AddOpCode("LGreater", 2);
            AddOpCode("LLess", 2);
            AddOpCode("ToBuffer", 1, 2);
            AddOpCode("ToDecimalString", 1,2);
            AddOpCode("ToHexlString", 1,2);
            AddOpCode("ToInteger", 1, 2);
            AddOpCode("ToString", 2, 3);
            AddOpCode("CopyObject", 2);
            AddOpCode("Mid", 3,4);
            AddOpCode("Continue");
            AddOpCode("If",1);
            AddOpCode("ElseIf",1); 
            AddOpCode("Else");
            AddOpCode("While",1);
            AddOpCode("Noop",-2);
            AddOpCode("Return");
            AddOpCode("Break", -2);
            AddOpCode("BreakPoint", -2);
            // Start of Scope Data
            AddScopeOpCode("Scope");
            AddScopeOpCode("Buffer");
            AddScopeOpCode("Package");
            AddScopeOpCode("PackageEx");
            AddScopeOpCode("Method");
            AddScopeOpCode("Field");
            AddScopeOpCode("Device");
            AddScopeOpCode("Processor");
            AddScopeOpCode("PowerRes");
            AddScopeOpCode("ThermalZone");
            AddScopeOpCode("IndexField");
            AddScopeOpCode("BankField");
            AddScopeOpCode("If");
            AddScopeOpCode("Else");
            AddScopeOpCode("While");
            InitConditionCode();
        }

        private void AddSubData (List<AcpiData> AcpiDatas, AmlOp amlOp)
        {
            if (amlOp.amlDatas.Count > 0)
            {
                foreach (AcpiData amlData in amlOp.amlDatas)
                {
                    AcpiDatas.Add(amlData);
                }
            }
            if (amlOp.SubOp.Count > 0)
            {
                foreach (AmlOp subOp in amlOp.SubOp)
                {
                    AddSubData(AcpiDatas, subOp);
                }
            }
        }

        public List<AcpiData> GetDebugData (int line)
        {
            List < AcpiData > AcpiDatas = new List<AcpiData>();
            foreach (AmlOp amlOp in amlOps)
            {
                if (amlOp.Line == line)
                {
                    // parse all data
                    //amlOp.SubOp
                    AddSubData(AcpiDatas, amlOp);
                    break;
                }
            }
            return AcpiDatas;
        }

        List<string> ConditionOpCode = new List<string>();
        //public delegate Boolean ConditionAction(AmlOp amlop);
        //Dictionary<string, ConditionAction> actions = new Dictionary<string, ConditionAction>();

        public delegate AcpiData AmlAction(AmlOp amlop);
        Dictionary<string, AmlAction> actions = new Dictionary<string, AmlAction>();


        //public Boolean IfMethod(AmlOp amlop)
        //{
        //    // only one subs... or one data
        //    if (amlop.SubOp.Count + amlop.amlDatas.Count == 1)
        //    {
        //        if (amlop.amlDatas.Count == 1)
        //        {
        //            if (amlop.amlDatas[0].Type != AcpiDataType.Int)
        //            {
        //                return false;
        //            }
        //            UInt64 value = 0;
        //            amlop.amlDatas[0].Data.GetValue(ref value);
        //            return value != 0;
        //            // check the data 
        //        } else
        //        {
        //            // must a logical operation
        //            try
        //            {
        //                return actions[amlop.SubOp[0].OpCode].Invoke(amlop.SubOp[0]);
        //            }catch(Exception)
        //            {
        //                return false;
        //            }
        //        }
        //    }
        //    return false;
        //}
        //public Boolean LAndMethod(AmlOp amlop)
        //{
        //    return false;
        //}

        //public Boolean LEqualMethod(AmlOp amlop)
        //{
        //    // must be 
        //    if (amlop.SubOp.Count + amlop.amlDatas.Count != 2)
        //    {
        //        return false;
        //    }

        //    // 2 Data or method from data
        //    if (amlop.SubOp.Count > 0)
        //    {
        //        // Not ready yet
        //        return false;
        //    }
        //    else
        //    {
        //        if (amlop.amlDatas[0].Type == AcpiDataType.Int)
        //        {
        //            // int compare
        //            if (amlop.amlDatas[1].Type == AcpiDataType.Int)
        //            {
        //                return amlop.amlDatas[1].Value == amlop.amlDatas[0].Value;
        //            } else
        //            {
        //                // Not ready yet
        //                return false;
        //            }
        //        } else
        //        {
        //            // Not ready yet
        //            return false;
        //        }
        //    }
        //    return false;
        //}
        //public Boolean LOrMethod(AmlOp amlop)
        //{

        //}

        //public Boolean LNotMethod(AmlOp amlop)
        //{
        //    if (amlop.amlDatas.Count == 1)
        //    {
        //        // it's a data
        //        if (amlop.amlDatas[0].Type != AcpiDataType.Int)
        //        {
        //            return false;
        //        }
        //        return amlop.amlDatas[0].Value == 0;
        //    } else if(amlop.SubOp.Count == 1)
        //    {
        //        return actions[amlop.SubOp[0].OpCode].Invoke(amlop.SubOp[0]);
        //    }
        //    //amlop.
        //    return false;
        //}
        private UInt64 CompareAcpiData (AcpiData data1, AcpiData data2)
        {
            if (data1 == null || data2 == null)
            {
                return 0;
            }
            if (data1.Type != data2.Type)
            {
                return 0;
            }
            if (data1.strValue.Equals(data2.strValue))
            {
                return 1;
            }
            return 0;
            //if (amlOp.amlDatas.Count == 2)
            //{
            //    // both data
            //    if (amlOp.amlDatas[0].Type != amlOp.amlDatas[1].Type)
            //    {
            //        // compare differetnt type
            //        // System.Diagnostics.Debug.Assert(false);                    
            //        amlData.PutValue(0);
            //        return amlData;
            //    }
            //    else
            //    {
            //        // compare the data....
            //        if (amlOp.amlDatas[0].Data.strVal.Equals(amlOp.amlDatas[1].Data.strVal))
            //        {
            //            amlData.PutValue(1);
            //        }
            //        else
            //        {
            //            amlData.Data.PutValue(0);
            //        }
            //        return amlData;
            //    }
            //}
            //else
            //{

                //}
        }
        private AcpiData AmlLEqualAction(AmlOp amlOp)
        {
            AcpiData data1 = null;
            AcpiData data2 = null;
            AcpiData amlData = new AcpiData();
            amlData.Name = "Result";
            amlData.Type = AcpiDataType.Int;
            if (amlOp.amlDatas.Count + amlOp.SubOp.Count != 2)
            {
                System.Diagnostics.Debug.Assert(false);
            }
            if (amlOp.amlDatas.Count == 2)
            {
                data1 = amlOp.amlDatas[0];
                data2 = amlOp.amlDatas[1];
                   
            }
            else if (amlOp.SubOp.Count == 2)
            {
                data1 = actions[amlOp.SubOp[0].OpCode].Invoke(amlOp.SubOp[0]);
                data2 = actions[amlOp.SubOp[1].OpCode].Invoke(amlOp.SubOp[1]);
            } else
            {
                data1 = actions[amlOp.SubOp[0].OpCode].Invoke(amlOp.SubOp[0]);
                data2 = amlOp.amlDatas[0];
            }
            //System.Diagnostics.Debug.Assert(false);
            amlData.PutValue(CompareAcpiData(data1, data2));
            return amlData;
        }

        private AcpiData AmlLOrAction(AmlOp amlOp)
        {
            AcpiData data1 = null;
            AcpiData data2 = null;
            AcpiData amlData = new AcpiData();
            amlData.Name = "Result";
            amlData.Type = AcpiDataType.Int;
            if (amlOp.amlDatas.Count + amlOp.SubOp.Count != 2)
            {
                System.Diagnostics.Debug.Assert(false);
            }
            if (amlOp.amlDatas.Count == 2)
            {
                data1 = amlOp.amlDatas[0];
                data2 = amlOp.amlDatas[1];

            }
            else if (amlOp.SubOp.Count == 2)
            {
                data1 = actions[amlOp.SubOp[0].OpCode].Invoke(amlOp.SubOp[0]);
                data2 = actions[amlOp.SubOp[1].OpCode].Invoke(amlOp.SubOp[1]);
            }
            else
            {
                data1 = actions[amlOp.SubOp[0].OpCode].Invoke(amlOp.SubOp[0]);
                data2 = amlOp.amlDatas[0];
            }
            //System.Diagnostics.Debug.Assert(false);
            if (data1 == null || data2== null) {
                System.Diagnostics.Debug.Assert(false);
            }
            if (data1.Type != AcpiDataType.Int || data2.Type != AcpiDataType.Int)
            {
                System.Diagnostics.Debug.Assert(false);
            }
            
            if (data1.Value != 0 || data2.Value != 0)
            {
                amlData.PutValue(1);
            } else
            {
                amlData.PutValue(0);
            }
            
            return amlData;
        }

        private AcpiData AmlLIfAction(AmlOp amlOp)
        {
            if (amlOp.amlDatas.Count + amlOp.SubOp.Count != 1)
            {
                System.Diagnostics.Debug.Assert(false);
            }
            if (amlOp.amlDatas.Count == 1)
            {
                if (amlOp.amlDatas[0].Type != AcpiDataType.Int)
                {
                    System.Diagnostics.Debug.Assert(false);
                }
                return amlOp.amlDatas[0];
            } else
            {
                // it's a operation, do a operation then
                return actions[amlOp.SubOp[0].OpCode].Invoke(amlOp.SubOp[0]);
            }
            System.Diagnostics.Debug.Assert(false);
            return null;
        }

        private AcpiData AmlDefaultAction(AmlOp amlOp)
        {
            System.Diagnostics.Debug.Assert(false);
            return null;
        }
        private AcpiData AmlNotAction(AmlOp amlOp)
        {
            AcpiData data1 = null;
            AcpiData amlData = new AcpiData();
            amlData.Name = "Result";
            amlData.Type = AcpiDataType.Int;
            if (amlOp.amlDatas.Count + amlOp.SubOp.Count != 1)
            {
                System.Diagnostics.Debug.Assert(false);
            }
            if (amlOp.amlDatas.Count == 1)
            {
                data1 = amlOp.amlDatas[0];
            }
            else{
                data1 = actions[amlOp.SubOp[0].OpCode].Invoke(amlOp.SubOp[0]);
            }
            if (data1 == null)
            {
                System.Diagnostics.Debug.Assert(false); 
            }
            amlData.PutValue(0);
            if (data1.Value == 0) {
                amlData.PutValue(1);
            }          
            return amlData;
        }

        private AcpiData AmlDefaultNonLogicalAction(AmlOp amlOp)
        {
            AcpiData amlData = new AcpiData();
            amlData.Name = "Result";
            amlData.Type = AcpiDataType.Int;
            amlData.PutValue(1);
            return amlData;
        }

        private AcpiData AmlLNotEqualAction(AmlOp amlOp)
        {
            AcpiData amlData = AmlLEqualAction(amlOp);
            if (amlData.Type != AcpiDataType.Int)
            {
                System.Diagnostics.Debug.Assert(false);
            }
            if (amlData.Value != 0)
            {
                amlData.Value = 0;
            } else
            {
                amlData.Value = 1;
            }
            return amlData;
        }
        private void InitConditionCode()
        {
            //actions[]
            actions["LAnd"] = AmlDefaultAction;
            actions["LOr"] = AmlLOrAction; 
            actions["LNot"]= AmlNotAction; 
            actions["LNotEqual"]= AmlLNotEqualAction; 
            actions["LLessEqual"]= AmlDefaultAction; 
            actions["LGreaterEqual"]= AmlDefaultAction; 
            actions["LEqual"]= AmlLEqualAction; 
            actions["LGreater"]= AmlDefaultAction;
            actions["LLess"]= AmlDefaultAction;   
            actions["If"]= AmlLIfAction;        // Test condition.... 
            actions["While"]= AmlDefaultAction;
            actions["Store"] = AmlDefaultNonLogicalAction;                  
        }

        public Boolean IsConditionCode(AmlOp amlOp)
        {
            return ConditionOpCode.IndexOf(amlOp.OpCode) >= 0;
        }

        public Boolean RunOpCode (AmlOp amlOp, ref Boolean isLocical)
        {
            if (IsConditionCode(amlOp))
            {
                try
                {
                    Boolean bRC = RunConditionCode(amlOp);
                    isLocical = true;
                    return bRC;
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.Assert(false);
                }
            }
            else
            {
                try
                {
                    actions[amlOp.OpCode].Invoke(amlOp);
                    isLocical = false;
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.Assert(false);
                }
            }
            return true;
        }
        public Boolean RunConditionCode(AmlOp amlOp)
        {
            AcpiData bRc =  actions[amlOp.OpCode].Invoke(amlOp);
            if (bRc == null || bRc.Type != AcpiDataType.Int)
            {
                // skip to next block.....
                return false;
            }
            return bRc.Value != 0;
        }

        public AmlOp GetLineOp(int line)
        {
            List<AcpiData> AcpiDatas = new List<AcpiData>();
            foreach (AmlOp amlOp in amlOps)
            {
                if (amlOp.Line == line)
                {
                    // parse all data
                    //amlOp.SubOp
                    return amlOp;
                }
            }
            return null;
        }

        public void Test () {
            //string strTestMethod = 
        }

        // get the raw data string.... etc, device 
    }
}
