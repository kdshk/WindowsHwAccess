using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AcpiWin
{
    public partial class MainForm : Form
    {
        private const int BorderSize = 5;
        private AmlDebug debug = new AmlDebug();        
        private int TrewViewWidth = 0;
        private Point MouseDownLocation;
        private bool ButtonDown = false;
        private AcpiLib acpiLib = null;
        private Boolean bRightClickInProgress = false;
        private TreeNode PopupNode = null;
        private ArgInput formArgInput = new ArgInput();    
        private AmlBuilder amlBuilder = new AmlBuilder();
        private AmlMethod amlMethod = null;// new AmlMethod();
        private Dictionary<string, string> EvalData = new Dictionary<string, string>();
        /// <summary>
        /// Main Form constructor
        /// </summary>
        public MainForm()
        {
            IntiEvalData();
            InitializeComponent();
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            InitAppSize();
            //statusStrip1.Height = 200;
            statusStrip1.Items.Add("Offline Mode");
            statusStrip1.Items.Add("");
            statusStrip1.Cursor = DefaultCursor;
            TrewViewWidth = this.ClientSize.Width / 3;
            ResizeLayout();
            acpiLib = new AcpiLib();
            amlBuilder.acpiLib = acpiLib;
            InitializeAcpiObjects();
            //amlMethod = new AmlMethod(acpiLib, "\\___");
            //amlMethod.RunMethod(); // Run method is a internal test code
            //amlMethod = null;
        }
        /// <summary>
        /// On selected event to display the asl code
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void acpiView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // show something
            if (bRightClickInProgress)
            {
                return;
            }
            aslText.Text = e.Node.Name;            
            string strAsl = "";           
            if (acpiLib.GetAslCode((string)e.Node.Tag, ref strAsl))
            {
                aslText.Text += string.Format("\n\n{0}\n", strAsl);
            }            
        }
        /// <summary>
        /// Start Resizing the text and treeview zone
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_MouseDown(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.NoMoveHoriz;
            MouseDownLocation = new Point(e.X, e.Y);
            ButtonDown = true;
            Capture = true;
        }
        /// <summary>
        /// Resize the text and treeview zone
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (ButtonDown)
            {
                TrewViewWidth += e.X - MouseDownLocation.X;
                if (TrewViewWidth < this.ClientSize.Width / 4)
                {
                    TrewViewWidth = this.ClientSize.Width / 4;
                } else if (TrewViewWidth > this.ClientSize.Width / 2)
                {
                    TrewViewWidth = this.ClientSize.Width / 2;
                }
                ResizeLayout();
                MouseDownLocation = new Point(e.X, e.Y);
            }
        }
        /// <summary>
        /// Resize the text and treeview zone finished
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_MouseUp(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.VSplit;
            ButtonDown = false;
            Capture = false;
        }
        /// <summary>
        /// Do layout resize
        /// </summary>
        private void ResizeLayout()
        {
            acpiView.Left = 0;
            acpiView.Top = menuStrip1.Height;
            acpiView.Width = TrewViewWidth;
            acpiView.Height = this.ClientSize.Height - statusStrip1.Height - menuStrip1.Height;
            aslText.Left = TrewViewWidth + BorderSize;
            aslText.Top = menuStrip1.Height;
            aslText.Width = this.ClientSize.Width - TrewViewWidth - BorderSize;
            aslText.Height = this.ClientSize.Height - statusStrip1.Height - menuStrip1.Height;
        }
        /// <summary>
        /// Resize event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Resize(object sender, EventArgs e)
        {
            ResizeLayout();
        }
        /// <summary>
        /// FormClosed event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (acpiLib != null)
            {
                acpiLib.Dispose();
                //del acpiLib;
            }
        }
        /// <summary>
        /// Build the tree view data
        /// </summary>
        /// <param name="root"></param>
        /// <param name="Path"></param>
        private void BuildAcpiObjects(TreeNode root, string Path)
        {
            string names = "";
            int Length = acpiLib.GetNamePath(Path, ref names);
            if (Length > 0)
            {
                //
                // TreeNode node = root.Nodes.Add(Path);
                // 
                int nStart = 0;
                AmlBuilder.AcpiNS acpiNSRoot = amlBuilder.GetCurrentPath();
                while (nStart < names.Length)
                {
                    string Name = names.Substring(nStart, 4);
                    string FullPath = Path + Name;
                    ushort nType = 0;
                    string type = acpiLib.GetTypeStringByName(FullPath, ref nType);
                    AmlBuilder.AcpiNS acpiNs =  amlBuilder.AddChildItem(FullPath, nType);
                    if (Name == "----")
                    {
                        type = "Field";
                        nStart += 4;
                        continue;
                    }
                    TreeNode subNode = root.Nodes.Add(string.Format("{0} [{1}]", Name, type));
                    // Create C# AcpiNS Data
                    
                    // Full Path to Acpi Path
                    string FullNamePath = "\\";
                    int nFullPathStart = 4;
                    while (nFullPathStart < FullPath.Length)
                    {
                        FullNamePath += FullPath.Substring(nFullPathStart, 4);
                        nFullPathStart += 4;
                        if (nFullPathStart < FullPath.Length)
                        {
                            FullNamePath += ".";
                        }
                    }
                    subNode.Tag = FullPath;
                    subNode.Name = FullNamePath;
                    subNode.ToolTipText = type;
                    subNode.ImageIndex = nType;
                    // set the child 
                    amlBuilder.SetCurrentPath(acpiNs);
                    BuildAcpiObjects(subNode, Path + Name);
                    // restore to parent
                    amlBuilder.SetCurrentPath(acpiNSRoot);
                    nStart += 4;
                }
            }
        }
        /// <summary>
        /// Load acpi objects from dll
        /// </summary>
        private void InitializeAcpiObjects()
        {
            if (acpiLib == null)
            {
                return;
            }
            if (acpiLib.DriverLoaded())
            {
                statusStrip1.Items[0].Text = "Online Mode";


            } else
            {
                statusStrip1.Items[0].Text = "Offline Mode";
                //statusStrip1.Items.Add("Offline Mode");
                //statusStrip1.Text = "Offline Mode";
            }
            TreeNode root = acpiView.Nodes.Add("Acpi Namespace");
            //TreeNode root = root.Add("Acpi Namespace");
            TreeNode acpi_root = root.Nodes.Add("\\___ (Uninitialized)");
            acpi_root.Tag = "\\___";
            acpi_root.Name = "\\___";
            BuildAcpiObjects(acpi_root, "\\___");
            //acpi_root.Expand();
            root.Expand();
            acpi_root.Expand();
        }
        /// <summary>
        /// Right click notification
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Method_Notify_Click(object sender, EventArgs e)
        {
            ToolStripItem clickedItem = sender as ToolStripItem;
            // your code here

            acpiView.SelectedNode = PopupNode;
            // your code here
            // do nothing
            //if (acpiLib != null && acpiLib.DriverLoaded())
            aslText.Text = PopupNode.Name;
            {
                // show diagnostics for args
                formArgInput.SetNotifyInput();
                formArgInput.ShowDialog();
                
                if (formArgInput.GetEvalState())
                {
                    List<AcpiMethodArg> methodargs = formArgInput.Args;
                    if (methodargs != null && methodargs.Count == 1)
                    {
                        if (methodargs[0].Type == 0)
                        {
                            if (acpiLib.Notify((string)PopupNode.Tag, methodargs[0].ulValue))
                            {
                                aslText.Text += "\n\nNotfiy succesfully";
                            } else
                            {
                                aslText.Text += "\n\nNotfiy method call failed";
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Acpi evaluation popup menu handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Method_Eval_Click(object sender, EventArgs e)
        {
            ToolStripItem clickedItem = sender as ToolStripItem;
            // your code here

            acpiView.SelectedNode = PopupNode;
            // your code here
            // do nothing
            //if (acpiLib != null && acpiLib.DriverLoaded())
            aslText.Text = PopupNode.Name;
            if (!acpiLib.DriverLoaded())
            {
                aslText.AppendText("\n\nDriver is not loaded");
                return;
            }
            {
                //acpiLib.GetEvalResult(PopupNode.Tag);
                string strAsl = "";
                if (PopupNode.ToolTipText == "Method")
                {
                    UInt64 args = 8;
                    if (acpiLib.GetMethodArgCount((string)PopupNode.Tag, ref args) && args < 8)
                    {
                        // it's a method, now do eval input or eval
                        if (args == 0)
                        {
                            if (acpiLib.GetEvalResult((string)PopupNode.Tag, ref strAsl))
                            {
                                aslText.Text += string.Format("\n\n{0}", strAsl);
                            }
                        }
                        else
                        {
                            // input args
                            if (PopupNode.Name == "\\_OSI")
                            {
                                formArgInput.SetInputString();
                                formArgInput.ShowDialog();
                            }
                            else
                            {
                                formArgInput.SetArgCount((int)args);
                                formArgInput.ShowDialog();
                            }

                            if (formArgInput.GetEvalState())
                            {
                                // run command
                                List<AcpiMethodArg> methodargs = formArgInput.Args;
                                if (methodargs != null)
                                {
                                    IntPtr pArg = IntPtr.Zero;
                                    for (int iIndex = 0; iIndex < methodargs.Count; iIndex++)
                                    {
                                        switch (methodargs[iIndex].Type)
                                        {
                                            case 0:
                                                pArg = acpiLib.ArgPutUInt64(pArg, methodargs[iIndex].ulValue);
                                                break;
                                            case 1:
                                                pArg = acpiLib.ArgPutString(pArg, methodargs[iIndex].strValue);
                                                break;
                                            case 2:
                                                pArg = acpiLib.ArgPutBuffer(pArg, methodargs[iIndex].bValue);
                                                break;
                                        }
                                        if (pArg == IntPtr.Zero)
                                        {
                                            MessageBox.Show("Failed to create Acpi Method Args");
                                            return;
                                        }
                                    }
                                    string strValue = "";
                                    if (acpiLib.GetEvalArgResult((string)PopupNode.Tag, pArg, ref strValue))
                                    {
                                        aslText.Text += string.Format("\n\n{0}", strValue);
                                    } else
                                    {
                                        aslText.Text += "Bad";
                                    }
                                    acpiLib.FreeArg(pArg);
                                }
                            }
                        }
                    }
                    // Check the args...
                }
                else
                {
                    //if (acpiLib.GetValue ())
                    if (acpiLib.GetEvalResult((string)PopupNode.Tag, ref strAsl))
                    {
                        aslText.Text += string.Format("\n\n{0}", strAsl);
                    }
                    string FullPath = (string)PopupNode.Tag;
                    //AcpiData amlData = new AcpiData(
                    //    acpiLib,
                    //    FullPath.Substring(0, FullPath.Length - 4),
                    //    FullPath.Substring(FullPath.Length - 4));
                }
            }
        }
        /// <summary>
        /// Popup evaluation menu cancelled
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Method_Eval_Cancel(object sender, EventArgs e)
        {
            ToolStripItem clickedItem = sender as ToolStripItem;
            
        }
        /// <summary>
        /// Initialize evaluation data t show up supported popup menu
        /// </summary>
        private void IntiEvalData()
        {
            EvalData["Method"] = "Method";
            EvalData["Integer"] = "Integer";
            EvalData["Buffer"] = "Buffer";
            EvalData["Package"] = "Package";
            EvalData["String"] = "String";
            EvalData["Field Unit"] = "Field Unit";
        }
        /// <summary>
        /// Create the popup menu on treeview node click
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private Boolean CreatePopMenu(TreeNodeMouseClickEventArgs e)
        {
            try
            {
                if (EvalData[e.Node.ToolTipText] != null)
                {
                    //statusStrip1.Items[0].Text = EvalData[e.Node.ToolTipText];
                    return true;
                }
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                statusStrip1.Items[1].Text = "Not support Acpi Object Type for Evaluate";
            }
            catch (Exception ex)
            {
                statusStrip1.Items[1].Text = "Err Message: " + ex.Message;
            }
            return false;
        }
        /// <summary>
        /// Tree view node click handle, display the information in text zone
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void acpiView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right )
            {
                PopupNode = e.Node;
                if (CreatePopMenu(e))
                {
                    acpiView.SelectedNode = e.Node;
                    {
                        bRightClickInProgress = true;
                        ContextMenuStrip contexMenu = new ContextMenuStrip();
                        contexMenu.Items.Add(string.Format("Eval {0} {1}", e.Node.ToolTipText, e.Node.Name));
                        contexMenu.Show(acpiView, new Point(e.X, e.Y));
                        contexMenu.ItemClicked += new ToolStripItemClickedEventHandler(
                            Method_Eval_Click);
                        contexMenu.Closed += new ToolStripDropDownClosedEventHandler(Method_Eval_Cancel);
                        bRightClickInProgress = false;
                    }
                }
                else if (e.Node.ToolTipText.Contains("Device") ||
                      e.Node.ToolTipText.Contains("Processor") ||
                      e.Node.ToolTipText.Contains("Thermal Zone"))
                {
                    acpiView.SelectedNode = e.Node;
                    bRightClickInProgress = true;
                    ContextMenuStrip contexMenu = new ContextMenuStrip();
                    contexMenu.Items.Add(string.Format("Notify {0} {1}", e.Node.ToolTipText, e.Node.Name));
                    contexMenu.Show(acpiView, new Point(e.X, e.Y));
                    contexMenu.ItemClicked += new ToolStripItemClickedEventHandler(
                        Method_Notify_Click);
                    contexMenu.Closed += new ToolStripDropDownClosedEventHandler(Method_Eval_Cancel);
                    bRightClickInProgress = false;
                }                
            }
        }
        /// <summary>
        /// initialize the size information
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

            AppX = AppX * 4 / 5;
            AppY = AppY * 4 / 5;
            //this.Width = AppX;
            //this.Height = AppY;
            this.Size = new Size(AppX , AppY );
        }
        /// <summary>
        /// check if key is a hex value
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private Boolean HexKey(KeyPressEventArgs e)
        {
            string hexstring = "0123456789xXABCDEFabcdef";
            if (hexstring.Contains(e.KeyChar.ToString()))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// text zone key press handler, do nothing 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aslText_KeyPress(object sender, KeyPressEventArgs e)
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
        /// Pass key press to asl text key up handler to engage debug view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void acpiView_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F12 || e.KeyCode == Keys.F10)
            {
                aslText_KeyUp(sender, e);
            }
        }
        /// <summary>
        /// To launch the debug view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aslText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F10)
            {
                if (acpiView.SelectedNode.ToolTipText.Equals("Method"))
                {
                    List<AcpiData> method_data = null;
                    UInt64 args = 0;
                    if (acpiLib.GetMethodArgCount((string)acpiView.SelectedNode.Tag, ref args))
                    {      
                        if (args == 0)
                        {
                            method_data = new List<AcpiData>();
                        } else
                        {
                            formArgInput.SetArgCount((int)args);
                            formArgInput.ShowDialog();
                            if (formArgInput.GetEvalState())
                            {
                                List<AcpiMethodArg> methodargs = formArgInput.Args;
                                method_data = new List<AcpiData>();
                                foreach (AcpiMethodArg arg in methodargs)
                                {
                                    AcpiData amlData = new AcpiData(arg);
                                    method_data.Add(amlData);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Canceled method simulation");
                                return;
                            }
                        }
                    }
                    if (method_data != null)
                    {
                        DebugViewForm debugViewForm = new DebugViewForm();
                        debugViewForm.acpiLib = acpiLib;
                        debugViewForm.MethodArgs = method_data;
                        debugViewForm.strAcpiPath = (string)acpiView.SelectedNode.Tag;
                        debugViewForm.strAslCode = (string)aslText.Text;
                        debugViewForm.ShowDialog();
                    }
                }             
            }
            else if (e.KeyCode == Keys.F12)
            {

                ResizeLayout();
            }            
        }
        /// <summary>
        /// Get acpi name space for selected acpi node
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        private TreeNode GetAcpiNS(TreeNodeCollection nodes, string Name)
        {
            TreeNode node = null;
            TreeNodeCollection treeNodes = nodes;
            if (treeNodes == null)
            {
                treeNodes = acpiView.Nodes;
                
            }
            if (treeNodes.Count > 0)
            {
                foreach (TreeNode treeNode in treeNodes) {
                    string Path =(string) treeNode.Tag;
                    if (Path != null)
                    {
                        Path = Path.Substring(Path.Length - 4);
                        if (Path == Name)
                        {
                            node = treeNode;
                            break;
                        }
                    }
                }
                if (node == null)
                {
                    // go through subs
                    foreach (TreeNode treeNode in treeNodes)
                    {
                        if (treeNode.Nodes != null)
                        {
                            node = GetAcpiNS(treeNode.Nodes,Name);
                            if (node != null)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return node;
        }
        /// <summary>
        /// find the name space
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // open a string for find 
            FindForm find = new FindForm();
            find.ShowDialog();
            string value = find.GetString();
            // find the method acpi now
            TreeNode node = GetAcpiNS(null, value);
            if (node != null)
            {
                // select it
                acpiView.SelectedNode = node;
            }
        }
        /// <summary>
        /// acpi view key press pass to text key press
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void acpiView_KeyPress(object sender, KeyPressEventArgs e)
        {
            aslText_KeyPress(sender, e);
        }
    }
}
