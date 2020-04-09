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
    public partial class ArgInput : Form
    {
        /// <summary>
        /// Arg type matrix, acpi support 7 at max
        /// </summary>
        private int[] TabArgType = new int[7];
        //public AcpiMethodArg[] ArgValues = new AcpiMethodArg[7];
        private List<AcpiMethodArg> _Args = new List<AcpiMethodArg>();

        public List<AcpiMethodArg> Args
        {
            get
            {
                return _Args;
            }
        }

        /// <summary>
        /// constructor
        /// </summary>
        public ArgInput()
        {
            InitializeComponent();
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            InitAppSize();            
        }
        /// <summary>
        /// initialize the windows size
        /// </summary>
        private void InitAppSize()
        {
            int AppX = 65536;
            int AppY = 65536;
            foreach (Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                if (screen.Bounds.Width < AppX)
                {
                    AppX = screen.Bounds.Width;
                }
                if (screen.Bounds.Height < AppY)
                {
                    AppY = screen.Bounds.Height;
                }
            }

            AppX = AppX * 2/ 3;
            AppY = AppY / 2;
            this.Size = new Size(AppX, AppY);
            if (ArgType == -1)
            {
                ArgType = 0;
                radioButton1.Checked = true;
            }            
        }
        /// <summary>
        /// layout resizing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ArgInput_Resize(object sender, EventArgs e)
        {
            groupBox1.Top = 0;
            tabControl1.Left = tabControl1.Top = 0;
            tabControl1.Width = this.ClientRectangle.Width - groupBox1.Width - 5;
            groupBox1.Left = tabControl1.Width + 5;
            tabControl1.Height = this.ClientRectangle.Height;
            groupBox1.Height = tabControl1.Height;

            //button2.Left = button1.Left = groupBox1.Left + 10;
            //button2.Height = button1.Height = groupBox1.Height / 20;
            //button2.Width = button1.Width = groupBox1.Width - 20;
            //button2.Top = 0;// groupBox1.Height - 4*(button2.Height + 10);
            //button1.Top = groupBox1.Height - 20 *(button2.Height + 10);

            for (int pageIdx = 0; pageIdx < tabControl1.TabPages.Count; pageIdx++)
            {
                // clear all edits..
                if (tabControl1.TabPages[pageIdx].Controls.Count > 0)
                {
                    tabControl1.TabPages[pageIdx].Controls[0].Left = 0;
                    tabControl1.TabPages[pageIdx].Controls[0].Top = 0;
                    tabControl1.TabPages[pageIdx].Controls[0].Width =
                        tabControl1.TabPages[pageIdx].ClientRectangle.Width;
                    tabControl1.TabPages[pageIdx].Controls[0].Height =
                        tabControl1.TabPages[pageIdx].ClientRectangle.Height;
                }
            }            
        }

        private int ArgCount = 7;
        private int ArgType = -1;
        private Boolean bEval = false;
        public Boolean GetEvalState ()
        {
            return bEval;
        }
        /// <summary>
        /// set input strings
        /// </summary>
        public void SetInputString()
        {
            radioButton1.Visible = false;
            radioButton2.Visible = true;
            radioButton3.Visible = false;
            for (int pageIdx = 0; pageIdx < tabControl1.TabPages.Count; pageIdx++)
            {
                // clear all edits..
                tabControl1.TabPages[pageIdx].Controls.Clear();
            }
            tabControl1.TabPages.Clear();
            for (int args = 0; args < 1; args++)
            {
                tabControl1.TabPages.Add(string.Format("Feature String"));
                TabArgType[args] = 1;
            }
            for (int args = 0; args < 1; args++)
            {
                RichTextBox richTextBox = new RichTextBox();
                richTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.richTextBox1_KeyPress);
                //richTextBox.K
                tabControl1.TabPages[args].Controls.Add(richTextBox);
                tabControl1.TabPages[args].Controls[0].Left = 0;
                tabControl1.TabPages[args].Controls[0].Top = 0;
                tabControl1.TabPages[args].Controls[0].Width =
                    tabControl1.TabPages[args].ClientRectangle.Width;
                tabControl1.TabPages[args].Controls[0].Height =
                    tabControl1.TabPages[args].ClientRectangle.Height;

            }
            radioButton2.Checked = true;
        }
        /// <summary>
        /// Notification type arg request
        /// </summary>
        public void SetNotifyInput()
        {
            radioButton1.Visible = true;
            radioButton2.Visible = false;
            radioButton3.Visible = false;
            for (int pageIdx = 0; pageIdx < tabControl1.TabPages.Count; pageIdx++)
            {
                // clear all edits..
                tabControl1.TabPages[pageIdx].Controls.Clear();
            }
            tabControl1.TabPages.Clear();
            for (int args = 0; args < 1; args++)
            {
                tabControl1.TabPages.Add(string.Format("Notification Code"));
                TabArgType[args] = 0;
            }
            for (int args = 0; args < 1; args++)
            {
                RichTextBox richTextBox = new RichTextBox();
                richTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.richTextBox1_KeyPress);
                //richTextBox.K
                tabControl1.TabPages[args].Controls.Add(richTextBox);
                tabControl1.TabPages[args].Controls[0].Left = 0;
                tabControl1.TabPages[args].Controls[0].Top = 0;
                tabControl1.TabPages[args].Controls[0].Width =
                    tabControl1.TabPages[args].ClientRectangle.Width;
                tabControl1.TabPages[args].Controls[0].Height =
                    tabControl1.TabPages[args].ClientRectangle.Height;

            }
            radioButton1.Checked = true;
        }
        /// <summary>
        /// Set arg count when request new args
        /// </summary>
        /// <param name="argCount"></param>
        public void SetArgCount(int argCount)
        {
            // clear all pages
            bEval = false;
            ArgCount = argCount;
            radioButton1.Visible = true;
            radioButton2.Visible = true;
            radioButton3.Visible = true;
            for (int pageIdx = 0; pageIdx < tabControl1.TabPages.Count; pageIdx++)
            {
                // clear all edits..
                tabControl1.TabPages[pageIdx].Controls.Clear();
            }
            tabControl1.TabPages.Clear();
            for (int args = 0; args < ArgCount; args++)
            {
                tabControl1.TabPages.Add(string.Format("Argument {0}", args));
                TabArgType[args] = 0;
            }
            for (int args = 0; args < ArgCount; args++)
            {
                RichTextBox richTextBox = new RichTextBox();
                richTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.richTextBox1_KeyPress);
                //richTextBox.K
                tabControl1.TabPages[args].Controls.Add(richTextBox);
                tabControl1.TabPages[args].Controls[0].Left = 0;
                tabControl1.TabPages[args].Controls[0].Top = 0;
                tabControl1.TabPages[args].Controls[0].Width =
                    tabControl1.TabPages[args].ClientRectangle.Width;
                tabControl1.TabPages[args].Controls[0].Height =
                    tabControl1.TabPages[args].ClientRectangle.Height;

            }
            radioButton1.Checked = true;
        }
        /// <summary>
        /// set right arg type when switch between args
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

            ArgType =TabArgType[tabControl1.SelectedIndex];
            switch (ArgType)
            {
                case 0:
                    radioButton1.Checked = true;
                    break;
                case 1:
                    radioButton2.Checked = true;
                    break;
                case 2:
                    radioButton3.Checked = true;
                    break;
                case 3:
                    radioButton4.Checked = true;
                    break;
            }
        }
        /// <summary>
        /// arrange the input layout
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ArgInput_StyleChanged(object sender, EventArgs e)
        {
            groupBox1.Top = 0;
            tabControl1.Left = tabControl1.Top = 0;
            tabControl1.Width = this.ClientRectangle.Width - groupBox1.Width - 5;
            groupBox1.Left = tabControl1.Width + 5;
            tabControl1.Height = this.ClientRectangle.Height;
            groupBox1.Height = tabControl1.Height;

            button2.Left = button1.Left =10;
            button2.Width = button1.Width = groupBox1.ClientRectangle.Width - 20;
            button2.Height = button1.Height = groupBox1.ClientRectangle.Height / 10;
            button1.Top = groupBox1.ClientRectangle.Height - 1 * (button2.Height + 10);
            button2.Top = groupBox1.ClientRectangle.Height - 2 * (button2.Height + 10);

            radioButton1.Height = radioButton2.Height = radioButton3.Height = radioButton4.Height = 
                groupBox1.ClientRectangle.Height / 10;

            radioButton1.Width = radioButton2.Width = radioButton3.Width = radioButton4.Width = 
                groupBox1.ClientRectangle.Width - 20;

            radioButton1.Left = radioButton2.Left = radioButton3.Left = radioButton4.Left = 10;

            radioButton1.Top = groupBox1.ClientRectangle.Height / 10;
            radioButton2.Top = radioButton1.Top + radioButton1.Top + radioButton1.Height;
            radioButton3.Top = radioButton2.Top + radioButton1.Top + radioButton1.Height;
            radioButton4.Top = radioButton3.Top + radioButton1.Top + radioButton1.Height;

            radioButton4.Enabled = false;
            radioButton4.Visible = false;

            for (int pageIdx = 0; pageIdx < tabControl1.TabPages.Count; pageIdx++)
            {
                // clear all edits..
                if (tabControl1.TabPages[pageIdx].Controls.Count > 0)
                {
                    tabControl1.TabPages[pageIdx].Controls[0].Left = 0;
                    tabControl1.TabPages[pageIdx].Controls[0].Top = 0;
                    tabControl1.TabPages[pageIdx].Controls[0].Width =
                        tabControl1.TabPages[pageIdx].ClientRectangle.Width;
                    tabControl1.TabPages[pageIdx].Controls[0].Height =
                        tabControl1.TabPages[pageIdx].ClientRectangle.Height;
                }
            }
        }
        /// <summary>
        /// check hex value or not when int type arg is selected
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private Boolean HexKey(KeyPressEventArgs e)
        {
            if (TabArgType[tabControl1.SelectedIndex] == 1)
            {
                return true;
            }
            string hexstring = "0123456789xXABCDEFabcdef";
            if (hexstring.Contains(e.KeyChar.ToString()))
            {
                return true;
            }
            else if (TabArgType[tabControl1.SelectedIndex] == 2)
            {
                if (e.KeyChar == ',')
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// check if a hex is pressed when a int value requested
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            // hex key
            if (e != null)
            {
                if (!HexKey(e))
                {
                    e.Handled = true;
                }
            }
        }
        /// <summary>
        /// Cancel the arg input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            // cancel data
            this.Hide();
        }
        /// <summary>
        /// check hex value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private Boolean IsHexValue (ref string value)
        {
            if (value.StartsWith("0x"))
            {
                value = value.Substring(2);
                return true;
            }
            if (value.Contains("a"))
            {
                return true;
            }
            if (value.Contains("b"))
            {
                return true;
            }
            if (value.Contains("c"))
            {
                return true;
            }
            if (value.Contains("d"))
            {
                return true;
            }
            if (value.Contains("e"))
            {
                return true;
            }
            if (value.Contains("f"))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Submit all args
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            string Value;
            _Args.Clear();
            bEval = true;
            for (int pageIdx = 0; pageIdx < tabControl1.TabPages.Count; pageIdx++)
            {
                // clear all edits..
                if (tabControl1.TabPages[pageIdx].Controls.Count > 0)
                {
                    Value = tabControl1.TabPages[pageIdx].Controls[0].Text;
                    if (tabControl1.TabPages[pageIdx].Controls[0].Text.Length == 0)
                    {
                        tabControl1.SelectedIndex = pageIdx;
                        break;
                    } else
                    {
                        // check all values...
                        try
                        {

                            if (TabArgType[pageIdx] == 0)
                            {
                                // integer
                                //byte[] bytex = BitConverter.GetBytes(
                                Value = Value.Trim().ToLower();
                                UInt64 num = 0;
                                if (IsHexValue(ref Value))
                                {
                                    // hex value
                                    if (Value.Length > 16)
                                    {
                                        MessageBox.Show("Hex value is too big, over 64bit integer(0xFFFFFFFF FFFFFFFF)");
                                        break;
                                    }
                                    else if (Value.Contains("x"))
                                    {
                                        MessageBox.Show("Invalid Hex value which contain extra 'x'");
                                        break;
                                    }
                                    num = UInt64.Parse(Value, System.Globalization.NumberStyles.HexNumber);
                                } else
                                {
                                    if (Value.Contains("x"))
                                    {
                                        MessageBox.Show("Invalid Hex value which contain extra 'x'");
                                        break;
                                    }
                                    num = UInt64.Parse(Value, System.Globalization.NumberStyles.Integer);
                                }
                                byte[] bytex = Encoding.ASCII.GetBytes(tabControl1.TabPages[pageIdx].Controls[0].Text);                                
                                AcpiMethodArg arg = new AcpiMethodArg();
                                arg.Type = 0;
                                arg.ulValue = num;
                                _Args.Add(arg);
                            }
                            else if (TabArgType[pageIdx] == 1)
                            {                             
                                AcpiMethodArg arg = new AcpiMethodArg();
                                arg.Type = 1;
                                arg.strValue = Value.Trim();
                                _Args.Add(arg);
                            }
                            else if (TabArgType[pageIdx] == 2)
                            {
                                string[] strBytes = tabControl1.TabPages[pageIdx].Controls[0].Text.Split(new char[] { ',' });
                                AcpiMethodArg arg = new AcpiMethodArg();
                                arg.Type = 2;
                                arg.Count = strBytes.Length;
                                arg.bValue = new byte[arg.Count];
                                arg.Count = 0;
                                foreach (string strByte in strBytes)
                                {
                                    string strByteValue = strByte.Trim().ToLower();
                                    strByteValue = strByteValue.Replace(" ", "");
                                    IsHexValue(ref strByteValue);
                                    UInt32 bValue = UInt32.Parse(strByteValue, System.Globalization.NumberStyles.HexNumber);
                                    arg.bValue[arg.Count] = (byte)bValue;
                                    arg.Count++;
                                }
                                _Args.Add(arg);
                            }
                        }
                        catch (Exception ex)
                        {
                            bEval = false;
                            tabControl1.SelectedIndex = pageIdx;
                            MessageBox.Show(ex.Message);
                            break;
                        }
                    }
                }
            }    
            if (bEval)
            {
                this.Hide();
            }
        }
        /// <summary>
        /// select the right arg type - package
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab.Controls.Count > 0)
            {
                //tabControl1.SelectedTab.Controls[0].Text = "";
            }
            TabArgType[tabControl1.SelectedIndex] = 3;
        }
        /// <summary>
        /// select the right arg type - buffer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab.Controls.Count > 0)
            {
                //tabControl1.SelectedTab.Controls[0].Text = "0x00,0x00,...";
            }
            TabArgType[tabControl1.SelectedIndex] = 2;
        }
        /// <summary>
        /// select the right arg type - string
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab.Controls.Count > 0)
            {
                //tabControl1.SelectedTab.Controls[0].Text = "String";
            }
            TabArgType[tabControl1.SelectedIndex] = 1;
        }
        /// <summary>
        /// select the right arg type - integer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab.Controls.Count > 0)
            {
               //tabControl1.SelectedTab.Controls[0].Text = "0x";
            }
            TabArgType[tabControl1.SelectedIndex] = 0;
        }
    }
}
