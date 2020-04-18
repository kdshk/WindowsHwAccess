using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AcpiWin
{
    public class AmlBuilder
    {
        // Map table for acpi type 
        public delegate void GetData(AcpiNS acpiNS);
        static public Dictionary<int, string> mAcpiTypeMap = new Dictionary<int, string>();
        static public Dictionary<string, GetData> mAcpiGetDataMap = new Dictionary<string, GetData>();
        static private AcpiNS acpiNSRoot;
        public AcpiLib acpiLib { get; set; }
        private AcpiNS acpiScope;
        public int ExtendedMethod = 0;
        /// <summary>
        /// Constructor - Initialize the private data before furthur operation.
        /// </summary>
        public AmlBuilder()
        {
            if (mAcpiTypeMap.Count == 0)
            {
                InitAcpiTypeMap();
            }
            if (mAcpiGetDataMap.Count == 0)
            {
                mAcpiGetDataMap["Method"] = GetMethodData;
                mAcpiGetDataMap["Integer"] = GetIntData;
                mAcpiGetDataMap["String"] = GetStringData;
                mAcpiGetDataMap["Buffer"] = GetBufferData;
                mAcpiGetDataMap["Pacakge"] = GetPackgeData;
                mAcpiGetDataMap["FieldUnit"] = GetFieldData;
            }
            if (acpiNSRoot == null)
            {
                acpiNSRoot = new AcpiNS("\\___", -1);
            }
            acpiScope = acpiNSRoot;

        }
        /// <summary>
        /// Check any data defined in method is not created by acpi subsystem and recreate it
        /// </summary>
        /// <param name="acpiNS">acpi name space</param>
        public void GetMethodData(AcpiNS acpiNS)
        {
            if (acpiLib != null)
            {
                //Get the AML of Method
                //int type = -1;
                //type = acpiLib.GetType(acpiNS.Path);
                //if (type != -1)
                //{
                //    ushort utype = (ushort)type;
                //    IntPtr intPtr = acpiLib.GetValue(acpiNS.Path, ref utype);
                //    if (intPtr != IntPtr.Zero)
                //    {
                //        int DataLength = Marshal.ReadInt32(intPtr + 0x4);
                //        // copy the method buffer to byte
                //        if (acpiNS.pbValue == null)
                //        {
                //            acpiNS.pbValue = new byte[DataLength];                            
                //            Marshal.Copy(intPtr, acpiNS.pbValue, 0, DataLength);
                //        }
                //        acpiLib.FreeArg(intPtr);
                //    }
                //}

                // Get the text of method
                //acpiLib.GetAslCode(acpiNS.Path, ref acpiNS.strValue);
            }
        }
        /*
         * Get the raw interger data from acpi libary
         */
        public void GetIntData(AcpiNS acpiNS)
        {
            if (acpiLib != null)
            {
                int type = -1;
                type = acpiLib.GetType(acpiNS.Path);
                if (type != -1)
                {
                    ushort utype = (ushort)type;
                    IntPtr intPtr = acpiLib.GetValue(acpiNS.Path, ref utype);
                    if (intPtr != IntPtr.Zero)
                    {
                        acpiNS.ulValue = (UInt64)Marshal.ReadInt64(intPtr + 0x10);
                        acpiLib.FreeArg(intPtr);
                    }
                }
            }
        }

        public void GetStringData(AcpiNS acpiNS)
        {
            if (acpiLib != null)
            {
                int type = -1;
                type = acpiLib.GetType(acpiNS.Path);
                if (type != -1)
                {
                    ushort utype = (ushort)type;
                    IntPtr intPtr = acpiLib.GetValue(acpiNS.Path, ref utype);
                    if (intPtr != IntPtr.Zero)
                    {
                        // acpi string is all ansi code
                        acpiNS.strValue = Marshal.PtrToStringAnsi(intPtr + 0x10);
                        acpiLib.FreeArg(intPtr);
                    }
                }
            }
        }

        public void GetBufferData(AcpiNS acpiNS)
        {
            if (acpiLib != null)
            {
                int type = -1;
                type = acpiLib.GetType(acpiNS.Path);
                if (type != -1)
                {
                    ushort utype = (ushort)type;
                    IntPtr intPtr = acpiLib.GetValue(acpiNS.Path, ref utype);

                    if (intPtr != IntPtr.Zero)
                    {
                        UInt32 val = (UInt32)Marshal.ReadInt32(intPtr);
                        if (val == 0x426F6541)
                        {
                            int DataLength = Marshal.ReadInt32(intPtr + 0x4);
                            // copy the buffer to byte
                            if (acpiNS.pbValue == null)
                            {
                                acpiNS.pbValue = new byte[DataLength];
                                Marshal.Copy(intPtr, acpiNS.pbValue, 0, DataLength);
                            }
                        }
                        acpiLib.FreeArg(intPtr);
                    }
                }
            }
        }

        public void GetPackgeData(AcpiNS acpiNS)
        {
            if (acpiLib != null)
            {
                int type = -1;
                type = acpiLib.GetType(acpiNS.Path);
                if (type != -1)
                {
                    ushort utype = (ushort)type;
                    IntPtr intPtr = acpiLib.GetValue(acpiNS.Path, ref utype);
                    if (intPtr != IntPtr.Zero)
                    {
                        UInt32 val = (UInt32)Marshal.ReadInt32(intPtr);
                        if (val == 0x426F6541)
                        {
                            int DataLength = Marshal.ReadInt32(intPtr + 0x4);
                            // copy the package buffer to byte
                            if (acpiNS.pbValue == null)
                            {
                                acpiNS.pbValue = new byte[DataLength];
                                Marshal.Copy(intPtr, acpiNS.pbValue, 0, DataLength);
                            }
                        }
                        acpiLib.FreeArg(intPtr);
                    }
                }
            }
        }
        public void GetFieldData(AcpiNS acpiNS)
        {
            if (acpiLib != null)
            {
                //int type = -1;
                //type = acpiLib.GetType(acpiNS.Path);
                //if (type != -1)
                //{
                //    ushort utype = (ushort)type;
                //    IntPtr intPtr = acpiLib.GetValue(acpiNS.Path, ref utype);
                //    if (intPtr != IntPtr.Zero)
                //    {
                //        int DataLength = Marshal.ReadInt32(intPtr + 0x4);                        
                //        acpiLib.FreeArg(intPtr);
                //    }
                //}
            }
        }
        public AcpiNS GetCurrentPath()
        {
            return acpiScope;
        }

        public void SetCurrentPath(AcpiNS acpiNS)
        {
            //acpiScope.GetLast();
            acpiScope = acpiNS;
        }

        public AcpiNS GetNS(string path, int type)
        {
            AcpiNS acpiNS = new AcpiNS(path, type);
            if (mAcpiGetDataMap.ContainsKey(acpiNS.Type))
            {
                mAcpiGetDataMap[acpiNS.Type].Invoke(acpiNS);
            }
            return acpiNS;
        }

        public AcpiNS AddChildItem(string path, int type)
        {
            AcpiNS acpiNS = new AcpiNS(path, type);
            //acpiNS.SetParent(acpiNSRoot);
            acpiScope.AddChild(acpiNS);
            if (mAcpiGetDataMap.ContainsKey(acpiNS.Type))
            {
                mAcpiGetDataMap[acpiNS.Type].Invoke(acpiNS);
            }
            return acpiNS;
        }

        /*
         * Initialize the acpi type map for register the acpi type
         */
        public static void InitAcpiTypeMap ()
        {
            mAcpiTypeMap[-1] = "Uninitialized";
            mAcpiTypeMap[0] = "Scope";
            mAcpiTypeMap[1] = "Integer";
            mAcpiTypeMap[2] = "String";
            mAcpiTypeMap[3] = "Buffer";
            mAcpiTypeMap[4] = "Pacakge";
            mAcpiTypeMap[5] = "FieldUnit";
            mAcpiTypeMap[6] = "Device";
            mAcpiTypeMap[7] = "Sync";
            mAcpiTypeMap[8] = "Method";
            mAcpiTypeMap[9] = "Mutex";
            mAcpiTypeMap[0xA] = "OperationRegion";
            mAcpiTypeMap[0xB] = "PowerSource";
            mAcpiTypeMap[0xC] = "Processor";
            mAcpiTypeMap[0xD] = "ThermalZone";
            mAcpiTypeMap[0xE] = "BufferUnit";
            mAcpiTypeMap[0xF] = "DDBHandle";
            mAcpiTypeMap[0x10] = "Debug";
            mAcpiTypeMap[0x80] = "Alias";
            mAcpiTypeMap[0x81] = "DataAlias";
            mAcpiTypeMap[0x82] = "BankField";
            mAcpiTypeMap[0x83] = "Field";
            mAcpiTypeMap[0x84] = "IndexField";
            mAcpiTypeMap[0x85] = "Data";
            mAcpiTypeMap[0x86] = "DataField";
            mAcpiTypeMap[0x87] = "DataObj";
            mAcpiTypeMap[0x88] = "Rev";
            mAcpiTypeMap[0x89] = "CreateField";
            mAcpiTypeMap[0x8A] = "External";
        }

        //
        // Get the Data definition in method..
        //
        //public delegate AmlData AmlAction(AmlOp amlop);
        //Dictionary<string, AmlAction> actions = new Dictionary<string, AmlAction>();
        public class AcpiNS
        {
            public string Path { get; set; }    // acpi path
            public string Type { get; set; }    // Type of data
            public string strValue;
            public UInt64 ulValue { get; set; }
            public string strValueType { get; set; }
            public byte[] pbValue { get; set; }
            private AcpiNS Parent;
            private List<AcpiNS> ChildItems = new List<AcpiNS>();   
            /// <summary>
            /// Constructor of acpi name space
            /// </summary>
            /// <param name="path"></param>
            /// <param name="type"></param>
            public AcpiNS(string path, int type)
            {
                Path = path;
                Type = GetType(type);
            }
            
            /// <summary>
            /// Add a child name space to current name space
            /// </summary>
            /// <param name="childItem">child item</param>
            public void AddChild(AcpiNS childItem)
            {
                childItem.Parent = this;
                ChildItems.Add(childItem);
            }
            /// <summary>
            /// get the acpi name type string
            /// </summary>
            /// <param name="type">acpi name type value</param>
            /// <returns>string represent the name type</returns>
            private string GetType (int type)
            {
                // TODO: map the acpi type value to string typ
                try
                {
                    return mAcpiTypeMap[type];
                }
                catch (Exception ex)
                {
                    // Log the error
                    Log.Logs(ex.Message);
                    return "InvalidAcpiType";
                }
            }
        }        
    }
}
