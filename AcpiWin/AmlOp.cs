using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    public class AmlOp
    {
        public enum OpType { 
            Definition = 1, 
            Condition, 
            Type1Op,
            Type2Op,
            Type6Op,
            UserDefined,
            Value,
            OSPM
        };
        public string Name;             // for arg name parse
        public OpType Type;             // Condition, Operation, UserDefine
        public int iArgs;
        public string OpCode;
        public string Asl;
        //public string NextCode;
        public string Code;
        public string ConditionCode;        
        public int Line;
        // Args
        public AcpiData[] Args;
        public List<AcpiData> amlDatas = new List<AcpiData>();
        // Sub Operation
        public List<AmlOp> SubOp = new List<AmlOp>();
        public AmlOp ParentOp;
        public AcpiData Result = new AcpiData();
        /// <summary>
        /// constructor 
        /// </summary>
        /// <param name="opcode">opcode name</param>
        public AmlOp (string opcode)
        {
            OpCode = opcode;
            Result.Name = "Result";
        }
        /// <summary>
        /// Get the condition code
        /// </summary>
        /// <returns>asl code of condition</returns>
        public string GetCondCode ()
        {
            return this.OpCode + this.ConditionCode;
        }
        /// <summary>
        /// Add a sub data
        /// </summary>
        /// <param name="data">data to add</param>
        public void AddSubData(AcpiData data)
        {
            amlDatas.Add(data);
        }
        /// <summary>
        /// Get nnumber of Sub Data count of AmlOp
        /// </summary>
        public int SubDataCount
        {
            get
            {
                return amlDatas.Count;
            }
        }
        /// <summary>
        /// Get number of Sub OpCode count of AmlOp
        /// </summary>
        public int SubOpCount
        {
            get
            {
                return SubOp.Count;
            }

        }
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="opcode">opcode name</param>
        /// <param name="Line">internal code</param>
        public AmlOp(string opcode, int Line)
        {
            Result.Name = "Result";
            OpCode = opcode;
            this.Line = Line;
        }
        /// <summary>
        /// Add a sub AmlOp 
        /// </summary>
        /// <param name="amlSubOp">sub operation</param>
        public void AddSubOp (AmlOp amlSubOp)
        {
            amlSubOp.ParentOp = this;
            SubOp.Add(amlSubOp);
        }
        /// <summary>
        /// Add a mixed type of arg/integer or string
        /// </summary>
        /// <param name="Name">name of arg</param>
        /// <param name="arg">string value represent the arg</param>
        public void AddArgs (string Name,string arg)
        {
            // what kind of data...
            AcpiData amlData = new AcpiData();
            amlData.Name = Name;
            if (Name.Equals("Zero"))
            {
                amlData.Type = AcpiDataType.Int;
                amlData.Value = 0;
            }
            else if (Name.Equals("One"))
            {
                amlData.Type = AcpiDataType.Int;
                amlData.Value = 1;
            }
            else if (Name.Equals("Ones"))
            {
                amlData.Type = AcpiDataType.Int;
                amlData.Value = 0xFFFFFFFFFFFFFFFF;
            }
            else
            {
                amlData.Type = AcpiDataType.String;
                amlData.strValue = arg;
            }            
            amlDatas.Add(amlData);
        }
        /// <summary>
        /// Add a integer arg
        /// </summary>
        /// <param name="Name">name of arg</param>
        /// <param name="arg">value of arg</param>
        public void AddArgs(string Name, UInt64 arg)
        {
            // what kind of data...
            AcpiData amlData = new AcpiData();
            amlData.Name = Name;
            amlData.Type = AcpiDataType.String;
            amlData.Value = arg;
            amlDatas.Add(amlData);
        }
    }
}
