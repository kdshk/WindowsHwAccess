using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace AcpiWin
{
    struct AcpiTableObj{
        public string Name;
        public object obj;
    }
    class AcpiTable : IDisposable
    {
        private AmlMethodBuilder _amlMethodBuilder;
        private const int DisplayOffset = 30;
        public string TableName
        {
            get
            {
                return string.Format("{0}{1}{2}{3}", (char)_TableBinary[0]
                    , (char)_TableBinary[1], (char)_TableBinary[2], (char)_TableBinary[3]);
            }
        }
        public byte[] Table
        {
            get
            {
                return _TableBinary;
            }
        }
        private uint _TableSignature;
        private byte[] _TableBinary;
        public byte[] AmlCode
        {
            get
            {
                if (_TableBinary.Length > 0x24)
                {
                    return _TableBinary.Skip(0x24).ToArray();
                }
                return null;
            }
        }
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="TablePtr">point to the table content from a unmanaged buffer</param>
        /// <param name="TableSize">size of table</param>
        public AcpiTable(IntPtr TablePtr, uint TableSize)
        {
            if (TablePtr != IntPtr.Zero)
            {
                _TableSignature = (uint)Marshal.ReadInt32(TablePtr, 0);
                _TableBinary = new byte[TableSize];
                Marshal.Copy(TablePtr, _TableBinary, 0, (int)TableSize);
               
            }
        }
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="binary">raw acpi table binary</param>
        public AcpiTable(byte[] binary)
        {
            if (binary != null)
            {
                _TableSignature = BitConverter.ToUInt32(binary, 0);
                _TableBinary = binary;                
            }
        }
        /// <summary>
        /// dispose all resource
        /// </summary>
        public void Dispose()
        {
            _TableBinary = null;
        }

        /// <summary>
        /// to acpi table string
        /// </summary>
        /// <returns>string</returns>
        public override string ToString()
        {
            // table to string.... based on acpi xml 
            return AcpiTableToString();
        }

        public void SetAmlMethodBuilder(AmlMethodBuilder amlMethodBuilder)
        {
            _amlMethodBuilder = amlMethodBuilder;
        }

        /// <summary>
        /// Get the description from description list
        /// </summary>
        /// <param name="field">field that contains the description</param>
        /// <param name="intValue">value of field</param>
        /// <returns>descriptions</returns>
        private string GetXmlDescriptionFromValue(XmlNode field, UInt64 intValue)
        {
            // data type is a mandatory feild and must existing for description type other bit or byte
            bool bitType = field.Attributes["datatype"].Value.ToLower() == "bit";
            string content = "";
            string others = "";
            XmlNodeList xmlDescriptions = field.SelectNodes("description");
            if (bitType)
            {
                // it's a bit type...
                foreach (XmlNode xmlDescription in xmlDescriptions)
                {
                    if (xmlDescription.Attributes["value"] != null)
                    {
                        try
                        {
                            if (xmlDescription.Attributes["value"].Value.ToLower() == "others")
                            {
                                others = xmlDescription.Attributes["name"].Value;
                                continue;
                            }
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine(e.Message);
                            Log.Logs(e.Message);
                        }
                    }
                    // check the bit value...
                    UInt64 bitOffset = GetXmlAttrIntValue(xmlDescription, "bitoffset");
                    UInt64 bitLength = GetXmlAttrIntValue(xmlDescription, "bitlength");
                    // get bits
                    if (xmlDescription.Attributes["value"] == null)
                    {
                        // no value, just single bit set check
                        if ((intValue >> (int)(bitOffset) & 1) != 0)
                        {
                            content += xmlDescription.Attributes["name"].Value + "\n";
                        }
                    } else
                    {
                        // todo, value compare
                        UInt64 attrIntValue = GetXmlAttrIntValue(xmlDescription, "value");
                        // check if the value is equal
                        UInt64 bitFields = (intValue >> (int)(bitOffset)) & (0xFFFFFFFFFFFFFFFF >> (int)(64 - bitLength));
                        if (bitFields == attrIntValue)
                        {
                            content += xmlDescription.Attributes["name"].Value + "\n";
                        }
                    }
                }
                if (content.Length > 0)
                {
                    return content;
                }
            }
            else
            {
                foreach (XmlNode xmlDescription in xmlDescriptions)
                {
                    try
                    {
                        if (xmlDescription.Attributes["value"].Value.ToLower() == "others")
                        {
                            others = xmlDescription.Attributes["name"].Value;
                            continue;
                        }

                    }
                    catch (Exception e)
                    {
                        Log.Logs(e.Message);
                        System.Diagnostics.Debug.WriteLine(e.Message);
                    }


                    UInt64 attrIntValue = GetXmlAttrIntValue(xmlDescription, "value");
                    if (attrIntValue == intValue)
                    {
                        content = xmlDescription.Attributes["name"].Value;
                        return content;
                    }

                }
            }
            return others;
        }
        /// <summary>
        /// Get the int value from a attributes
        /// </summary>
        /// <param name="node">xml node</param>
        /// <param name="attrName">attribute name</param>
        /// <returns>int value or an exception that indicates the error</returns>
        private UInt64 GetXmlAttrIntValue(XmlNode node, string attrName)
        {
            UInt64 value = 0;
            try
            {
                string intString = node.Attributes[attrName].Value.ToLower();
                if (intString.StartsWith ("0x"))
                {
                    value = Convert.ToUInt64(intString, 16);
                } else
                {
                    value = UInt64.Parse(intString);
                }
            }
            catch (Exception e)
            {
                Log.Logs(e.Message);
                throw e;
            }
            return value;
        }
        /// <summary>
        /// Get the value from acpi table offset with giving length
        /// </summary>
        /// <param name="lenght">length of value</param>
        /// <param name="offset">offset of value</param>
        /// <returns>64bit unsigned int value</returns>
        private UInt64 GetAcpiValue(int lenght, int offset, byte[] data)
        {
            UInt64 value = 0;
            switch (lenght)
            {
                case 1:
                    value = (UInt64)data[offset];
                    break;
                case 2:
                    value = (UInt64)BitConverter.ToUInt16(data, offset);
                    break;
                case 4:
                    value = (UInt64)BitConverter.ToUInt32(data, offset);
                    break;
                case 8:
                    value = (UInt64)BitConverter.ToUInt64(data, offset);
                    break;
                default:
                    throw new ArgumentException();
            }
            return value; 
        }
        private string AcpiComponentDecode (XmlNode componentName, byte[] compoentValues)
        {
            return "";
        }
        /// <summary>
        /// Decode the fields
        /// </summary>
        /// <param name="root">xml file root</param>
        /// <param name="header">xml node to decode</param>
        /// <returns>table string for node</returns>
        public string AcpiFieldsDecode(XmlNode root, XmlNode header, byte[] data, int DispOff = DisplayOffset)
        {
            string content = "";
            //XmlNode header = root.SelectSingleNode("Header");

            if (header != null)
            {
                // do a parse of header
                try
                {
                    XmlNodeList fields = header.SelectNodes("field");
                    // get description of fields, length, offset, value type. etc

                    foreach (XmlNode xmlNode in fields)
                    {
                        int Length = -1;
                        //content += xmlNode.Attributes["name"].Value;
                        if (xmlNode.Attributes["length"].Value.StartsWith("0x") ||
                            xmlNode.Attributes["length"].Value.StartsWith("0X"))
                        {
                            Length = Convert.ToInt32(xmlNode.Attributes["length"].Value, 16);
                        }
                        else
                        {
                            Length = int.Parse(xmlNode.Attributes["length"].Value);
                        }
                        int offset = int.Parse(xmlNode.Attributes["offset"].Value);
                        string datatype = xmlNode.Attributes["datatype"].Value;
                        string type = xmlNode.Attributes["type"].Value;
                        string Name = xmlNode.Attributes["name"].Value;
                        string Blank = "                                        ";
                        // expand the name
                        Name =  Name + Blank.Substring(0, DispOff - Name.Length) + " : ";
                        content += Name;// xmlNode.Attributes["name"].Value + ":\t\t\t\t\t\t";
                        if (Length + offset > data.Length)
                        {
                            break;
                        }
                        switch (type)
                        {
                            case "char":
                                content += "'";
                                for (int Idx = 0; Idx < Length; Idx++)
                                {
                                    if (data[offset + Idx] == 0)
                                    {
                                        content += " ";
                                    }
                                    else
                                    {
                                        content += (char)data[offset + Idx];
                                    }
                                }
                                content += "'";
                                break;
                            case "value":
                                switch (Length)
                                {
                                    case 1:
                                    case 2:
                                    case 4:
                                    case 8:
                                        {
                                            UInt64 intValue = GetAcpiValue(Length, offset, data);
                                            content += string.Format("0x{0:X} ({1})", intValue,
                                                    intValue);
                                            break;
                                        }
                                    default:
                                        content += "0x";
                                        for (int Idx = 0; Idx < Length; Idx++)
                                        {
                                            content += string.Format("{0:X2}", data[offset + Idx]);
                                        }
                                        System.Diagnostics.Debug.WriteLine("incorrect data length");
                                        break;
                                }
                                break;
                            case "description":                                
                                {
                                    System.Diagnostics.Debug.WriteLine("TODO");
                                    UInt64 intValue = GetAcpiValue(Length, offset, data);
                                    string descriptions = GetXmlDescriptionFromValue(xmlNode, intValue);
                                    if (descriptions.Contains("\n"))
                                    {
                                        string[] descriptionlist = descriptions.Split(new char[] { '\n' });
                                        foreach (string description in descriptionlist)
                                        {
                                            if (description.Length > 1)
                                            {
                                                content += "\n" + Blank.Substring(0, DispOff + 3) + description;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        content += descriptions;
                                    }
                                    break;
                                }
                            case "component":
                                // get the compoent and parse
                                {
                                    if (xmlNode.Attributes["component"] != null)
                                    {
                                        // get the fields of data..
                                        byte[] component_data = new byte[Length];
                                        Array.Copy(data, offset, component_data, 0, Length);

                                        // find the component now
                                        XmlNodeList componentlist = root.SelectNodes("component");
                                        foreach (XmlNode component in componentlist)
                                        {
                                            if (component.Attributes["name"] != null)
                                            {
                                                if (component.Attributes["name"].Value == xmlNode.Attributes["component"].Value)
                                                {
                                                    //string componets = AcpiComponentDecode(component, component_data);
                                                    string components = AcpiFieldsDecode(root, component, component_data, DisplayOffset - 3);
                                                    if (components.Contains("\n"))
                                                    {
                                                        string[] descriptionlist = components.Split(new char[] { '\n' });
                                                        foreach (string description in descriptionlist)
                                                        {
                                                            if (description.Length > 1)
                                                            {
                                                                content += "\n" + Blank.Substring(0, 3) + description;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        content += components;
                                                    }
                                                    break;
                                                }                                               
                                            }
                                        }
                                    }
                                }
                                break;
                            default:
                                System.Diagnostics.Debug.WriteLine("unknow acpi type");
                                break;
                        }
                        content += "\n";
                    }
                }
                catch (Exception e)
                {
                    Log.Logs(e.Message);
                    System.Diagnostics.Debug.WriteLine(e.Message);
                    return "";
                }
                return content;
            }
            return content;

        }
        /// <summary>
        /// internal to acpi table string
        /// </summary>
        /// <returns>string</returns>
        private string AcpiTableToString()
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load("acpi.xml");
            string content = "";
            XmlNode root = xmlDocument.SelectSingleNode("AcpiTable");
            if (root == null)
            {
                return "Invalid Acpi Table Description XML File";
            }

            XmlNodeList nodeList = root.SelectNodes("Table");

            foreach (XmlNode xmlNode in nodeList)
            {
                try
                {
                    if (xmlNode.Attributes[0].Value == TableName)
                    {
                        // match
                        
                        try
                        {
                            XmlNode Head = xmlNode.SelectSingleNode("header");
                            if (Head != null && Head.Attributes["type"].Value == "Header")
                            {
                                XmlNode stdheader = root.SelectSingleNode("Header");
                                content = "/*\nStandard Table Header:\n" + AcpiFieldsDecode(root, stdheader,_TableBinary) + "*/\n";
                                // c# aml decode for tables.
                                if (Head.Attributes["aml"] != null && Head.Attributes["aml"].Value == "true")
                                {
                                    // parse the table then
                                    AmlDisassemble amlDisassemble = new AmlDisassemble();
                                    content += "\n";
                                    amlDisassemble.SetAmlMethodBuilder(_amlMethodBuilder);
                                    // make a define..
                                    string Header = Util.StringFromBytes(_TableBinary.Take(4).ToArray());
                                    string OemId = Util.StringFromBytes(_TableBinary.Skip(10).Take(6).ToArray());
                                    string OemTableId = Util.StringFromBytes(_TableBinary.Skip(16).Take(8).ToArray());
                                    UInt32 OemRev = BitConverter.ToUInt32(_TableBinary, 24);
                                    content += string.Format("DefinitionBlock(\"\", \"{0}\", {1}, \"{2}\", \"{3}\", 0x{4:X8})\n", Header, _TableBinary[8], OemId, OemTableId, OemRev) + "{\n";
                                    content += amlDisassemble.DecodeAmlFromTable(_TableBinary);
                                    content += "}";
                                    return content;
                                }
                            }
                            content += "\n";
                            content += AcpiFieldsDecode(root, xmlNode, _TableBinary);
                        } catch (Exception e)
                        {
                            Log.Logs(e.Message);
                            System.Diagnostics.Debug.WriteLine(e.Message);
                            return "";
                        }
                        //content = AcpiStandardHeader(root);
                        return content;
                    }
                }
                catch (Exception e)
                {
                    Log.Logs(e.Message);
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }               
            }
            // no matching just follow standard tables
            XmlNode header = root.SelectSingleNode("Header");
            return AcpiFieldsDecode(root, header, _TableBinary);
        }
       
    }
    class AcpiTables
    {
        [DllImport("Kernel32.dll", EntryPoint = "EnumSystemFirmwareTables", CharSet = CharSet.Unicode)]
        public static extern uint EnumSystemFirmwareTables(UInt32 FirmwareTableProviderSignature,
            IntPtr pFirmwareTableEnumBuffer,
            UInt32 BufferSize);

        [DllImport("kernel32.dll", EntryPoint = "GetSystemFirmwareTable", CharSet = CharSet.Unicode)]
        public static extern uint GetSystemFirmwareTable(UInt32 FirmwareTableProviderSignature,
            UInt32 FirmwareTableID,
            IntPtr pFirmwareTableBuffer,
            UInt32 BufferSize);

        

        private List<AcpiTable> _Tables;

        public List<AcpiTable> Tables
        {
            get
            {
                return _Tables;
            }
        }

        public AcpiTables()
        {
            _Tables = new List<AcpiTable>();
        }

        private void AddTable (IntPtr TablePtr, uint TableSize)
        {
            AcpiTable acpiTable = new AcpiTable(TablePtr, TableSize);
            SaveAcpiTable(acpiTable.TableName, acpiTable.Table);
            _Tables.Add(acpiTable);
        }

        private void AddTable(byte[] binary)
        {
            AcpiTable acpiTable = new AcpiTable(binary);
            _Tables.Add(acpiTable);
        }

        public IntPtr GetAcpiTable(UInt32 FirmwareTableID, ref uint TableSize)
        {
            //0x41435049 = 'ACPI' signature
            uint BufSize = GetSystemFirmwareTable(0x41435049, FirmwareTableID, IntPtr.Zero, 0);
            IntPtr AcpiTablePtr = Marshal.AllocHGlobal((int)BufSize);
            if (AcpiTablePtr != IntPtr.Zero)
            {
                TableSize = BufSize;
                if (GetSystemFirmwareTable(0x41435049, FirmwareTableID, AcpiTablePtr, BufSize) != BufSize)
                {
                    Marshal.FreeHGlobal(AcpiTablePtr);
                    AcpiTablePtr = IntPtr.Zero;
                }
            }
            return AcpiTablePtr;
        }
        /// <summary>
        /// Query all acpi tables from local system - reuqested administrator priviledge
        /// </summary>
        public void QueryAcpiTables()
        {
            //0x41435049 = 'ACPI' signature
            uint BufSize = EnumSystemFirmwareTables(0x41435049, IntPtr.Zero, 0);
            IntPtr AcpiSignaturesPtr = Marshal.AllocHGlobal((int)BufSize);
            if (AcpiSignaturesPtr != IntPtr.Zero)
            {
                if (EnumSystemFirmwareTables(0x41435049, AcpiSignaturesPtr, BufSize) == BufSize)
                {
                    // go through all the tables
                    for (int nSize = 0; nSize < (int)BufSize; nSize += 4)
                    {
                        UInt32 AcpiSignature = (UInt32)Marshal.ReadInt32(AcpiSignaturesPtr, nSize);
                        //0x54445353 = 'SSDT' signature
                        if (AcpiSignature == 0x54445353)
                        {
                            // SSDT using register to query since multiple SSDT in system 
                            continue;
                        }
                        // get the table
                        uint TableSize = 0;
                        IntPtr AcpiTablePtr = GetAcpiTable(AcpiSignature, ref TableSize);
                        if (AcpiTablePtr != IntPtr.Zero)
                        {
                            // Add the table
                            AddTable(AcpiTablePtr, TableSize);
                            Marshal.FreeHGlobal(AcpiTablePtr);
                        }
                    }
                }
                Marshal.FreeHGlobal(AcpiSignaturesPtr);
            }
            // now try to get all SSDT from registry
            QuerySSDT();
        }
        private RegistryKey GetSubOrFirstReg(RegistryKey Root, string Name)
        {
            if (Root == null)
            {
                return null;
            }
            if (Name != null)
            {
                return  Root.OpenSubKey(Name);
            }
            else
            {
                string[] regs = Root.GetSubKeyNames();
                if (regs != null && regs.Length >= 1)
                {
                    return Root.OpenSubKey(regs[0]);
                }
            }
            return null;
        }
        private void SaveAcpiTable(string TableName, byte[] data)
        {
            string FileName = TableName + ".bin";
            if (File.Exists(FileName))
            {
                //File.Delete(FileName);
                return;
            }
            using (BinaryWriter binWriter =
                new BinaryWriter(File.Open(FileName, FileMode.Create)))
            {
                // Write string   
                binWriter.Write(data);
                binWriter.Close();
            }
        }
        private void QuerySSDT()
        {
            RegistryKey HardwareAcpi = Registry.LocalMachine.OpenSubKey("HARDWARE\\ACPI");
            string[] tables = HardwareAcpi.GetSubKeyNames();
            if (tables == null || tables.Length < 1)
            {
                return;
            }
            foreach (string table in tables)
            {
                if (!table.StartsWith("FA"))
                {                 
                    // Root Key/OemId/TableId/RevID/000000
                    RegistryKey ssdt = GetSubOrFirstReg(HardwareAcpi, table);
                    RegistryKey OemId = GetSubOrFirstReg(ssdt, null);
                    RegistryKey TableId = GetSubOrFirstReg(OemId, null);
                    RegistryKey RevID = GetSubOrFirstReg(TableId, null);
                    if (RevID != null)
                    {
                        byte[] binary = (byte[])RevID.GetValue("00000000", null);
                        if (binary != null)
                        {
                            AddTable(binary);
                            // save table
                            SaveAcpiTable(table, binary);
                        }
                        RevID.Close();
                    }
                    if (TableId != null)
                    {
                        TableId.Close();
                    }
                    if (OemId != null)
                    {
                        OemId.Close();
                    }
                    if (ssdt != null)
                    {
                        ssdt.Close();
                    }
                }
            }
            HardwareAcpi.Close();
        }

        ~AcpiTables()
        {
            foreach (AcpiTable acpiTable in _Tables)
            {
                acpiTable.Dispose();
            }
        }
    }
}
