using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    public class AcpiMethodArg
    {
        /// <summary>
        ///  type of method arg
        /// </summary>
        public int Type  //0 integer, 1, string, 2, buffer, 3, pacakge
        {
            get;
            set;
        }
        /// <summary>
        /// length of arg for buffer, package
        /// </summary>
        public int Count
        {
            get;
            set;
        }
        /// <summary>
        /// string value of args
        /// </summary>
        public string strValue
        {
            get;
            set;
        }
        /// <summary>
        /// integer value of args
        /// </summary>
        public UInt64 ulValue
        {
            get;
            set;
        }
        /// <summary>
        /// buffer or package value of args
        /// </summary>
        public byte[] bValue
        {
            get;
            set;
        }
    }
}
