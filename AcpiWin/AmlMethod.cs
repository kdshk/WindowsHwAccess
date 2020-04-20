using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AcpiWin
{
    public class AmlMethod
    {
        // delegate method for opcode handling
        private delegate AmlOp SingleOp(string opCode, ref string inner, ref string asl);
        // local data
        private List<AcpiData> amlMethodDatas = new List<AcpiData>();
        private string strRoot;
        private AcpiLib acpiLib;
        private List<AmlOp> amlMethodOps = new List<AmlOp>();
        private AcpiData[] Local = new AcpiData[8];
        private AcpiData[] MethodArg = new AcpiData[8];
        private Dictionary<string, AcpiData> LocalData = new Dictionary<string, AcpiData>();
        private Dictionary<string, SingleOp> OpDefineData = new Dictionary<string, SingleOp>();
        private string strMethodCode;
        private AmlOp LastOp = null;
        private AmlOp LastConOp = null;
        private AcpiData Result = null;
        private string strCondCode;
        private Stack<AmlOp> conditionStack = new Stack<AmlOp>();
        private string RunningCode;
        private string RawCode;
        private Boolean bError = false;
        /// <summary>
        /// Return the error state to let caller terminate 
        /// </summary>
        /// <returns>error state</returns>
        public Boolean HasError ()
        {
            return bError;
        }
        /// <summary>
        /// Code running state
        /// </summary>
        /// <returns>Code finished or not</returns>
        public Boolean Running ()
        {
            return LastOp != null;
        }
        /// <summary>
        /// Get inner code from asl OpCode from a bracket, or brace
        /// </summary>
        /// <param name="inner"></param>
        /// <param name="Type"></param>
        /// <returns>Inner Code</returns>
        private string GetInnerCode (ref string inner, int Type = 0)
        {
            char OpChar = '(';
            char ClChar = ')';
            int nScope = -1;
            if (Type != 0)
            {
                OpChar = '{';
                ClChar = '}';                
            }
            nScope = ScopeSize(inner, OpChar, ClChar);
            if(nScope == -1) {
                return null;
            }
            int nStart = inner.IndexOf(OpChar);
            nStart++;
            //nScope -= nStart;
            string bufData = inner.Substring(nStart);
            bufData = bufData.Substring(0, nScope - nStart- 1);
            inner = inner.Substring(nScope);
            return bufData;
        }
        /// <summary>
        /// Get inner code from asl OpCode from a bracket
        /// </summary>
        /// <param name="inner"></param>
        /// <param name="Type"></param>
        /// <returns>Inner Code</returns>
        private string GetBracketCode (ref string inner)
        {
            return GetInnerCode(ref inner);
        }
        /// <summary>
        /// Get inner code from asl OpCode from a brace
        /// </summary>
        /// <param name="inner"></param>
        /// <param name="Type"></param>
        /// <returns>Inner Code</returns>
        private string GetBraceCode(ref string inner)
        {
            return GetInnerCode(ref inner,1);
        }
        /// <summary>
        /// Get line number of next running code for code highlighting
        /// </summary>
        /// <returns>line number or zero for end</returns>
        public int GetLine()
        {
            //string runCode = RunningCode;
            if (LastOp != null && LastOp.Type == AmlOp.OpType.Condition && LastOp.Code.Length > 2)
            {
                RunningCode = LastOp.Code;
            }
            if (RunningCode.Length < 2)
            {
                RunningCode = strMethodCode;
            }
            string strRun = RawCode.Substring(0, RawCode.LastIndexOf(RunningCode));
            string[] lines = strRun.Split(new char[] { '\n', '\r' });
            string[] rawlines = RawCode.Split(new char[] { '\n', '\r' });
            if(lines.Length == rawlines.Length)
            {
                return lines.Length - 1;
            }
            if (lines == null)
            {
                lines = strRun.Split(new char[] { '\r' });
            }
            if (lines == null || lines.Length == 0)
            {
                return 0;
            }
            string endLine = lines[lines.Length - 1];
            //endLine = endLine.Trim(new char[] { '\n', '\r' });
            if (endLine.Length < 1)
            {
                return lines.Length - 1;
            }
            return lines.Length;
        }
        /// <summary>
        /// Acpi name to AcpiNS NameSeg string
        /// </summary>
        /// <param name="val">acpi name string</param>
        /// <returns>AcpiNS NameSeg string</returns>
        private string ToNameSeg(string val)
        {
            string nameSeg = strRoot;
            string path = val;
            if (val.StartsWith("\\"))
            {
                // this is a simple root
                nameSeg = "\\___";
                path = val.Substring(1);

            }
            else if (val.StartsWith("^"))
            {
                while (path[0] == '^')
                {
                    if (nameSeg.Length >= 8)
                    {
                        nameSeg = nameSeg.Substring(0, nameSeg.Length - 4);
                    }
                    path = path.Substring(1);
                }
            }
            string[] segs = path.Split(new char[] { '.' });
            if (segs.Length > 0)
            {
                foreach (string seg in segs)
                {
                    nameSeg += seg;
                }
            }
            return nameSeg;
        }
        /// <summary>
        /// Debug purpose to make line number more visiblity
        /// </summary>
        /// <returns>remained code to run</returns>
        public string NextCode()
        {
            string runCode = RunningCode;
            if (LastOp != null && LastOp.Type == AmlOp.OpType.Condition && LastOp.Code.Length > 2)
            {
                runCode = LastOp.Code;
            }
            if (runCode == null || runCode.Length < 3)
            {
                runCode = strMethodCode;
            }
            return runCode;
        }
        /// <summary>
        /// Current running code
        /// </summary>
        /// <returns></returns>
        public string CurrentCode()
        {
            if (RunningCode == null)
            {
                return "";
            }
            return CurrentCode(RunningCode);
        }
        private string CurrentCode(string value)
        {
            string code = value;
            code = code.TrimStart(new char[] { '\n', '\r' });
            // get first line
            int nEnd = code.IndexOfAny(new char[] { '\n', '\r' });
            if (nEnd != -1)
            {
                code = code.Substring(0, nEnd);
            }
            return code;
        }
        /// <summary>
        /// Debug Assert for debug purpose
        /// </summary>
        /// <param name="message"></param>
        private void DbgMessage(
            string message)
        {
            bError = true;
            strMethodCode = "";
            //System.Diagnostics.Debug.Assert(false);
            Log.Logs("");
        }
        public AmlMethod(AcpiLib acpiLib, string path)
        {
            //this.amlBuilder = builder;
            this.acpiLib = acpiLib;
            string strTest = test;
            strRoot = path;
            InitOpDefineData();
        }
        /// <summary>
        /// Check and assign the new code if there is any loop operation 
        /// </summary>
        private void CheckUpLevelCondition()
        {
            while(conditionStack.Count > 0)
            {
                AmlOp amlOp = conditionStack.Peek();
                // this this condition still have the code, if not pop it..
                
                // other
                if (amlOp.Code.Length > 2)
                {
                    // still have the code, running it
                    RunningCode = amlOp.Code;
                    LastOp = amlOp;
                    break;
                } else
                {
                    if (amlOp.OpCode == "While")
                    {
                        // it's while op check the condition
                        if (ConditionCodeCheck(amlOp.GetCondCode()))
                        {
                            // still a valid condition, run the while again
                            amlOp.Code = amlOp.Asl;
                            RunningCode = amlOp.Code;
                            LastOp = amlOp;
                            break;
                        }
                        else
                        {
                            // finished running drop it and check next up level of condition code
                            conditionStack.Pop();
                        }
                    }
                    else
                    {
                        // drop the condition
                        conditionStack.Pop();
                    }
                }
            }
            return;
            if (conditionStack.Count > 0)
            {
                AmlOp lastCon = conditionStack.Pop();
                // if a while or a normal condition
                if (lastCon.OpCode == "While")
                {
                    // Check the conditions
                    if (ConditionCodeCheck(lastCon.GetCondCode()))
                    {
                        // still a valid condition, run the while again
                        lastCon.Code = lastCon.Asl;
                        conditionStack.Push(lastCon);
                        RunningCode = LastOp.Code;
                        LastOp = lastCon;
                    }
                    else
                    {
                        // condition is a false, no need to run while again
                        lastCon.Code = "";
                        LastOp = lastCon;
                        RunningCode = LastOp.Code;
                    }
                }
                else
                {
                    // continue run the other if else code..
                    LastOp = lastCon;
                    RunningCode = lastCon.Code;                   
                }
            }

            if (LastOp.Code.Length < 2)
            {
                // NKSNKS: how about also finished.... 
                RunningCode = strMethodCode;
                if (conditionStack.Count > 0)
                { 
                    // current condition code is done, check any previouse condition code need to run
                    AmlOp amlOp = conditionStack.Peek();
                    if (amlOp.Code.Length > 2) {
                        LastOp = amlOp;
                        RunningCode = amlOp.Code;   
                    }
                }
            }
        }
        /// <summary>
        /// get a while op if it existing
        /// </summary>
        /// <returns>uplevel while op or nothing</returns>
        public AmlOp GetWhileOp()
        {
            while (conditionStack.Count > 0)
            {
                // find the while op
                AmlOp lastCon = conditionStack.Pop();
                if (lastCon.OpCode == "While")
                {
                    return lastCon;
                }
            }
            return null;
        }
        /// <summary>
        /// check if the condition value of condition op
        /// </summary>
        /// <param name="condition"></param>
        /// <returns>TRUE/FALSE</returns>
        private Boolean ConditionCodeCheck(string condition)
        {
            AmlOp result = RunMethodStepByStep(ref condition);
            if (result != null && result.Result != null &&
                result.Result.Type == AcpiDataType.Int && result.Result.Value != 0)
            {
                return true;
            }
            return false;
        }
        private Boolean HasInnerCode (AmlOp amlOp)
        {
            if (amlOp != null && amlOp.Type == AmlOp.OpType.Condition && amlOp.Code != null && amlOp.Code.Length > 2)
            {
                return true;
            }
            return false;
        }       
        /// <summary>
        /// Run single AML Code 
        /// </summary>
        /// <returns>Op struct of AML Code</returns>
        public AmlOp RunStep()
        {
            // TODO: NKSNKS Multilevel If handleling...
            // need to check if it's end of condition code, condition code end
            if (LastOp != null && LastOp.Type == AmlOp.OpType.Condition && LastOp.Code.Length > 2)
            {
                // run the code for condition
                AmlOp cond_op = RunMethodStepByStep(ref LastOp.Code);
                RunningCode = LastOp.Code;
                if (HasInnerCode(cond_op))
                {
                    cond_op.Code = cond_op.Code.Trim(new char[] { '\n', '\r' });                                       
                    conditionStack.Push(cond_op);
                    // assign the new running code
                    RunningCode = cond_op.Code;
                    LastOp = cond_op;
                    return LastOp;
                }
                if (cond_op.OpCode == "Return")
                {
                    // finish the method by marking end of the code.
                    RunningCode = strMethodCode = "";
                    return cond_op;
                }
                if (cond_op.OpCode == "Break" || cond_op.OpCode == "Continue")
                {
                    AmlOp lastCon = GetWhileOp();
                    if (lastCon != null)
                    {
                        if (cond_op.OpCode == "Break")
                        {
                            // this is a break request, mark code complete 
                            lastCon.Code = "";
                            LastOp = lastCon;
                        } else
                        {
                            // this is a continue request, check the condition code                      
                            if (ConditionCodeCheck(lastCon.GetCondCode()))
                            {
                                // condition is still a true, then run the while again                                                          
                                lastCon.Code = lastCon.Asl;
                                conditionStack.Push(lastCon);                                
                                RunningCode = LastOp.Code;
                                LastOp = lastCon;
                                return cond_op;
                            }
                            else
                            {
                                // condition is a false, no need to run while again
                                lastCon.Code = "";
                                LastOp = lastCon;
                                RunningCode = LastOp.Code;
                            }
                        }
                    }
                }
                if (LastOp.Code.Length < 4)
                {
                    // finished the sub condition code here
                    // pop the code and continue previous condition or normal code
                    CheckUpLevelCondition();
                }
                return cond_op;
            }
            if (strMethodCode.Trim(new char[] { '\n', '\r' }).Length < 2)
            {
                // no more code to perform, do noting
                return null;
            }
            AmlOp op = RunMethodStepByStep(ref strMethodCode); 
            string SubCode = op.Code;
            if (SubCode != null && SubCode.Length > 2)
            {
                // push a condition Code for later check
                op.Code = op.Code.Trim(new char[] { '\n', '\r' });
                op.Asl = op.Code;
                conditionStack.Push(op);
                strCondCode = SubCode;
            }
            RunningCode = strMethodCode;
            LastOp = op;
            return op;
        }
        /// <summary>
        /// Debug purpose, not used
        /// </summary>
        /// <returns></returns>
        public AmlOp RunStepTest()
        {
            if (LastOp != null && LastOp.Type == AmlOp.OpType.Condition)
            {
                // there is previous code, to check if the condition need to be run

                if (LastOp.Result.Type == AcpiDataType.Int &&
                        LastOp.Code != null && LastOp.Code.Length > 0)
                {
                    //AmlOp amlOp = RunMethodStepByStep(ref asl);
                    if (LastOp.Code.Length > 2)
                    {
                        //RunConditionCode(LastOp, ref LastOp.Code);
                        AmlOp cond_op = RunMethodStepByStep(ref LastOp.Code);
                        LastConOp = cond_op;
                        RunningCode = LastOp.Code;
                        Boolean bCond = true;
                        if (LastConOp.Code.Length > 2 && LastConOp.Type == AmlOp.OpType.Condition)
                        {
                            // it's push code
                            cond_op.Asl = cond_op.Code;
                            conditionStack.Push(cond_op);
                            RunningCode = LastConOp.Code;
                            bCond = false;
                        }
                        string strCode = LastOp.Code.Trim(new char[] { '\r', '\n', ' ' });
                        if (strCode.Length < 2 && bCond)
                        {
                            AmlOp popOp = conditionStack.Pop();
                            // check if it's while
                            string cond_code = popOp.ConditionCode;
                            if (popOp.OpCode == "While" && cond_code.Length > 0)
                            {
                                // check the condition again to decide next code    
                                cond_code = "While" + cond_code;
                                AmlOp result = RunMethodStepByStep(ref cond_code);
                                if (result.Result != null && result.Result.Type == AcpiDataType.Int &&
                                    result.Result.Value == 1)
                                {
                                    // continue run while code
                                    LastOp.Code = popOp.Asl;
                                    strCode = popOp.Asl;
                                    RunningCode = LastOp.Code;
                                    popOp.Code = popOp.Asl;
                                    conditionStack.Push(popOp);
                                }
                            }
                        }
                        if (LastConOp.Code.Length > 2 && strCode.Length < 2) 
                        {
                            // need to run the new code of condition
                            
                            LastOp.Code = LastConOp.Code;
                        }
                        if (RunningCode.Length < 2)
                        {
                            RunningCode = strMethodCode;
                        }
                        return LastConOp;
                    }
                }
            }
            if (strMethodCode.Length < 1)
            {
                // finish code running
                return null;
            }
            AmlOp op = RunMethodStepByStep(ref strMethodCode);
            LastOp = op;
            RunningCode = strMethodCode;
            if (LastOp != null && LastOp.Type == AmlOp.OpType.Condition)
            {
                LastOp.Asl = LastOp.Code;
                RunningCode = LastOp.Code;
                if (RunningCode.Length < 3)
                {
                    RunningCode = strMethodCode;
                } else
                {
                    conditionStack.Push(op);
                }
            }
            return LastOp;
        }
        /// <summary>
        /// Check a name string is all upper
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        bool IsAllUpper(string input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (Char.IsLetter(input[i]) && !Char.IsUpper(input[i]))
                    return false;
            }
            return true;
        }
        /// <summary>
        /// Check if a target is a acpi
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        private int IsAcpiNS(ref string Name)
        {
            //string ReducedName = Name.R
            string FullPath = "";
            // skip the null, empty or string
            if (Name == null || Name.Length == 0 || Name.Contains("\""))
            {
                return -1;
            }
            if (IsAllUpper(Name) && acpiLib != null)
            {
                // check if it's all captical 
                //return Name.All(c => char.IsUpper(c));
                // or Name.All(char.IsUpper);
                int result;
                FullPath = ToNameSeg(Name); // get the path name the name
                string NameSeg = FullPath.Substring(FullPath.Length - 4);
                FullPath = FullPath.Substring(0, FullPath.Length - 4);
                while (FullPath.Length >= 4)
                {
                    if ((result = acpiLib.GetType(FullPath + NameSeg)) != -1)
                    {
                        Name = FullPath + NameSeg;
                        return result;
                    }
                    FullPath = FullPath.Substring(0, FullPath.Length - 4);
                }
            } 
            return -1;
        }
        /// <summary>
        /// Prepare the aml method asl code and input args
        /// </summary>
        /// <param name="asl">asl code of method</param>
        /// <param name="Args">method args or empty</param>
        public void PrepareMethodRun(string asl, List<AcpiData> Args)
        {  
            asl = asl.Replace(" ", "");
            asl = asl.Replace("\t", "");
            RawCode = asl;
            int nScope = ScopeSize(asl, '{', '}');
            if (asl.IndexOf("Method(") == -1 || nScope == -1)
            {
                DbgMessage();
                return;
            }
            asl = asl.Remove(0, asl.IndexOf("Method("));
            // move to scope of method...
            string method = asl.Substring(0, asl.IndexOf('{'));
            method = method.Substring(6);
            AmlOp amlMethod = DefMethod("Method", ref method, ref method);
            //asl = asl.Substring(asl.IndexOf('{') + 1);
            int nStart = asl.IndexOf('{');
            nScope = ScopeSize(asl, '{', '}');
            asl = asl.Substring(nStart, nScope - nStart);
            asl = asl.Remove(0, 1);
            asl = asl.Remove(asl.Length - 1, 1);
            strMethodCode = asl;
            int ArgCount = Args == null ? 0 : Args.Count;
            for (int Index = 0; Index < Local.Length; Index++)
            {
                Local[Index].Type = AcpiDataType.Unknown;
            }
            for (int Index = 0; Index < MethodArg.Length; Index++)
            {
                MethodArg[Index].Type = AcpiDataType.Unknown;
            }
            for (int Index = 0; Index < ArgCount; Index++)
            {
                MethodArg[Index].SetValue(Args[Index]);
            }
        }
        /// <summary>
        /// for caller to collect args and view
        /// </summary>
        /// <returns></returns>
        public List<AcpiData> ViewArgData()
        {
            List<AcpiData> args = new List<AcpiData>();
            for (int Index = 0; Index < MethodArg.Length; Index++)
            {
                if (MethodArg[Index].Type != AcpiDataType.Unknown)
                {
                    args.Add(MethodArg[Index]);
                }
            }
            return args;
        }
        /// <summary>
        /// for caller to collect local and view
        /// </summary>
        /// <returns></returns>
        public List<AcpiData> ViewLocalData()
        {
            List<AcpiData> args = new List<AcpiData>();
            for (int Index = 0; Index < Local.Length; Index++)
            {
                if (Local[Index].Type != AcpiDataType.Unknown)
                {
                    args.Add(Local[Index]);
                }
            }
            return args;
        }
        /// <summary>
        /// for caller to collect datas and view
        /// </summary>
        /// <returns></returns>
        public List<AcpiData> ViewDefinedData()
        {
            return amlMethodDatas;
        }
        /// <summary>
        /// Get the next Single line of ASL code from asl codes
        /// </summary>
        /// <param name="asl">ASL Codes set</param>
        /// <returns>single line of code or empty</returns>
        private string GetSingleOpString(ref string asl)
        {
            string inner = null;
            asl = asl.TrimStart(new char[] { '\n', '\r', ' ' });
            if (asl.Length < 1)
            {
                return asl;
            }
            int nEnd = asl.IndexOfAny(new char[] { '\n', '(', ')', '{', '}' });
            if (nEnd == -1 && asl.Length > 1)
            {
                if (OpDefineData.ContainsKey(asl))
                {
                    string Code = asl;
                    asl = "";
                    return Code;
                }
            }
            if (asl[nEnd] == '(')
            {
                //
                // this is a data operation, refer, define or update
                // the close brace is the end
                int nScope = ScopeSize(asl, '(', ')');
                inner = asl.Substring(0, nScope);
                if (asl.Length > nScope)
                {
                    asl = asl.Substring(nScope + 1);
                }
                else
                {
                    asl = asl.Substring(nScope);
                }

            }
            else if (asl[nEnd] == '\n')
            {
                // it's a non data operations
                //return asl.Substring(0, nEnd).Trim(new char[] { '\r', '\n' });
                inner = asl.Substring(0, nEnd).Trim(new char[] { '\r', '\n' });
                if (asl.Length > nEnd)
                {
                    asl = asl.Substring(nEnd + 1);
                }
                else
                {
                    asl = asl.Substring(nEnd);
                }
            }
            else if (asl[nEnd] == '{')
            {
                // it's a condition, while or defintion of others
                int nScope = ScopeSize(asl, '{', '}');

                if (asl.Length > nScope)
                {
                    asl = asl.Substring(nScope + 1);
                }
                else
                {
                    asl = asl.Substring(nScope);
                }
            }
            else
            {
                DbgMessage();
            }
            return inner;
        }
        /// <summary>
        /// get next parameter from opCode inner
        /// </summary>
        /// <param name="inner">opCode inner code</param>
        /// <param name="ops">options for terminate or others</param>
        /// <returns>Parameters</returns>
        private string GetNextPara(ref string inner, ref int ops)
        {
            //if (inner[0])
            //return;
            if (inner.Length < 1)
            {
                ops = 0;
                return null;
            }
            if (inner.IndexOf(',') == 0)
            {
                inner = inner.TrimStart(',');
            }
            int nEnd = inner.IndexOfAny(new char[] { ',', '(', ')' });
            if (nEnd == -1)
            {
                if (inner.Length > 0)
                {
                    string lastWord = inner;
                    inner = "";
                    return lastWord;
                }
                return null;
            }
            if (nEnd == 0)
            {
                return null;
            }
            string word = inner.Substring(0, nEnd);
            if (inner[nEnd] == ')')
            {
                // this is the last arg                
                ops = 0;
            }
            else if (inner[nEnd] == ',')
            {
                // it has next arg
                ops = 1;
            }
            else if (inner[nEnd] == '(')
            {
                // this as a internal operation     
                ops = 2;
                char opch = '(';
                char clch = ')';

                int nScope = ScopeSize(inner, '(', ')');
                if (nScope == -1)
                {
                    DbgMessage();
                }
                word = inner.Substring(0, nScope);
                if (inner.Length > nScope && inner[nScope] == '{')
                {
                    nScope = ScopeSize(inner, '{', '}');
                    if (nScope == -1)
                    {
                        DbgMessage();
                    }
                    word = inner.Substring(0, nScope);
                    inner = inner.Substring(nScope);
                    // follow with a new start
                }
                else
                {
                    inner = inner.Substring(nScope);
                }
            }
            if (ops != 2)
            {
                inner = inner.Substring(nEnd + 1);
            }
            return word;
        }
        /// <summary>
        /// Debug purpose, not used 
        /// </summary>
        /// <param name="amlCondition"></param>
        /// <param name="asl"></param>
        private void RunConditionCode(AmlOp amlCondition, ref string asl)
        {
            //int nStart = asl.IndexOf('{');
            //asl = asl.Substring(nStart + 1);
            //int nEnd = asl.LastIndexOf('}');
            //asl = asl.Substring(0, nEnd);
            asl = asl.Trim(new char[] { '\r', '\n' });
            string nCode = asl; // check the condition again....
            while (true)
            {
                if (amlCondition.OpCode == "While")
                {
                    int n = 0;
                }
                if (asl.Length < 1)
                {
                    if (amlCondition.OpCode == "While")
                    {
                        // continue run it until a break
                        asl = nCode;
                    }
                    break;
                }
                AmlOp amlOp = RunMethodStepByStep(ref asl);
                if (amlOp != null && amlOp.Type == AmlOp.OpType.Condition)
                {
                    // Run inner code??
                    if (amlOp.Result.Type == AcpiDataType.Int &&
                        amlOp.Code != null && amlOp.Code.Length > 0)
                    {
                        //AmlOp amlOp = RunMethodStepByStep(ref asl);
                        string code = amlOp.Code;
                        RunConditionCode(amlCondition, ref code);
                    }
                }
                else if (amlOp != null)
                {
                    //
                    if (amlOp.OpCode == "Break")
                    {
                        // break the while,
                        break;
                    }
                    else if (amlOp.OpCode == "Continue")
                    {
                        // 
                        // TODO: Check the code again for result
                        //
                        //AmlOp amlC = DefA
                        //AmlOp amlC = DefAllData(amlCondition.OpCode, ref amlCondition.ConditionCode, ref amlCondition.Code);
                        string cCode = amlCondition.ConditionCode;
                        cCode = cCode.Substring(cCode.IndexOf('(') + 1);
                        cCode = cCode.Substring(0, cCode.LastIndexOf(')'));
                        AmlOp amlC = RunMethodStepByStep(ref cCode);
                        if (amlC != null && amlC.Result.Type == AcpiDataType.Int)
                        {
                            if (amlC.Result.Value != 0)
                            {
                                asl = nCode;
                                continue;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            DbgMessage();
                        }
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// Get the single line code and run it
        /// </summary>
        /// <param name="asl">asl code set</param>
        /// <returns>Op struct with opCode, conditions, result value</returns>
        private AmlOp RunMethodStepByStep(ref string asl)
        {
            string debug = asl;
            string line;
            AmlOp amlOp = null;
            // process the first valid code
            while (amlOp == null && asl.Length > 0)
            {
                line = GetSingleOpString(ref asl);
                if (line == null || line.Length < 1)
                {
                    continue;
                }
                CleanAslInner(ref line);
                //string CoditionCode = line;
                amlOp = SingleOpCode(ref line, ref asl);
            }

            // now I got the code and make sure it's valid
            if (amlOp != null)
            {
                if (amlOp.Type == AmlOp.OpType.Definition)
                {
                    // assign a data definition
                    // amlMethodDatas.Add(amlOp.Result);
                }
                else if (amlOp.Type == AmlOp.OpType.Condition)
                {
                    // assign a data definition
                    amlMethodOps.Add(amlOp);
                    amlOp.Code = GetScopeCode(ref asl);
                    if (amlOp.Result.Type == AcpiDataType.Int
                        && amlOp.Result.Value == 0)
                    {
                        string ElseCode =
                            asl.TrimStart(new char[] { '\r', '\n', ' ' });
                        if (ElseCode.StartsWith("Else"))
                        {
                            // get the else cpde
                            amlOp.Code = GetScopeCode(ref asl);
                        }
                        else if (ElseCode.StartsWith("ElseIf"))
                        {
                            // there is a multiple else run one by one by change it to 
                            // If by remove else
                            amlOp.Code = "";
                            asl = asl.Substring(4);
                        }
                        else
                        {
                            // if the test condition is failed, no need to run the code without else
                            amlOp.Code = "";
                        }
                    } else
                    {
                        // need to skip the all else code, if condition is to run if
                        while (true)
                        {
                            string ElseCode =
                                asl.TrimStart(new char[] { '\r', '\n', ' ' });
                            if (!ElseCode.StartsWith("Else"))
                            {
                                break;
                            }
                            string braceCode = GetBraceCode(ref asl);
                        }
                    }
                    amlOp.Asl = amlOp.Code;
                }
                else
                {
                    // assign a code running
                    amlMethodOps.Add(amlOp);
                }
            }
            return amlOp;
        }
        private string GetScopeCode(ref string inner)
        {
            int nScope = ScopeSize(inner, '{', '}');
            if (nScope < 2)
            {
                return null;
            }
            string Code = inner.Substring(0, nScope);
            // Remove open and close brace 
            Code = Code.Substring(Code.IndexOf('{') + 1);
            Code = Code.Substring(0, Code.LastIndexOf('}'));
            inner = inner.Substring(nScope);
            return Code;
        }
        /// <summary>
        /// Get 2 parameter from inner code
        /// </summary>
        /// <param name="opCode">OpCode</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp Def2Data(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || GetOpArgs(amlOp) != 2)
            {
                DbgMessage();
                return null;
            }          
            amlOp.Type = AmlOp.OpType.Value;
            amlOp.Name = opCode;          
            return amlOp;
        }
        /// <summary>
        /// Get 1 parameter from inner code
        /// </summary>
        /// <param name="opCode">OpCode</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp Def1Data(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || GetOpArgs(amlOp) != 1)
            {
                DbgMessage();
                return null;
            }
            amlOp.Type = AmlOp.OpType.Value;
            amlOp.Name = opCode;
            return amlOp;
        }
        /// <summary>
        /// Get all parameter from inner code
        /// </summary>
        /// <param name="opCode">OpCode</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefAllData(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = DefData(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            if (GetOpArgs(amlOp) == 0)
            {
                return amlOp;
            }
            AcpiData[] args = GetOpArgs(amlOp, GetOpArgs(amlOp));
            amlOp.Args = args;
            if (args == null)
            {
                DbgMessage();
            }
            amlOp.Type = AmlOp.OpType.Value;
            amlOp.Name = opCode;
            amlOp.Result.Name = "Result";
            for (int index = 0; index < args.Length; index++)
            {
                args[index] = QueryRefData(args[index]);
            }
            return amlOp;
        }
        /// <summary>
        /// Get 1 or 2 parameter from inner code
        /// </summary>
        /// <param name="opCode">OpCode</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp Def1Or2Data(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || (GetOpArgs(amlOp) != 1 && GetOpArgs(amlOp) != 2))
            {
                return null;
            }
            return amlOp;
        }
        /// <summary>
        /// Get 2 or 3 parameter from inner code
        /// </summary>
        /// <param name="opCode">OpCode</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp Def2Or3Data(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || (GetOpArgs(amlOp) != 2 && GetOpArgs(amlOp) != 3))
            {
                return null;
            }
            return amlOp;
            //AmlOp amlOp = DefData(opCode, ref inner, ref asl);
            //if (amlOp == null || GetOpArgs(amlOp) < 2)
            //{
            //    DbgMessage();
            //}
            //AcpiData[] args = null;
            //if (GetOpArgs(amlOp) >= 3)
            //{
            //    args = GetOpArgs(amlOp, 3);
            //}
            //else
            //{
            //    args = GetOpArgs(amlOp, 2);
            //}

            //amlOp.Args = args;
            //if (args == null)
            //{
            //    DbgMessage();
            //}
            //amlOp.Type = AmlOp.OpType.Value;
            //amlOp.Name = opCode;
            //amlOp.Result.Name = "Result";
            //args[0] = QueryRefData(args[0]);
            //args[1] = QueryRefData(args[1]);
            //if (args.Length > 2)
            //{
            //    args[2] = QueryRefData(args[2]);
            //}
            //return amlOp;
        }
        /// <summary>
        /// Run a IfOp and get the return code
        /// </summary>
        /// <param name="opCode">"If"</param>
        /// <param name="inner">"Conditions"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefIf(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
                amlOp = null;
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Condition;
                // Assignt the result
                amlOp.Result.Assign(amlOp.Args[0]);
            }
            return amlOp;
        }
        /// <summary>
        /// Run a Store code and store the first parameter to target
        /// </summary>
        /// <param name="opCode">"Store"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefStore(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                AcpiData src = amlOp.Args[0];
                AcpiData dst = amlOp.Args[1];
                // TODO: NKSNKS Check how to store all the data
                if (dst.Type == AcpiDataType.FieldUnit)
                {
                    dst.SetValue(src);
                }
                else
                {
                    dst.SetValue(src);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Subtract operation
        /// </summary>
        /// <param name="opCode">"Subtract"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefSubtract(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Sub(amlOp.Args[1]));
                if (amlOp.Args.Length > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Mid operation
        /// </summary>
        /// <param name="opCode">"Mid"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefMid(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                if ((amlOp.Args[0].Type != AcpiDataType.String &&
                    amlOp.Args[0].Type != AcpiDataType.Buffer) ||
                    amlOp.Args[1].Type != AcpiDataType.Int ||
                    amlOp.Args[2].Type == AcpiDataType.Int)
                {
                    DbgMessage();
                }
                amlOp.Result.Assign(amlOp.Args[0].Mid(
                    (int)amlOp.Args[1].Value,
                    (int)amlOp.Args[2].Value
                    ));
                if (amlOp.Args.Length > 3)
                {
                    amlOp.Args[3].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// SizeOf operation
        /// </summary>
        /// <param name="opCode">"SizeOf"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefSizeOf(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result = amlOp.Args[0].Size();
            }
            return amlOp;
        }
        /// <summary>
        /// Add operation
        /// </summary>
        /// <param name="opCode">"Add"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefAdd(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Add(amlOp.Args[1]));
                if (amlOp.Args.Length > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Index operation
        /// </summary>
        /// <param name="opCode">"Index"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefIndex(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Index((int)amlOp.Args[1].Value));
                if (amlOp.Args.Length > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// ObjectType operation
        /// </summary>
        /// <param name="opCode">"ObjectType"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefObjectType(string opCode, ref string inner, ref string asl)
        {            
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                // TODO:NKSNKS get the object type.
                amlOp.Result.Value = (UInt64)amlOp.Args[0].Type;
                amlOp.Result.Type = AcpiDataType.Int;
            }
            return amlOp;
        }
        /// <summary>
        /// Mod operation
        /// </summary>
        /// <param name="opCode">"Mod"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefMod(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Mod(amlOp.Args[1]));
                if (amlOp.Args.Length > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Copy operation
        /// </summary>
        /// <param name="opCode">"Copy"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefCopy(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                // TODO: NKSNKS Check copy operation again
                amlOp.Result = amlOp.Args[1].Copy(amlOp.Args[0]);
            }
            return amlOp;
        }
        /// <summary>
        /// Divide operation
        /// </summary>
        /// <param name="opCode">"Divide"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefDivide(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Divide(amlOp.Args[1]));
                if (amlOp.Args.Length > 2)               
                {
                    
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// FindLeftBit/FindRightBit operation
        /// </summary>
        /// <param name="opCode">"FindLeftBit/FindRightBit"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefFindBit(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                if (opCode.Contains("Left"))
                {
                    amlOp.Result.Assign(amlOp.Args[0].FindLeft());
                    if (amlOp.Args.Length > 1)
                    {
                        amlOp.Args[1].SetValue(amlOp.Result);
                    }
                }
                else
                {
                    amlOp.Result.Assign(amlOp.Args[0].FindRight());
                    if (amlOp.Args.Length > 1)
                    {
                        amlOp.Args[1].SetValue(amlOp.Result);
                    }
                }
            }
            return amlOp;
        }
        /// <summary>
        /// FromBCD operation
        /// </summary>
        /// <param name="opCode">"FromBCD"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefFromBCD(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].FromBCD());
                if (amlOp.Args.Length > 1)
                {
                    amlOp.Args[1].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Multiple operation
        /// </summary>
        /// <param name="opCode">"Multiple"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefMultiple(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Multiple(amlOp.Args[1]));
                if (amlOp.Args.Length > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Not operation
        /// </summary>
        /// <param name="opCode">"Not"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefNotOp(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Op(null, opCode));
                if (amlOp.Args.Length > 1)
                {
                    amlOp.Args[1].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// _OSI User method operation
        /// </summary>
        /// <param name="opCode">"_OSI/OSI"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefOSI(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.OSPM; // OS defined method
                amlOp.Result.Type = AcpiDataType.Int;
                amlOp.Result.Value = 0;
                if (acpiLib.DriverLoaded())
                {
                    string value = "";
                    IntPtr pArg = acpiLib.ArgPutString(IntPtr.Zero, amlOp.Args[0].strValue);
                    if (acpiLib.GetEvalArgResult("\\____OSI", pArg, ref value))
                    {
                        // now get the data, must be a value of integer
                        UInt64 ulValue = 0;
                        if (IsIntString(value, ref ulValue))
                        {
                            amlOp.Result.Value = ulValue;
                        }
                    }
                    acpiLib.FreeArg(pArg);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Locagical Math  operation like Or, Xor, And, NAnd
        /// </summary>
        /// <param name="opCode">"Locagical Math"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefOp(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Op(amlOp.Args[1], opCode));

                if (amlOp.Args.Length > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Concat  operation
        /// </summary>
        /// <param name="opCode">"Concat"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefConcat(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Type = AmlOp.OpType.Type2Op;
                amlOp.Result.Assign(amlOp.Args[0].Concat(amlOp.Args[1]));
                if (amlOp.Args.Length > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// LNot  operation
        /// </summary>
        /// <param name="opCode">"LNot"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefLNot(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Result.Type = AcpiDataType.Int;
                amlOp.Result.Assign(amlOp.Args[0].LOp(null, opCode));
            }
            return amlOp;
        }
        /// <summary>
        /// Logical Operation include LEqual, LNotEqual...
        /// </summary>
        /// <param name="opCode">"Logical"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefLOp(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Result.Type = AcpiDataType.Int;
                amlOp.Result.Assign(amlOp.Args[0].LOp(amlOp.Args[1], opCode));
            }
            return amlOp;
        }
        /// <summary>
        /// ToHexString Operation 
        /// </summary>
        /// <param name="opCode">"ToHexString"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefToHexString(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Result.Assign(amlOp.Args[0].ToHexString());
                if (GetOpArgs(amlOp) > 1)
                {
                    amlOp.Args[1].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// ToBuffer Operation 
        /// </summary>
        /// <param name="opCode">"ToBuffer"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefToBuffer(string opCode, ref string inner, ref string asl)
        {            
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Result.Assign(amlOp.Args[0].ToBuffer());
                if (GetOpArgs(amlOp) > 1)
                {
                    amlOp.Args[1].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// ToBcd Operation 
        /// </summary>
        /// <param name="opCode">"ToBcd"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefToBcd(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Result.Assign(amlOp.Args[0].ToBCD());
                if (GetOpArgs(amlOp) > 1)
                {
                    amlOp.Args[1].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// ToDecString Operation 
        /// </summary>
        /// <param name="opCode">"ToDecString"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefToDecString(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Result.Assign(amlOp.Args[0].ToDecString());
                if (GetOpArgs(amlOp) > 1)
                {
                    amlOp.Args[1].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// ShiftLeft/ShiftRigth Operation 
        /// </summary>
        /// <param name="opCode">"ShiftLeft/ShiftRigth"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefShift(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Or3Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                if (opCode.Contains("Left"))
                {
                    amlOp.Result.Assign(amlOp.Args[0].ShiftLeft((int)amlOp.Args[1].Value));
                }
                else
                {
                    amlOp.Result.Assign(amlOp.Args[0].ShiftRight((int)amlOp.Args[1].Value));
                }
                if (GetOpArgs(amlOp) > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// ToSring Operation 
        /// </summary>
        /// <param name="opCode">"ToSring"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefToString(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                if (GetOpArgs(amlOp) > 1)
                {
                    amlOp.Result.Assign(amlOp.Args[0].ToString((int)amlOp.Args[1].Value));
                }
                else
                {
                    amlOp.Result.Assign(amlOp.Args[0].ToString());
                }
                if (GetOpArgs(amlOp) > 2)
                {
                    amlOp.Args[2].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// ToSring Operation 
        /// </summary>
        /// <param name="opCode">"ToSring"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefToInteger(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Or2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                
                amlOp.Type = AmlOp.OpType.Value;
                // only one args with string, integer or buffer type
                if (amlOp.Args[0].Type == AcpiDataType.Buffer)
                {
                    if (amlOp.Args[0].bpValue.Length < 8)
                    {
                        DbgMessage();
                    }
                    UInt64 value = BitConverter.ToUInt64(amlOp.Args[0].bpValue, 0);
                    amlOp.Result.Type = AcpiDataType.Int;
                    amlOp.Result.Value = value;
                }
                else if (amlOp.Args[0].Type == AcpiDataType.String)
                {
                    // do transfer
                    UInt64 value = 0;
                    if (!IsIntString(amlOp.Args[0].strValue, ref value))
                    {
                        DbgMessage();
                    }
                    amlOp.Result.Type = AcpiDataType.Int;
                    amlOp.Result.Value = value;
                }
                else if (amlOp.Args[0].Type == AcpiDataType.Int)
                {
                    // just use this data
                    amlOp.Result.Assign(amlOp.Args[0]);
                }
                else
                {
                    // refer to somthing?
                    return null;
                }
                if (GetOpArgs(amlOp) > 1)
                {
                    amlOp.Args[1].SetValue(amlOp.Result);
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Process the buffer data from package
        /// </summary>
        /// <param name="pkg">Parent Pacakge</param>
        /// <param name="inner">Next String Args from package</param>
        /// <returns>Package Data</returns>
        private AcpiPackage ProcessBuffer(ref AcpiPackage pkg, ref string inner)
        {
            if (pkg == null)
            {
                pkg = new AcpiPackage();
                pkg.Type = AcpiDataType.Buffer;
            }
            int nScope = ScopeSize(inner, '{', '}');
            if (nScope == -1)
            {
                DbgMessage();
            }
            string strCount = inner.Substring(0, inner.IndexOf(')'));
            strCount = strCount.Substring(strCount.IndexOf('('));
            strCount = strCount.Trim(new char[] { '(', ')', '\r', '\n', ' ' });
            UInt64 ulSize = 0;
            if (!IsIntString(strCount, ref ulSize))
            {
                DbgMessage("Invalid package size valie");
            }
            // ulSize size contain number of 
            string strPackage = inner.Substring(inner.IndexOf('{') + 1);
            strPackage = strPackage.Substring(0, strPackage.LastIndexOf('}'));
            strPackage = strPackage.Trim(new char[] { '\r', '\n', ' ' });
            // process the data of pacakge now
            for (UInt64 Index = 0; Index < ulSize; Index++)
            {
                if (strPackage.Length < 1)
                {
                    DbgMessage();
                }
                // only the data
                string strVal = "";
                if (strPackage.IndexOf(',') == -1)
                {
                    strVal = strPackage;
                    strPackage = "";
                }
                else
                {
                    strVal = strPackage.Substring(0, strPackage.IndexOf(','));
                    strPackage = strPackage.Substring(strPackage.IndexOf(','));
                    strPackage = strPackage.TrimStart(new char[] { ',', '\r', '\n', ' ' });
                }
                //UInt64 ulVal = 0;
                AcpiPackage dataPkg = new AcpiPackage();
                dataPkg.Type = AcpiDataType.Int;
                if (!IsIntString(strVal, ref dataPkg.Value))
                {
                    DbgMessage();
                }
                pkg.pkgs.Add(dataPkg);
            }
            return pkg;
        }
        /// <summary>
        /// Process the Package data from package
        /// </summary>
        /// <param name="pkg">Parent Pacakge</param>
        /// <param name="inner">Next String Args from package</param>
        /// <returns>Package Data</returns>
        private AcpiPackage ProcessPackge(ref AcpiPackage pkg, ref string inner)
        {
            if (pkg == null)
            {
                pkg = new AcpiPackage();
                pkg.Type = AcpiDataType.Packge;
            }
            int nScope = ScopeSize(inner, '{', '}');
            if (nScope == -1)
            {
                DbgMessage();
            }
            string strCount = inner.Substring(0, inner.IndexOf(')'));
            strCount = strCount.Substring(strCount.IndexOf('('));
            strCount = strCount.Trim(new char[] { '(', ')', '\r', '\n', ' ' });
            UInt64 ulSize = 0;
            if (!IsIntString(strCount, ref ulSize))
            {
                DbgMessage("Invalid package size valie");
            }
            // ulSize size contain number of 
            string strPackage = inner.Substring(inner.IndexOf('{') + 1);
            strPackage = strPackage.Substring(0, strPackage.LastIndexOf('}'));
            strPackage = strPackage.Trim(new char[] { '\r', '\n', ' ' });
            // process the data of Packge now
            for (UInt64 Index = 0; Index < ulSize; Index++)
            {
                if (strPackage.Length < 1)
                {
                    DbgMessage();
                }
                if (strPackage.StartsWith("Package"))
                {
                    // get the code and add the package...
                    int nPackage = ScopeSize(strPackage, '{', '}');
                    if (nPackage == -1)
                    {
                        DbgMessage();
                    }
                    string strSubPkg = strPackage.Substring(0, nPackage);
                    strPackage = strPackage.Substring(nPackage);
                    strPackage = strPackage.TrimStart(new char[] { ',', '\r', '\n', ' ' });
                    AcpiPackage emptyPkg = null;
                    AcpiPackage subPkg = ProcessPackge(ref emptyPkg, ref strSubPkg);
                    pkg.pkgs.Add(subPkg);
                }
                else if (strPackage.StartsWith("Buffer"))
                {
                    int nBuffer = ScopeSize(strPackage, '{', '}');
                    if (nBuffer == -1)
                    {
                        DbgMessage();
                    }
                    string strSubBuf = strPackage.Substring(0, nBuffer);
                    strPackage = strPackage.Substring(nBuffer);
                    strPackage = strPackage.TrimStart(new char[] { ',', '\r', '\n', ' ' });
                    AcpiPackage emptyPkg = null;
                    AcpiPackage subPkg = ProcessBuffer(ref emptyPkg, ref strSubBuf);
                    pkg.pkgs.Add(subPkg);
                }
                else
                {
                    // to a value by ","
                    string strVal = "";
                    if (strPackage.IndexOf(',') == -1)
                    {
                        strVal = strPackage;
                        strPackage = "";
                    }
                    else
                    {
                        strVal = strPackage.Substring(0, strPackage.IndexOf(','));
                        strPackage = strPackage.Substring(strPackage.IndexOf(','));
                        strPackage = strPackage.TrimStart(new char[] { ',', '\r', '\n', ' ' });
                    }
                    //UInt64 ulVal = 0;
                    AcpiPackage dataPkg = new AcpiPackage();
                    dataPkg.Type = AcpiDataType.Int;
                    if (!IsIntString(strVal, ref dataPkg.Value))
                    {
                        dataPkg.Type = AcpiDataType.String;
                        dataPkg.strValue = strVal;
                    }
                    pkg.pkgs.Add(dataPkg);
                }
            }
            return pkg;
        }
        /// <summary>
        /// Packge Operation 
        /// </summary>
        /// <param name="opCode">"Packge"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefPackge(string opCode, ref string inner, ref string asl)
        {            
            AmlOp amlOp = new AmlOp(opCode);
            AcpiPackage package = null;
            string strPackage = inner + "{" +GetBraceCode(ref asl)+"}";
            package = ProcessPackge(ref package, ref strPackage);
            amlOp.Result.Type = AcpiDataType.Packge;
            amlOp.Result.Pkg = package;
            return amlOp;
        }
        /// <summary>
        /// Buffer Operation 
        /// </summary>
        /// <param name="opCode">"Buffer"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefBufData(string opCode, ref string inner, ref string asl)
        {
            string strBuffer = inner;
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            int DataLength = -1;
            if (amlOp.Args != null  && amlOp.Args[0].Type == AcpiDataType.Int)
            {
                DataLength = (int)amlOp.Args[0].Value;
            } 
            int Idx = 0;
            AcpiData amlData = new AcpiData();
            // some buffer definition without the code, just a empty
            amlData.Type = AcpiDataType.Buffer;
            //int nStart = strBuffer.IndexOf('{');
            //string bufData = strBuffer.Substring(nStart + 1);
            //int nEnd = bufData.IndexOf('}');
            //bufData = bufData.Substring(0, nEnd);
            string bufData = GetBraceCode(ref strBuffer);
            string[] data = bufData.Split(new char[] { ',' });
            if (data.Length > DataLength)
            {
                DataLength = data.Length;
            }
            amlData.bpValue = new byte[DataLength];
            amlData.Type = AcpiDataType.Buffer;
            if (DataLength > 0 && bufData.Length > 0)
            {
                foreach (string value in data)
                {
                    UInt64 val = 0;
                    if (IsIntString(value, ref val))
                    {
                        //DbgMessage();
                        amlData.bpValue[Idx] = (byte)val;
                    }
                    Idx++;
                }
            } else
            amlOp.Result = amlData;
            return amlOp;
        }
        /// <summary>
        /// Method Operation 
        /// </summary>
        /// <param name="opCode">"Method"</param>
        /// <param name="inner">"Parameters string"</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefMethod(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = DefData(opCode, ref inner, ref asl);
            if (amlOp == null || amlOp.SubDataCount != 3)
            {
                DbgMessage();
            }
            // it's data
            amlOp.Type = AmlOp.OpType.UserDefined;
            amlOp.Name = amlOp.amlDatas[0].strValue;
            amlOp.iArgs = (int)amlOp.amlDatas[1].Value;
            // move to next code of inner
            // TODO: NKSNKS, possible method in method??
            return amlOp;
        }
        /// <summary>
        /// Get total count parameters
        /// </summary>
        /// <param name="amlOp">AmlOp to check </param>
        /// <returns>number of parameters</returns>
        private int GetOpArgs(AmlOp amlOp)
        {
            return amlOp.SubDataCount + amlOp.SubOpCount;
        }
        /// <summary>
        /// Get args data from a op/user define method
        /// </summary>
        /// <param name="amlOp">target operation for args to query</param>
        /// <param name="argIndex">arg index to query</param>
        /// <returns>data or null</returns>
        private AcpiData GetOpArg(AmlOp amlOp, int argIndex)
        {
            string argNum = "Arg" + argIndex.ToString();
            if (amlOp.SubDataCount > 0)
            {
                foreach (AcpiData amlData in amlOp.amlDatas)
                {
                    if (amlData.Tag == argNum)
                    {
                        return amlData;
                    }
                }
            }
            if (amlOp.SubOpCount > 0)
            {
                foreach (AmlOp op in amlOp.SubOp)
                {
                    if (op.Name == argNum)
                    {
                        return op.Result;
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// get args list based on requested count
        /// </summary>
        /// <param name="amlOp">target operation for args to query</param>
        /// <param name="ArgCount">number of args to query</param>
        /// <returns>arglist or empty arglist</returns>
        private AcpiData[] GetOpArgs(AmlOp amlOp, int ArgCount)
        {
            AcpiData[] amlDatas = new AcpiData[ArgCount];
            for (int index = 0; index < ArgCount; index++)
            {
                amlDatas[index] = GetOpArg(amlOp, index);
                if (amlDatas[index] == null)
                {
                    return null;
                }
            }
            return amlDatas;
        }
        /// <summary>
        /// Check if it's refer the local or arg data
        /// </summary>
        /// <param name="name">name of target</param>
        /// <returns>local/arg data or empty</returns>
        private AcpiData LocalOrArg(string name)
        {
            //amlData.strValue.StartsWith("Local");
            if (name == null || !LocalData.ContainsKey(name))
            {
                return null; // not local or arg return it self
            }
            return LocalData[name];
        }
        /// <summary>
        /// Query its defiend data or local/arg or itself
        /// </summary>
        /// <param name="amlData">data to query</param>
        /// <returns>referd data or itself</returns>
        private AcpiData QueryRefData(AcpiData amlData)
        {
            AcpiData refData = QueryRefData(amlData.Name);
            if (refData == null)
            {
                refData = amlData;
            }
            return refData;
        }
        /// <summary>
        /// Query its defiend data or local or arg
        /// </summary>
        /// <param name="Name">name of data to query</param>
        /// <returns>not find or data or local/arg</returns>
        private AcpiData QueryRefData(string Name)
        {
            AcpiData localArg = null;
            if (Name == null)
            {
                //DbgMessage();
                return null;
            }
            if (Name.StartsWith("InternalLocalData") || Name.StartsWith("InternalMethodArg"))
            {
                // this is refer to itself since it's local or arg data
                return null;
            }
            if (Name.StartsWith("\""))
            {
                // it's just a string
                AcpiData amlData = new AcpiData(Name);
                amlData.Type = AcpiDataType.String;
                amlData.Name = "DataString";
                return amlData;
            }

            if (amlMethodDatas.Count == 0)
            {
                localArg = LocalOrArg(Name);
                if (localArg != null)
                {
                    return localArg;
                }
            }
            else
            {
                foreach (AcpiData amlData in amlMethodDatas)
                {
                    if (amlData.Name == Name)
                    {
                        return amlData;
                    }
                }
            }
            if (localArg == null)
            {
                localArg = LocalOrArg(Name);
            }
            if (localArg == null)
            {
                // not find check if it's acpi ns data
                //
                if (acpiLib == null)
                {
                    DbgMessage();
                    return null;
                }
                string path = strRoot;
                string FullPath = Name;
                int Type;
                if ((Type = IsAcpiNS(ref FullPath)) >= 0)
                {
                    // it's a valid name and with type and fullpath
                    AcpiData amlData = new AcpiData(
                        acpiLib,
                        FullPath.Substring(0, FullPath.Length - 4),
                        FullPath.Substring(FullPath.Length - 4));
                    amlData.Name = Name;

                    if (amlData.Type != AcpiDataType.Unknown)
                    {
                        amlMethodDatas.Add(amlData);
                        return amlData;
                    }
                }
            }
            return localArg;
        }
        /// <summary>
        /// Bytes to UInt64
        /// </summary>
        /// <param name="bytes">byte arrays</param>
        /// <returns>bytes to uint64</returns>
        private UInt64 BytesToUInt64(byte[] bytes)
        {
            if (bytes == null)
            {
                return 0;
            }
            byte[] localbytes = new byte[8];
            Array.Copy(bytes, 0, localbytes, 0, bytes.Length < 8 ? bytes.Length : 8);
            return BitConverter.ToUInt64(localbytes, 0);
            //UInt64 val = 0;
            //int offset;
            //for (int idx = 0; idx < bytes.Length; idx++)
            //{
            //    if (idx >= 8)
            //    {
            //        break;
            //    }
            //    val |= ((UInt64)(bytes[idx])) << (8 * idx);
            //}
            //return val;
        }        
        /// <summary>
        /// Get local or arg if data refer to a local or arg
        /// </summary>
        /// <param name="amlData">orignal data</param>
        /// <returns>data/local/arg</returns>
        private AcpiData GetLocalArg(AcpiData amlData)
        {
            if (amlData.Tag.StartsWith("Arg") || amlData.Tag.StartsWith("Local"))
            {
                //TODO: Refer the local or arg data now.
                AcpiData args = new AcpiData();
                args.Name = amlData.Name;
                args.strValue = amlData.strValue;
                args.Type = amlData.Type;// AcpiDataType.Int;
                args.Value = 0;
                return args;
            }
            return amlData;
        }
        /// <summary>
        /// Get the args of Opcode
        /// </summary>
        /// <param name="opCode">OpCode</param>
        /// <param name="inner">inner source code</param>
        /// <param name="asl">full source code</param>
        /// <returns>AmlCode structure with args(SubOpCode/ArgData)</returns>
        private AmlOp DefData(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = new AmlOp(opCode);
            inner = inner.Trim(new char[] { '\r', '\n', '(' });
            int nEnd = inner.LastIndexOf(')');
            inner = inner.Substring(0, nEnd);
            string strWord = "";
            int ops = -1;
            int idx = 0;

            while (strWord != null)
            {
                ops = -1;
                strWord = GetNextPara(ref inner, ref ops);
                if (strWord == null)
                {
                    break;
                }
                if (ops == 2)
                {
                    AmlOp amlSubOp;
                    string opcode = GetOpCode(ref strWord);
                    if (OpDefineData.ContainsKey(opcode))
                    {
                        amlSubOp = OpDefineData[opcode].Invoke(opcode, ref strWord, ref asl);
                        if (amlSubOp != null)
                        {
                            //amlSubOp.Name = "Arg" + idx.ToString();
                            amlSubOp.Name = "Arg" + idx.ToString();
                            amlOp.AddSubOp(amlSubOp);
                        }
                    }
                    else
                    {
                        // NKSNKS, check possiblity of user defined code
                        string MethodPath = "";
                        int Args = QueryUserDefinedMethod(opcode, ref MethodPath);
                        if (Args == -1)
                        {
                            DbgMessage();
                        } else
                        {
                            amlSubOp = DefUserMethod(Args, MethodPath, ref strWord, ref asl);
                            if (amlSubOp != null)
                            {
                                amlSubOp.Name = "Arg" + idx.ToString();
                                amlOp.AddSubOp(amlSubOp);
                            }
                        }
                    }
                }
                else
                {
                    AcpiData amlData = new AcpiData();
                    UInt64 uVal = 0;
                    if (IsIntString(strWord, ref uVal))
                    {
                        amlData.Tag = "Arg" + idx.ToString();
                        amlData.Type = AcpiDataType.Int;
                        amlData.Value = uVal;
                        amlData.Name = "Constant";  // contant value
                        
                    }
                    else
                    {
                        amlData.Type = AcpiDataType.Unknown;
                        if (strWord.Contains("\"")) 
                        {
                            amlData.Name = "Constant";  // contant string
                            amlData.Type = AcpiDataType.String;
                        }
                        else
                        {
                            amlData.Name = strWord;
                        }
                        amlData.Tag = "Arg" + idx.ToString();
                        //amlData = GetLocalArg(amlData);
                        amlData.Tag = "Arg" + idx.ToString();                        
                        amlData.strValue = strWord;                        
                    }
                    amlOp.AddSubData(amlData);
                }
                idx++;
            }
            return amlOp;
        }
        /// <summary>
        /// Simple opcode with args
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="inner"></param>
        /// <param name="asl"></param>
        /// <returns>AmlCode structure</returns>
        private AmlOp DefOpSimple(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = new AmlOp(opCode);

            return amlOp;
        }
        /// <summary>
        /// No Data Opcode Handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="inner"></param>
        /// <param name="asl"></param>
        /// <returns>AmlCode structure</returns>
        private AmlOp DefNoData(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = new AmlOp(opCode);
            DbgMessage();
            return amlOp;
        }
        /// <summary>
        /// Run the single line op
        /// </summary>
        /// <param name="method">line of code</param>
        /// <param name="asl">full asl code</param>
        /// <returns>amloperation struct</returns>
        public AmlOp SingleOpCode(ref string method, ref string asl)
        {
            // how to check if it's a single operation code and expanded 
            AmlOp amlOp = null;
            string cCode = "";
            int nScope = ScopeSize(method, '(', ')');
            if (nScope == -1)
            {
                string inner = method;
                if (OpDefineData.ContainsKey(inner))
                {
                    cCode = inner;
                    amlOp = OpDefineData[inner].Invoke(inner, ref inner, ref asl);
                    if (amlOp != null && amlOp.Type == AmlOp.OpType.Condition)
                    {
                        amlOp.Code = method;
                    }
                }
                else
                {
                    // NKSNKS, check possiblity of user defined code
                    string MethodPath = "";
                    int Args = QueryUserDefinedMethod(inner, ref MethodPath);
                    if (Args == -1)
                    {
                        DbgMessage();
                    }
                    else
                    {
                        amlOp = DefUserMethod(Args, MethodPath, ref inner, ref asl);                        
                    }
                }
            }
            else
            {
                string inner = method.Substring(0, nScope);
                string code = inner;
                int nOps = inner.Count(f => f == '(');
                string opCode = GetOpCode(ref inner);

                if (OpDefineData.ContainsKey(opCode))
                {
                    cCode = inner;
                    amlOp = OpDefineData[opCode].Invoke(opCode, ref inner, ref asl);
                    if (amlOp != null && amlOp.Type == AmlOp.OpType.Condition)
                    {
                        amlOp.Code = code;
                    }
                }
                else
                {
                    // NKSNKS, check possiblity of user defined code
                    string MethodPath = "";
                    int Args = QueryUserDefinedMethod(opCode, ref MethodPath);
                    if (Args == -1)
                    {
                        DbgMessage();
                    }
                    else
                    {
                        amlOp = DefUserMethod(Args, MethodPath, ref inner, ref asl);                        
                    }
                }
            }
            if (amlOp != null)
            {
                amlOp.ConditionCode = cCode;
            }
            return amlOp;
        }
        /// <summary>
        /// Get the asl code size of Brackets/Braces pair 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="opench">pair start char</param>
        /// <param name="closech">pair clsoe char</param>
        /// <returns>-1 for failure, or code size</returns>
        private int ScopeSize(string text, char opench, char closech)
        {
            int iClose = text.IndexOf(closech);
            if (iClose == -1)
            {
                // no close
                return -1;
            }
            string inner_text = text.Substring(0, iClose + 1);
            string outer_text = text.Substring(iClose + 1);

            while (true)
            {
                int nOpenCh = inner_text.Count(f => f == opench);
                int nCloseCh = inner_text.Count(f => f == closech);
                if (nOpenCh == nCloseCh)
                {
                    break;
                }
                int iNextClose = outer_text.IndexOf(closech);
                if (iNextClose != -1)
                {
                    iClose += iNextClose + 1;
                }
                else
                {
                    DbgMessage();
                }
                inner_text = text.Substring(0, iClose + 1);
                outer_text = text.Substring(iClose + 1);
            }

            return iClose + 1;
        }

        /// <summary>
        /// Clean up the string for simple pass
        /// </summary>
        /// <param name="inner">inner string to clean</param>
        private void CleanAslInner(ref string inner)
        {
            if (inner == null)
            {
                return;
            }
            inner = inner.Replace("\r", "");
            inner = inner.Replace("\n", "");
            inner = inner.Replace(" ", "");
        }
        /// <summary>
        /// Get the op code from line
        /// </summary>
        /// <param name="line"> string line to get the opCode</param>
        /// <returns>valid opCode or "" when it does not contain any valid string</returns>
        private string GetOpCode(ref string line)
        {
            string opCode = null;
            int nOpStart = line.IndexOf('(');
            int nLineEnd = line.IndexOf('\r');
            if (nLineEnd == -1)
            {
                line.IndexOf('\n');
            }
            if (nOpStart == -1 && nLineEnd == -1)
            {
                // incorrect asl code
                DbgMessage();
            }
            else if (nOpStart > nLineEnd && nLineEnd != -1)
            {
                // a non parameter operation
                opCode = line.Substring(0, nLineEnd).Trim();
                line = line.Substring(nLineEnd);
            }
            else
            {
                opCode = line.Substring(0, nOpStart).Trim();
                line = line.Substring(nOpStart);
            }
            return opCode;
        }
        /// <summary>
        /// Is it a Acpi Int
        /// </summary>
        /// <param name="strValue">string represent a int or not</param>
        /// <param name="iValue">value of string represent</param>
        /// <returns>TRUE/FALSE indicate an int or not</returns>
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
            catch (Exception e)
            {
                Log.Logs(e.Message);
                return false;
            }
        }
        /// <summary>
        /// create field op
        /// </summary>
        /// <param name="opCode">CreateXXXXField</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp CreateFieldGeneric(string opCode, ref string inner, UInt64 nSize)
        {
            string asl = "";
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || GetOpArgs(amlOp) != 3)
            {
                DbgMessage();
            }
            else
            {
                // it's data reference definition
                amlOp.Type = AmlOp.OpType.Definition;
                //AcpiData[] args = GetOpArgs(amlOp, 3);
                //if (args == null)
                //{
                //    DbgMessage();
                //}
                //// arg 0 is the refer data
                //AcpiData referData = QueryRefData(args[0]);
                //// refer must be a buffer
                //if (referData == null || referData.Type != AcpiDataType.Buffer)
                //{
                //    DbgMessage();
                //}
                // offset must be integer
                if (amlOp.Args[1].Type != AcpiDataType.Int)
                {
                    DbgMessage();
                }
                if (nSize <= 64)
                {
                    // convert to a UInt64
                    amlOp.Result.Type = AcpiDataType.Int;
                }
                else
                {
                    // buffer type since width is bigger then UInt64
                    amlOp.Result.Type = AcpiDataType.Buffer;
                }
                amlOp.Result.Name = amlOp.Args[2].Name;
                //amlOp.Result.strRefName = args[0].strValue;
                //amlOp.Result.RefStart = (args[1].Value) * 8;   
                //amlOp.Result.RefWidth = nSize;
                amlOp.Result.bpValue = amlOp.Args[0].bpValue;
                // get the values for integer
                // referData.bpValue.Select ()
                byte[] array = new byte[nSize / 8];
                Array.Copy(amlOp.Result.bpValue, (int)amlOp.Args[1].Value, array, 0, (int)nSize / 8);
                amlOp.Result.Value = BytesToUInt64(array);
                amlMethodDatas.Add(amlOp.Result);
            }
            return amlOp;
        }
        //// <summary>
        /// createfield op
        /// </summary>
        /// <param name="opCode">createfield</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp CreateField(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || GetOpArgs(amlOp) != 4)
            {
                DbgMessage();
            }
            else
            {
                // offset must be integer
                if (amlOp.Args[1].Type != AcpiDataType.Int ||
                    amlOp.Args[2].Type != AcpiDataType.Int)
                {
                    DbgMessage();
                }
                amlOp.Result.Type = AcpiDataType.Buffer;
                amlOp.Result.Name = amlOp.Args[3].strValue;
            }
            return amlOp;
        }
        /// <summary>
        /// CreateBitField op
        /// </summary>
        /// <param name="opCode">CreateBitField</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp CreateBitField(string opCode, ref string inner, ref string asl)
        {
            return CreateFieldGeneric(opCode, ref inner, 1);
        }
        /// <summary>
        /// CreateByteField op
        /// </summary>
        /// <param name="opCode">CreateByteField</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp CreateByteField(string opCode, ref string inner, ref string asl)
        {
            return CreateFieldGeneric(opCode, ref inner, 8);
        }
        /// <summary>
        /// CreateWordField op
        /// </summary>
        /// <param name="opCode">CreateWordField</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp CreateWordField(string opCode, ref string inner, ref string asl)
        {
            return CreateFieldGeneric(opCode, ref inner, 16);
        }
        /// <summary>
        /// CreateDWordField op
        /// </summary>
        /// <param name="opCode">CreateDWordField</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp CreateDWordField(string opCode, ref string inner, ref string asl)
        {
            return CreateFieldGeneric(opCode, ref inner, 32);
        }
        /// <summary>
        /// CreateQWordField op
        /// </summary>
        /// <param name="opCode">CreateQWordField</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp CreateQWordField(string opCode, ref string inner, ref string asl)
        {
            return CreateFieldGeneric(opCode, ref inner, 64);
        }
        /// <summary>
        /// Name OpCOde handler
        /// </summary>
        /// <param name="opCode">OpCode</param>
        /// <param name="inner">inner source code</param>
        /// <param name="asl">full source code</param>
        /// <returns>AmlCode structure with args(SubOpCode/ArgData)</returns>
        private AmlOp DefName(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            // it's data
            amlOp.Type = AmlOp.OpType.Definition;
            amlOp.Name = amlOp.Args[0].Name;
            amlOp.Result.Assign(amlOp.Args[1]);
            amlOp.Result.Name = amlOp.Name;
            amlMethodDatas.Add(amlOp.Result);
            return amlOp;
        }
        /// <summary>
        /// Alias op
        /// </summary>
        /// <param name="opCode">Alias</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefAlias(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                // first data is the alias name, second is a alias                
                amlOp.Args[0].SetAlias(amlOp.Args[1]);
                amlMethodDatas.Add(amlOp.Args[0]);
                amlOp.Type = AmlOp.OpType.Definition;
            }
            return amlOp;
        }
        /// <summary>
        /// Mutex op
        /// </summary>
        /// <param name="opCode">Mutex</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefMutex(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                // first data is the alias name, second is a alias
                amlOp.Args[0].Type = AcpiDataType.Mutex;
                amlOp.Args[0].Value = amlOp.Args[1].Value;
                amlMethodDatas.Add(amlOp.Args[0]);
            }
            return amlOp;
        }
        /// <summary>
        /// Timer op
        /// </summary>
        /// <param name="opCode">Timer</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefTimer(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            // first data is the alias name, second is a alias
            else
            {
                amlOp.Args[0].Type = AcpiDataType.Timer;
                amlMethodDatas.Add(amlOp.Args[0]);
                amlOp.Type = AmlOp.OpType.Definition;
            }
            return amlOp;
        }
        /// <summary>
        /// Event op
        /// </summary>
        /// <param name="opCode">Event</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefEvent(string opCode, ref string inner, ref string asl)
        {
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                // first data is the alias name, second is a alias
                amlOp.Args[0].Type = AcpiDataType.Event;
                amlOp.Args[0].Value = 0;
                amlMethodDatas.Add(amlOp.Args[0]);
                amlOp.Type = AmlOp.OpType.Definition;
            }
            return amlOp;
        }
        /// <summary>
        /// Field op
        /// </summary>
        /// <param name="regionData">Fieldunit list</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>List unit field</returns>
        private List<AcpiFieldUnit> DefOpFieldList(ref AcpiField field, ref string asl)
        {
            //List<AcpiFieldUnit> Fields = new List<AcpiFieldUnit>();
            if (field.fieldUnits == null)
            {
                field.fieldUnits= new List<AcpiFieldUnit>();
            }
            // get inner test...
            string strFieldOp = GetSingleOpString(ref asl);
            string opCode = GetOpCode(ref strFieldOp);
            AmlOp amlOp = DefAllData(opCode, ref strFieldOp, ref asl);

            if (amlOp == null || GetOpArgs(amlOp) != 4)
            {
                DbgMessage();
            }
            else
            {
                // get the operations type
                if (amlOp.Args[0].strValue != field.Name)
                {
                    DbgMessage();
                }
                field.SetType(amlOp.Args[1].strValue);
                // now do field collections///
                asl = asl.Substring(asl.IndexOf('{') + 1);
                asl = asl.Substring(0, asl.IndexOf('}'));
                asl = asl.Trim(new char[] { '\r', '\n' });
                UInt64 Offset = 0;
                while (asl.Length > 0)
                {
                    // collect the field
                    if (asl.IndexOf(',') != -1)
                    {
                        string Name = asl.Substring(0, asl.IndexOf(','));
                        Name = Name.Trim(new char[] { '\r', '\n', ' ' });
                        asl = asl.Substring(asl.IndexOf(',') + 1);
                        if (!Name.StartsWith("Offset"))
                        {
                            string Width;
                            if (asl.IndexOf(',') == -1)
                            {
                                // the last
                                Width = asl.Trim(new char[] { '\r', '\n', ' ' });
                                asl = "";
                            }
                            else
                            {
                                Width = asl.Substring(0, asl.IndexOf(','));
                                asl = asl.Substring(asl.IndexOf(',') + 1);
                            }
                            Width = Width.Trim(new char[] { '\r', '\n', ' ' });
                            AcpiFieldUnit fieldData = new AcpiFieldUnit();
                            fieldData.Width = 0;
                            if (!IsIntString(Width, ref fieldData.Width))
                            {
                                DbgMessage();
                            }
                            fieldData.Offset = Offset;
                            Offset += fieldData.Width;
                            field.fieldUnits.Add(fieldData);
                            fieldData.Name = Name;
                            if (Name.Length > 0)    // not a empty
                            {                                
                                AcpiData fieldUnit = new AcpiData();
                                fieldUnit.Name = Name;
                                fieldUnit.Type = AcpiDataType.FieldUnit;
                                fieldUnit.Field = field;    // refer to the field                                
                                fieldUnit.Value = 0;
                                amlMethodDatas.Add(fieldUnit);
                            }
                        }
                        else
                        {
                            //
                            // get the offset, get the byte value of the data and get the code
                            //
                            if (Name.IndexOf('(') == -1 || Name.LastIndexOf(')') == -1)
                            {
                                DbgMessage();
                            }
                            string strOff = Name.Substring(Name.IndexOf('(') + 1);
                            strOff = strOff.Substring(0, strOff.LastIndexOf(')'));
                            UInt64 byte_offset = 0;
                            if (!IsIntString(strOff, ref byte_offset))
                            {
                                DbgMessage();
                            }
                            Offset = byte_offset * 8;
                        }
                    }
                }
            }
            return null;
            //return regionData.OpRegion.Fields;
        }
        /// <summary>
        /// DataRegion op
        /// </summary>
        /// <param name="opCode">DataRegion</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefDataRegion(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || GetOpArgs(amlOp) != 4)
            {
                DbgMessage();
            }
            // TODO:
            return amlOp;
        }
        /// <summary>
        /// OpRegion op
        /// </summary>
        /// <param name="opCode">OpRegion</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefOpRegion(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null || GetOpArgs(amlOp) != 4)
            {
                DbgMessage();
            }
            else
            {
                // TODO: NKSNKS Add OperationRegion data definition, internal of method
                // first data is the alias name, second is a alias
                amlOp.Args[0].Type = AcpiDataType.OpRegion;
                amlOp.Args[0].Value = 0;
                //amlMethodDatas.Add(amlOp.Args[0]);
                amlOp.Type = AmlOp.OpType.Definition;
                // get the followring code
                amlOp.Result.Type = AcpiDataType.OpRegion;
                amlOp.Result.Name = amlOp.Args[0].strValue;
                AcpiOpRegion OpRegion = new AcpiOpRegion(amlOp.Args[1].Name);
                OpRegion.Name = amlOp.Args[0].strValue;
                OpRegion.Address = amlOp.Args[2].Value;
                OpRegion.Width = amlOp.Args[3].Value;
                amlOp.Result.Field = new AcpiField(OpRegion);
                        
                if (asl.StartsWith("Field"))
                {
                    int nScope = ScopeSize(asl, '{', '}');
                    if (nScope > 0)
                    {
                        string strFields = asl.Substring(0, nScope);
                        asl = asl.Substring(nScope);
                        DefOpFieldList(ref amlOp.Result.Field, ref strFields);
                    }
                }
                //if (amlOp.Result.OpRegion.Fields.Count == 0)
                //{
                //    // it's ok there is no Field
                //    //DbgMessage();
                //}
                //amlMethodDatas.Add(amlOp.Result);
            }
            return amlOp;
        }
        /// <summary>
        /// Break op
        /// </summary>
        /// <param name="opCode">Break</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefBreak(string opCode, ref string inner, ref string asl)
        {
            // must in a scope of If or while excution, need to stop the excution
            AmlOp amlOp = new AmlOp(opCode);
            return amlOp;
        }
        /// <summary>
        /// Continue op
        /// </summary>
        /// <param name="opCode">Continue</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefContinue(string opCode, ref string inner, ref string asl)
        {
            // must in a scope of If or while excution, need to stop the excution
            AmlOp amlOp = new AmlOp(opCode);
            return amlOp;
        }
        /// <summary>
        /// Fatal op
        /// </summary>
        /// <param name="opCode">Fatal</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefFatal(string opCode, ref string inner, ref string asl)
        {
            // must in a scope of If or while excution, need to stop the excution
            AmlOp amlOp = new AmlOp(opCode);
            DbgMessage("A Fata error throw by acpi code");
            return amlOp;
        }
        /// <summary>
        /// Sleep/Stall op
        /// </summary>
        /// <param name="opCode">Sleep/Stall</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefSleep(string opCode, ref string inner, ref string asl)
        {
            // sleep and stall no different for simulation
            // stall keep CPU busy and sleep can do something else
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                if (amlOp.Args[0].Type != AcpiDataType.Int)
                {
                    DbgMessage();
                }
                else
                {
                    Thread.Sleep((int)amlOp.Args[0].Value);
                }
            }
            return amlOp;
        }

        /// <summary>
        /// Notify op
        /// </summary>
        /// <param name="opCode">Notify</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefNotify(string opCode, ref string inner, ref string asl)
        {
            // sleep and stall no different for simulation
            // stall keep CPU busy and sleep can do something else
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Args[0].strValue = ToNameSeg(amlOp.Args[0].Name);
                if (amlOp.Args[1].Type != AcpiDataType.Int)
                {
                    DbgMessage();
                }
                else
                {
                    if (acpiLib != null)
                    {
                        // do the notification, only valid when acpi driver is loaded on targt system
                        acpiLib.Notify(amlOp.Args[0].strValue, amlOp.Args[1].Value);
                    }
                }
            }
            return amlOp;
        }
        /// <summary>
        /// DefRefOf op
        /// </summary>
        /// <param name="opCode">DefRefOf</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefRefOf(string opCode, ref string inner, ref string asl)
        {
            // sleep and stall no different for simulation
            // stall keep CPU busy and sleep can do something else
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                // if DerefOf create the copy data
                // if RefOfcreate a data and only evulate them when the data is start to access
                // no need differ them for debug simulation
                amlOp.Result.Assign(amlOp.Args[0]);
            }
            return amlOp;
        }
        /// <summary>
        /// CondRef op
        /// </summary>
        /// <param name="opCode">CondRef</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefCondRef(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Args[0].Name = ToNameSeg(amlOp.Args[0].Name);
                amlOp.Type = AmlOp.OpType.Condition;
                if (acpiLib != null)
                {
                    // check if the path is valid
                    amlOp.Result.Type = AcpiDataType.Int;
                    if (amlOp.Args[0].Name.Equals("\\____OSI"))
                    {
                        if (acpiLib.DriverLoaded())
                        {
                            amlOp.Result.Value = 1;
                        }
                        else
                        {
                            amlOp.Result.Value = 0;
                        }
                    }
                    else
                    {
                        if (acpiLib.GetType(strRoot + amlOp.Args[0].Name) == -1)
                        {
                            amlOp.Result.Value = 0;
                        }
                        else
                        {
                            amlOp.Result.Value = 1;
                        }
                    }
                }
            }
            return amlOp;
        }
        /// <summary>
        /// Load op
        /// </summary>
        /// <param name="opCode">Load</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefLoad(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def2Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            // Dynamically load ssdt not valid for run time debug, and do nothing
            return amlOp;
        }
        /// <summary>
        /// LoadTable op
        /// </summary>
        /// <param name="opCode">LoadTable</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefLoadTable(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            DbgMessage();
            // Dynamically load ssdt not valid for run time debug, and do nothing
            return amlOp;
        }
        /// <summary>
        /// Return op
        /// </summary>
        /// <param name="opCode">Return</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefReturn(string opCode, ref string inner, ref string asl)
        {
            // sleep and stall no different for simulation
            // stall keep CPU busy and sleep can do something else
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                amlOp.Result.Assign(amlOp.Args[0]);                
                Result = amlOp.Result;
                Result.Name = "Return";
                strMethodCode = "";
            }
            return amlOp;
        }
        /// <summary>
        /// BreakPoint op
        /// </summary>
        /// <param name="opCode">BreakPoint</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefBreakPoint(string opCode, ref string inner, ref string asl)
        {
            // must in a scope of If or while excution, need to stop the excution
            AmlOp amlOp = new AmlOp(opCode);
            // TODO: NKSNKS Stop the code for debug for acpi debug, for this nothing to do

            return amlOp;
        }
        /// <summary>
        /// Acquire/Release op
        /// </summary>
        /// <param name="opCode">Acquire/Release</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefMutexOp(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = DefAllData(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                if (amlOp.Args[0].Type != AcpiDataType.Mutex)
                {
                    DbgMessage();
                }
                if (opCode == "Acquire")
                {
                    // must be zero
                    if (amlOp.Args[0].Value != 0)
                    {
                        DbgMessage("Mutext already acquried without release");
                    }
                    else
                    {
                        amlOp.Args[0].Value += 1;
                    }
                }
                else if (opCode == "Release")
                {
                    if (amlOp.Args[0].Value == 0)
                    {
                        DbgMessage("Mutext already released without acquire");
                    }
                    else
                    {
                        amlOp.Args[0].Value -= 1;
                    }
                }
            }
            amlOp.Result.Assign(amlOp.Args[0]);
            return amlOp;
        }
        /// <summary>
        /// Wait/Reset op
        /// </summary>
        /// <param name="opCode">Wait/Reset</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefEventOp(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                if (amlOp.Args[0].Type != AcpiDataType.Event)
                {
                    DbgMessage();
                }
                else
                {
                    if (opCode == "Wait")
                    {
                        // must be zero
                        amlOp.Args[0].Value = 1;
                    }
                    else
                    {
                        // else if it's a reset or signal make the value to be 0 to let wait to continue
                        amlOp.Args[0].Value = 0;
                    }
                }
            }
            amlOp.Result.Assign(amlOp.Args[0]);
            return amlOp;
        }
        /// <summary>
        /// Increment/Decrement op
        /// </summary>
        /// <param name="opCode">Increment/Decrement</param>
        /// <param name="inner">Inner Parameter for OpCode</param>
        /// <param name="asl">For certain op is used to move to next single code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefIncrement(string opCode, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = Def1Data(opCode, ref inner, ref asl);
            if (amlOp == null)
            {
                DbgMessage();
            }
            else
            {
                if (opCode == "Increment")
                {
                    if (amlOp.Args[0].Type == AcpiDataType.Int)
                    {
                        amlOp.Args[0].Value += 1;
                    }
                }
                else if (opCode == "Decrement")
                {
                    if (amlOp.Args[0].Type == AcpiDataType.Int)
                    {
                        amlOp.Args[0].Value -= 1;
                    }
                }
            }
            amlOp.Result.Assign(amlOp.Args[0]);
            return amlOp;
        }
        /// <summary>
        /// Initialize the OpMap
        /// </summary>
        private void InitOpDefineData()
        {
            //if (OpDefineData.Count == 0)
            {
                OpDefineData["BankField"] = DefData;
                // 
                OpDefineData["Name"] = DefName;
                OpDefineData["Alias"] = DefAlias;
                OpDefineData["Mutex"] = DefMutex;
                OpDefineData["Event"] = DefEvent;
                OpDefineData["CreateBitField"] = CreateBitField;
                OpDefineData["CreateByteField"] = CreateByteField;
                OpDefineData["CreateDWordField"] = CreateDWordField;
                OpDefineData["CreateField"] = CreateField;
                OpDefineData["CreateQWordField"] = CreateQWordField;
                OpDefineData["CreateWordField"] = CreateWordField;
                OpDefineData["DataRegion"] = DefDataRegion;
                OpDefineData["OperationRegion"] = DefOpRegion;
                OpDefineData["Method"] = DefMethod;
                // Type 1 Opcodes                
                OpDefineData["Break"] = DefBreak;
                OpDefineData["BreakPoint"] = DefBreakPoint;
                OpDefineData["Continue"] = DefContinue;
                OpDefineData["Fatal"] = DefFatal;
                OpDefineData["If"] = DefIf;
                OpDefineData["Else"] = DefOpSimple;
                OpDefineData["Load"] = DefLoad;
                OpDefineData["Noop"] = DefOpSimple;
                OpDefineData["Notify"] = DefNotify;
                OpDefineData["Return"] = DefReturn;
                OpDefineData["Release"] = DefMutexOp;
                OpDefineData["Reset"] = DefEventOp;
                OpDefineData["Signal"] = DefEventOp;
                OpDefineData["Sleep"] = DefSleep;
                OpDefineData["Stall"] = DefSleep;
                OpDefineData["While"] = DefIf;
                // Type 2 Opcodes
                OpDefineData["Acquire"] = DefMutexOp;
                OpDefineData["Add"] = DefAdd;
                OpDefineData["And"] = DefOp;
                OpDefineData["Buffer"] = DefBufData;
                OpDefineData["Concat"] = DefConcat;
                OpDefineData["ConcatRes"] = DefConcat;
                OpDefineData["CondRefOf"] = DefCondRef;
                OpDefineData["CopyObject"] = DefCopy;
                OpDefineData["Decrement"] = DefIncrement;
                OpDefineData["DerefOf"] = DefRefOf;
                OpDefineData["Divide"] = DefDivide;
                OpDefineData["FindSetLeftBit"] = DefFindBit;
                OpDefineData["FindSetRightBit"] = DefFindBit;
                OpDefineData["FromBCD"] = DefFromBCD;
                OpDefineData["Increment"] = DefIncrement;
                OpDefineData["Index"] = DefIndex;
                OpDefineData["LAnd"] = DefLOp;
                OpDefineData["LEqual"] = DefLOp;
                OpDefineData["LGreater"] = DefLOp;
                OpDefineData["LGreaterEqual"] = DefLOp;
                OpDefineData["LLess"] = DefLOp;
                OpDefineData["LLessEqual"] = DefLOp;
                OpDefineData["LNot"] = DefLNot;
                OpDefineData["LNotEqual"] = DefLOp;
                OpDefineData["LoadTable"] = DefLoadTable;
                OpDefineData["LOr"] = DefLOp;
                OpDefineData["Match"] = DefNoData;  // TODO: Matching
                OpDefineData["Mid"] = DefMid;
                OpDefineData["Mod"] = DefMod;
                OpDefineData["Multiply"] = DefMultiple;
                OpDefineData["NAnd"] = DefOp;
                OpDefineData["_OSI"] = DefOSI;
                OpDefineData["OSI"] = DefOSI;
                OpDefineData["NOr"] = DefOp;
                OpDefineData["Not"] = DefNotOp;
                OpDefineData["ObjectType"] = DefObjectType;
                OpDefineData["Or"] = DefOp;
                OpDefineData["Package"] = DefPackge;
                OpDefineData["VarPackage"] = DefPackge; // TODO: VarPackage
                OpDefineData["RefOf"] = DefRefOf;
                OpDefineData["ShiftLeft"] = DefShift;
                OpDefineData["ShiftRight"] = DefShift;
                OpDefineData["SizeOf"] = DefSizeOf;
                OpDefineData["Store"] = DefStore;
                OpDefineData["Subtract"] = DefSubtract;
                OpDefineData["Timer"] = DefTimer;
                OpDefineData["ToBCD"] = DefToBcd;
                OpDefineData["ToBuffer"] = DefToBuffer;
                OpDefineData["ToDecimalString"] = DefToDecString;
                OpDefineData["ToHexString"] = DefToHexString;
                OpDefineData["ToInteger"] = DefToInteger;
                OpDefineData["ToString"] = DefToString;
                OpDefineData["Wait"] = DefEventOp;
                OpDefineData["Xor"] = DefOp;
                //
                OpDefineData["Debug"] = DefBreakPoint;
            }
            for (int index = 0; index < 8; index++)
            {
                Local[index] = new AcpiData();
                Local[index].Name = "InternalLocalData" + index.ToString();
                MethodArg[index] = new AcpiData();
                MethodArg[index].Name = "InternalMethodArg" + index.ToString();
            }
            //  if (LocalData.Count == 0)
            {
                LocalData["Local0"] = Local[0];
                LocalData["Local1"] = Local[1];
                LocalData["Local2"] = Local[2];
                LocalData["Local3"] = Local[3];
                LocalData["Local4"] = Local[4];
                LocalData["Local5"] = Local[5];
                LocalData["Local6"] = Local[6];
                LocalData["Local7"] = Local[7];
                LocalData["Arg0"] = MethodArg[0];
                LocalData["Arg1"] = MethodArg[1];
                LocalData["Arg2"] = MethodArg[2];
                LocalData["Arg3"] = MethodArg[3];
                LocalData["Arg4"] = MethodArg[4];
                LocalData["Arg5"] = MethodArg[5];
                LocalData["Arg6"] = MethodArg[6];
                LocalData["Arg7"] = MethodArg[7];
            }
        }
        /// <summary>
        /// Query user method
        /// </summary>
        /// <param name="strName">name to search under curernt root and uplevel</param>
        /// <param name="MethodPath">acpi ns full path to receive the method full path</param>
        /// <returns>usermethod arg count or -1 indicate not a method</returns>
        private int QueryUserDefinedMethod(string strName, ref string MethodPath)
        {
            if (acpiLib == null)
            {
                return -1;
            }
            string path = strRoot;
            string refName = strName;
            int Type = IsAcpiNS(ref refName);
            if (Type == 8)
            {
                UInt64 args = 0;
                if (acpiLib.GetMethodArgCount(refName, ref args))
                {
                    MethodPath = refName;
                    return (int)args;
                }
            }
            return -1;
        }
        /// <summary>
        /// Args array to args list
        /// </summary>
        /// <param name="amlOp">amlop soruce</param>
        /// <returns>args list</returns>
        private List<AcpiData> ToArgLsit(AmlOp amlOp)
        {
            List<AcpiData> args = new List<AcpiData>();
            if (amlOp.Args != null && amlOp.Args.Length > 0)
            {
                foreach (AcpiData data in amlOp.Args)
                {
                    args.Add(data);
                }
            }                    
            return args;
        }
        /// <summary>
        /// Run used defined method
        /// </summary>
        /// <param name="Args">number of args of method</param>
        /// <param name="MethodPath">full acpins path</param>
        /// <param name="inner">parameters string</param>
        /// <param name="asl">remained asl code</param>
        /// <returns>Op struct with result and conditions</returns>
        private AmlOp DefUserMethod(int Args, string MethodPath, ref string inner, ref string asl)
        {
            asl = asl.TrimStart(new char[] { '\r', '\n' });
            AmlOp amlOp = DefAllData(MethodPath, ref inner, ref asl);
            if (amlOp == null || GetOpArgs(amlOp) != Args)
            {
                DbgMessage();
            }
            // run the command
            // args to args........
            UInt64 args = 0;
            if (acpiLib.GetMethodArgCount(MethodPath, ref args))
            {
                if (args == (UInt64)GetOpArgs(amlOp))
                {
                    DebugViewForm debugViewForm = new DebugViewForm();
                    debugViewForm.acpiLib = acpiLib;
                    //debugViewForm.MethodArgs = method_data;
                    debugViewForm.strAcpiPath = MethodPath;
                    debugViewForm.strAslCode = "";
                    debugViewForm.MethodArgs = ToArgLsit(amlOp);
                    if (acpiLib.GetAslCode(MethodPath, ref debugViewForm.strAslCode))
                    {
                        if (acpiLib.DriverLoaded())
                        {
                            // run the command on acpi driver enabled system
                            string result = "";
                            if (GetOpArgs(amlOp) == 0)
                            {                                
                                if (!acpiLib.GetEvalResult(MethodPath, ref result))
                                {
                                    // parse the result
                                    result = null;
                                }
                            } else
                            {
                                IntPtr pArgs = IntPtr.Zero;
                                foreach (AcpiData arg in amlOp.Args)
                                {
                                    if (result == null)
                                    {
                                        break;
                                    }
                                    switch (arg.Type)
                                    {
                                        case AcpiDataType.Int:
                                            pArgs = acpiLib.ArgPutUInt64(pArgs, arg.Value);
                                            break;
                                        case AcpiDataType.String:
                                            pArgs = acpiLib.ArgPutString(pArgs, arg.strValue);
                                            break;
                                        case AcpiDataType.Buffer:
                                            pArgs = acpiLib.ArgPutBuffer(pArgs, arg.bpValue);
                                            break;
                                        default:
                                            //TODO: Add package support;
                                            DbgMessage("Unsupported data ");
                                            result = null;
                                            break;
                                    }
                                }
                                if (result != null)
                                {
                                    //if (!acpiLib.GetEvalArgResult(MethodPath, pArgs, ref result))
                                    //{
                                    //    // parse the result
                                    //    result = null;
                                    //}
                                    IntPtr output = acpiLib.GetEvalArgOutput(MethodPath, pArgs);
                                    if (output != IntPtr.Zero)
                                    {
                                        AcpiData acpiData = new AcpiData();
                                        acpiData.FromAcpiOutput(output);
                                        acpiLib.FreeArg(output);
                                        amlOp.Result.Assign(acpiData);
                                    }
                                }
                                if (pArgs != IntPtr.Zero)
                                {
                                    acpiLib.FreeArg(pArgs);
                                }
                                //if (result != null)
                                //{
                                //    // get the result know
                                //    DbgMessage("Wait for....");
                                //}
                            }
                            
                        }
                        else
                        {
                            // go to the code and run the command... more complexity... how, or run all the code???
                            if (true)
                            {
                                AmlMethod amlMethod = new AmlMethod(
                                            acpiLib,
                                            MethodPath);
                                AmlOp opResult = amlMethod.RunAslMethod(
                                    debugViewForm.strAslCode,
                                    debugViewForm.MethodArgs);
                                if (opResult.OpCode == "Return")
                                {
                                    amlOp.Result = opResult.Result;
                                }
                            }
                            else
                            {
                                //if (SingleStep) 
                                debugViewForm.ShowDialog();
                                if (debugViewForm.DialogResult != System.Windows.Forms.DialogResult.OK)
                                {
                                    //
                                    DbgMessage("Sub Code not finished, must be a error will cause");
                                    bError = true;
                                }
                                // method has 
                                else
                                {
                                    amlOp.Result = debugViewForm.Result;
                                }
                            }
                        }                        
                    }
                }
            }           
            return amlOp;
        }
        /// <summary>
        /// Debug message
        /// </summary>
        /// <param name="memberName"></param>
        /// <param name="sourceFilePath"></param>
        /// <param name="sourceLineNumber"></param>
        private void DbgMessage(
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            bError = true;
            System.Diagnostics.Debug.Fail(
                string.Format("Error {0} Line {1}",
                sourceFilePath,
                sourceLineNumber));
        }
       
        /// <summary>
        /// Debug/Test purpose only
        /// </summary>
        public void RunMethod()
        {
            //RunMethodDebug ();
            //AcpiData acpiData = new AcpiData(1);
            //acpiData.acpiLib = this.acpiLib;
            //acpiData.QueryData("\\___", "M045");
            RunMethodDebug();
        }
        /// <summary>
        /// Run asl method
        /// </summary>
        /// <param name="asl">asl code</param>
        /// <param name="Args">args for asl code</param>
        /// <returns>OpCode for last op after run method</returns>
        public AmlOp RunAslMethod (string asl, List<AcpiData> Args)
        {
            AmlOp op;
            PrepareMethodRun(asl, Args);
            while (true)
            {
                op = RunStep();
                if (op == null)
                {
                    break;
                }
                string Code = CurrentCode();
                if (Code.Length < 2)
                {
                    break;
                }
                Code = NextCode();
            }
            return op;
        }
        public string testMethod1 = "" +
            "Method (SLEN, 1, NotSerizlized)\n{\n Store (\"String\", Local0)\nAdd (1, SizeOf(Local0), Local0)\nName(BUFF, Buffer(Local0){ })\nStore(Arg0, BUFF)\nReturn(BUFF)\n}\r\n";
        private string testMethod =
   @"
