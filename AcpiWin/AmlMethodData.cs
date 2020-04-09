using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    /// <summary>
    /// AmlMethodData for internal Debug View to display locals, args or other name space data.
    /// </summary>
    public class AmlMethodData
    {
        /// <summary>
        /// Name of Data
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Type of data
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// String represented the data
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// Construct of AmlMethodData
        /// </summary>
        /// <param name="amlData">amlData to AmlMethodData</param>
        public AmlMethodData (AcpiData amlData)
        {
            Name = amlData.Name;
            if (Name.StartsWith("InternalMethodArg"))
            {
                Name = Name.Substring(14);
            } else if (Name.StartsWith("InternalLocalData"))
            {
                Name = Name.Replace("InternalLocalData", "Local");
            }
            AcpiDataType type = amlData.Type;
            if (type == AcpiDataType.Alias)
            {
                type = amlData.Alias.Type;
            }
            switch (type)
            {
                case AcpiDataType.Int:
                    Type = "Integer";
                    Value = string.Format("0x{0:X}", amlData.Value);
                    break;
                case AcpiDataType.String:
                    Type = "String";
                    Value = string.Format("{0}", amlData.strValue);
                    break;
                case AcpiDataType.Buffer:
                    Type = "Buffer";
                    Value = string.Format("Buffer(0x{0:X})", amlData.bpValue.Length);
                    Value += "{";
                    for(int Index = 0; Index < amlData.bpValue.Length; Index++)
                    {
                        Value += string.Format("0x{0:X}", (int)amlData.bpValue[Index]);
                        if (Index < amlData.bpValue.Length -1 )
                        {
                            Value += ",";
                        }
                    }
                    Value += "}";
                    break;
                case AcpiDataType.Packge:
                    Type = "Packge";
                    //System.Diagnostics.Debug.Assert(false);
                    break;
                case AcpiDataType.Mutex:
                    Type = "Mutex";
                    Value = string.Format("0x{0:X}", amlData.Value);
                    break;
                case AcpiDataType.Event:
                    Type = "Event";
                    Value = string.Format("0x{0:X}", amlData.Value);
                    break;
                case AcpiDataType.FieldUnit:
                    Type = "FieldUnit";
                    Value = string.Format("0x{0:X}", amlData.Value);
                    break;
                default:
                    DebugMsg("unknow type of aml method data");
                    break;
            }
        }
        /// <summary>
        /// Print debug message to visual studio console for debug
        /// </summary>
        /// <param name="msg">message to display</param>
        private void DebugMsg(string msg)
        {
            // get system time log
            string strDate = DateTime.Now.ToString();
            System.Diagnostics.Debug.WriteLine(string.Format("AmlMethodData: {0} - {1}", strDate, msg));
            Log(string.Format("AmlMethodData: {0}", msg));
        }
        /// <summary>
        /// Log to event
        /// </summary>
        /// <param name="msg">message to log</param>
        private void Log(string msg)
        {
            //eventLog = new EventLog();
            EventLog eventLog = new EventLog();
            if (!EventLog.SourceExists("AcpiWin"))
            {
                EventLog.CreateEventSource(
                    "AcpiWin", "AcpiWinNewLog");
            }
            eventLog.Source = "AcpiWin";
            eventLog.Log = "AcpiWinNewLog";
            eventLog.WriteEntry(msg);
        }
    }
}
