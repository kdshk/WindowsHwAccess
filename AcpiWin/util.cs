using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    public static class Util
    {
        /// <summary>
        /// get the string from byte array
        /// </summary>
        /// <param name="array">byte array</param>
        /// <returns>string</returns>
        public static string StringFromBytes (byte[] array)
        {
            int length = 0;
            for (length = 0; length < array.Length; length ++)
            {
                if (array[length] == 0)
                {
                    break;
                }
            }
            return Encoding.ASCII.GetString(array.Take(length).ToArray());
        }
    }
}