Method (MLIB, 2, Serialized)
{    
    Name(BNST, 2)
    Store(BNST, Local0)
    And(Local0, One, Local1)
    And(Local0, 0x02, Local2)
    If(LEqual(Local0, 0x2))
    {
        Notify(\_SB_.VMOD.BAT1, 0x81)
        Notify(\_SB_.VMOD.AC1_, 0x80)
    } Else {
        If (LEqual(Local1, One)) {
            Notify(\_SB_.VMOD.BAT1, 0x80)
            Notify(\_SB_.VMOD.AC1_, 0x80)
        }
    }
    Store(Local1, BNST)
}
";
        private string test =
   @"
\_SB_.SPI1.ASSC

Method (ASSC, 2, Serialized)
{
    Name (_T_0, Zero)
    Acquire (ASSM,0xFFFF)
    Store (Arg1, Local0)
    While (One)
    {
        Store (ToInteger (Arg0, ), _T_0)
        If (LEqual (_T_0,0x03))
        {
            Store (One, DOWS)
            Store (One, STRN)
            While (LAnd (BOWS,STRN))
            {
                If (Arg1)
                {
                    Stall (One)

                    Subtract (Local0, One, Local0)
                    If (LEqual (Local0,Zero))
                    {
                        Break
                    }
                }
            }
            Store (RAR2, SAR2)
            Store (Zero, RAR2)
            Store (0x02, ASCE)
            Store (SSSC, RG22)
        }
        Else
        {
            If (LEqual (_T_0,0x04))
            {
                Store (RG22, SSSC)
                Store (Zero, ASCE)
                Store (SAR2, RAR2)
                Store (One, Local0)
                Store (Zero, DOWS)
            }
        }
        Break
    }
    Release (ASSM)
    Return (Local0)
}

