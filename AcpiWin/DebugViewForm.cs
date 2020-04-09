using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AcpiWin
{
    public partial class DebugViewForm : Form
    {
        public AcpiLib acpiLib { get; set; }
        public string strRoot { get; set; }
        public string strAcpiPath { get; set; }
        public string strAslCode;
        public List<AcpiData> MethodArgs { get; set; }
        private int nHightLine = -1;
        private AmlMethod amlMethod = null;
        private Boolean MethodDone =false;
        public AcpiData Result { get; set; }
        public DebugViewForm()
        {
            InitializeComponent();
            MethodDone = false;
            //ResizeLayout();
            //richTextBox1.Text = strAslCode;
        }
        private void ResizeLayout()
        {
            richTextBox2.Visible = true;
            richTextBox2.Left = dataGridView1.Left = 0;
            dataGridView1.Top = 0;
            richTextBox2.Width = dataGridView1.Width = this.ClientSize.Width /2-2;
            dataGridView1.Height = (this.ClientSize.Height) / 2;
            richTextBox2.Top = dataGridView1.Bottom;
            richTextBox2.Height = dataGridView1.Height;
            richTextBox1.Left = this.ClientSize.Width / 2 + 2;
            richTextBox1.Top = 0;
            richTextBox1.Width = richTextBox2.Width;
            richTextBox1.Height = (this.ClientSize.Height);
            if (nHightLine != -1)
            {
                ActiveLine(nHightLine);
            }
        }

        private void DebugViewForm_Resize(object sender, EventArgs e)
        {
            ResizeLayout();
        }

        private void DebugViewForm_Load(object sender, EventArgs e)
        {          
            richTextBox1.Text = strAslCode;
            nHightLine = GetStartLine();            
            ResizeLayout();
            amlMethod = new AmlMethod(
                acpiLib,
                (string)strAcpiPath);
            amlMethod.PrepareMethodRun(richTextBox1.Text, MethodArgs);
        }

        private void ActiveLine (int nLine)
        {
            //richTextBox1.Select(0, richTextBox1.Text.Length);                 // Select from there to the end                
            //richTextBox1.SelectionBackColor = richTextBox1.BackColor;
            //DeactiveLine(nLine);
            int targetLine = nLine;
            
            string[] lines = richTextBox1.Lines;
            if (targetLine == -1)
            {
                targetLine = lines.Length - 1;
                while (true) {
                    string value = lines[targetLine].Trim(new char[] {'\r', '\n' });
                    if (value.Length > 1)
                    {
                        break;
                    }
                    targetLine--;
                }
            }
            // Get the 1st char index of the appended text
            int start = richTextBox1.GetFirstCharIndexFromLine(targetLine);
            string line = lines[targetLine];
            int length = lines[targetLine].Length;
            
            //start - lines.TrimStart();
            start = start + (line.Length - line.TrimStart().Length);
            length = length - (length - line.Trim().Length);
            // Select from there to the end
            richTextBox1.Select(start, length);                 
            richTextBox1.SelectionBackColor = Color.Yellow;

            //int firstVisibleChar = richTextBox1.GetCharIndexFromPosition(new Point(0, richTextBox1.Height / 2));
            //int lineIndex = richTextBox1.GetLineFromCharIndex(firstVisibleChar);
            //int LastLine = richTextBox1.GetCharIndexFromPosition(new Point(0, richTextBox1.Height));
            //int lastIndex = richTextBox1.GetLineFromCharIndex(LastLine);

            //if (targetLine > lineIndex)
            //{
            //    if (lastIndex < lines.Length - 2)
            //    {
            //        //richTextBox1.ScrollToCaret();                    
            //        //richTextBox1.HScroll
            //    }
            //}
        }
        private void DeactiveLine(int nLine)
        {
            //string[] lines = richTextBox1.Lines;
            var start = richTextBox1.GetFirstCharIndexFromLine(nLine);  // Get the 1st char index of the appended text
            var length = richTextBox1.Lines[nLine].Length;
            richTextBox1.Select(start, length);                 // Select from there to the end                
            richTextBox1.SelectionBackColor = richTextBox1.BackColor;
        }

        private int GetStartLine()
        {
            int nLine = richTextBox1.Lines.Length;
            for (int Index = 0; Index < nLine; Index++)
            {
                if (richTextBox1.Lines[Index].Contains ("Method ("))
                {
                    return Index + 2;
                }
            }
            return -1;
        }

        private void richTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.F10 && e.KeyCode != Keys.F12)
            {
                return;
            }
            if (e.KeyCode == Keys.F12)
            {
                if (MethodDone)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();                    
                }
                return;
            }
            // check status
            AmlOp amlOp = null;
            if (amlMethod == null || MethodDone == true)
            {
                if (MethodDone)
                {
                    MessageBox.Show("Code running done, Press F12 to return");
                }
                return;
            }
            // run step code
            amlOp = amlMethod.RunStep();
            if (amlMethod.HasError())
            {
                MessageBox.Show("Aml code debug encount an error, code may not run correct if continue running");
            }
            if (amlOp == null)
            {
                // finish or failed to run code mark method done
                MethodDone = true;                
            } else
            {
                if (amlOp.OpCode == "Return")
                {
                    // there is a return code from method, mark its done
                    MethodDone = true;
                    Result = amlOp.Result;
                    UpdateData(amlOp);
                    List<AmlMethodData> methodDatas = (List<AmlMethodData>)dataGridView1.DataSource;
                    if (methodDatas != null)
                    {
                        methodDatas.Add(new AmlMethodData(amlOp.Result));
                        dataGridView1.DataSource = methodDatas;
                        dataGridView1.Update();
                        dataGridView1.Refresh();
                        dataGridView1.IsAccessible = false;
                    }
                } else
                {
                    UpdateData(null);
                }
                richTextBox2.Text = amlMethod.NextCode();                
            }
            if (!MethodDone)
            {
                //int nHightLine
                DeactiveLine(nHightLine);
                int hLine = amlMethod.GetLine();
                if (hLine > 2)
                {
                    nHightLine = hLine;
                    ActiveLine(hLine);
                }
                if (hLine == 0)
                {
                    ActiveLine(-1);
                }
            } else
            {
                MessageBox.Show("Code running done, Press F12 to return");
            }
        }
        private void UpdateData(AmlOp amlOp)
        {
            if (amlMethod != null)
            {
                List<AcpiData> args = amlMethod.ViewArgData();
                List<AcpiData> locals = amlMethod.ViewLocalData();
                List<AcpiData> datas = amlMethod.ViewDefinedData();
                List<AmlMethodData> methodData = new List<AmlMethodData>();
                // convert to method args data...
                if (args.Count > 0)
                {
                    foreach (AcpiData amlData in args)
                    {
                        methodData.Add(new AmlMethodData(amlData));
                    }
                }
                if (locals.Count > 0)
                {
                    foreach (AcpiData amlData in locals)
                    {
                        methodData.Add(new AmlMethodData(amlData));
                    }
                }
                if (datas.Count > 0)
                {
                    foreach (AcpiData amlData in datas)
                    {
                        methodData.Add(new AmlMethodData(amlData));
                    }
                }
                if (amlOp != null && amlOp.OpCode == "Return")
                {
                    amlOp.Result.Name = "Return";
                    methodData.Add(new AmlMethodData(amlOp.Result));
                }
                dataGridView1.DataSource = methodData;
                dataGridView1.Update();
                dataGridView1.Refresh();
                dataGridView1.IsAccessible = false;
            }
        }
        private void richTextBox2_KeyUp(object sender, KeyEventArgs e)
        {
            richTextBox1_KeyUp(sender, e);
        }

        private void dataGridView1_KeyUp(object sender, KeyEventArgs e)
        {
            richTextBox1_KeyUp(sender, e);
        }

        private void DebugViewForm_KeyUp(object sender, KeyEventArgs e)
        {
            richTextBox1_KeyUp(sender, e);
        }
    }
    
}
