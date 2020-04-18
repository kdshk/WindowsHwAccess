using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    class Logs
    {
        private string _LogFileName;
        public Logs(string LogFileName)
        {
            _LogFileName = LogFileName;
        }
        /// <summary>
        /// Log the message into log file
        /// </summary>
        /// <param name="Message"></param>
        public void Log (string Message)
        {
            string strDate = DateTime.Now.ToString();
            //Append(string.Format("{0} - {1}", strDate, Message));
        }

        private void Append (string Message)
        {
            using (StreamWriter w = File.AppendText(_LogFileName))
            {
                w.WriteLine(Message);
            }
        }
    }
}
