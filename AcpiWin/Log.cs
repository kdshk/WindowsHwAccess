using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    public class Log
    {
        static private string log_file = @"log.txt";
        static public void Logs(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }
            using (StreamWriter sw = File.AppendText(log_file))
            {
                string Text = "";
                foreach(byte bt in bytes)
                {
                    Text += string.Format("0x{0:X2} ", (int)bt);
                }
                sw.WriteLine(Text);
            }
        }
        static public void Logs(string text)
        {
            // This text is always added, making the file longer over time
            // if it is not deleted.
            //return;
            using (StreamWriter sw = File.AppendText(log_file))
            {
                var currentStack = new System.Diagnostics.StackTrace(true);
                //return currentStack.ToString();
                sw.Write(string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now));
                sw.Write(" ");
                if (currentStack.FrameCount > 1)
                {     
                    string[] call_stack = currentStack.ToString().Split (new char[] {'\n'});
                    sw.Write(call_stack[1].Replace("\r",""));
                    sw.Write(" ");
                } else
                {
                    //sw.Write(currentStack.GetFrame(0).ToString());
                    string[] call_stack = currentStack.ToString().Split(new char[] { '\n' });
                    sw.Write(call_stack[0].Replace("\r", ""));
                    sw.Write(" ");
                }
                sw.WriteLine(text);
            }
        }
    }
}