";
        /// <summary>
        /// Debug/Test purpose only
        /// </summary>
        public void RunMethodDebug()
        {
            List<AcpiData> amlDatas = new List<AcpiData>();
            //amlDatas.Add(new AcpiData(1));
           //amlDatas.Add(new AcpiData("string"));
            amlDatas.Add(new AcpiData(1));
            amlDatas.Add(new AcpiData(1));
            //RunMethodDebug(test, amlDatas);     // run with silent mode
            // RunMethodDebug(test, amlDatas, 0);  // run with ui mode
            //ushort utype = 4;
            //AcpiData acpiData = new AcpiData();
            //IntPtr intPtr = acpiLib.GetValue("\\___WAKP", ref utype);
            //if (intPtr != IntPtr.Zero)
            //{
            //    acpiData.FromAcpiOutput(intPtr);
            //    acpiLib.FreeArg(intPtr);
            //}           
        }
        private void RunMethodDebug(string strAsl, List<AcpiData> argList, int Type = 1)
        {
            ////IsAcpiNS("\\.ABCD");
            if (acpiLib == null || !acpiLib.DriverLoaded())
            {
                if (Type == 1)
                {

                    AmlOp op = RunAslMethod(strAsl, argList);
                }
                else
                {
                    DebugViewForm debugViewForm = new DebugViewForm();
                    debugViewForm.acpiLib = acpiLib;
                    // _SB_.VMOD.BAT1 _SB_.VMOD.BAT1
                    debugViewForm.strAcpiPath = "\\____SB_SPI1ASSC";
                    debugViewForm.strAslCode = strAsl;
                    debugViewForm.MethodArgs = argList;
                    debugViewForm.ShowDialog();
                }
            }
        }
    }
}
