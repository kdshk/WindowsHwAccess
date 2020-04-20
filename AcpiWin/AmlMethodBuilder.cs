using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcpiWin
{
    class AmlMethodBuilder
    {
        // build the method
        class AmlMethodInfo
        {
            string Method;
            public int ArgCount;
            public AmlMethodInfo(string Method, int ArgCount)
            {
                if (Method.Contains(".")) {
                    this.Method = Method.Replace(".", ""); 
                }
                else
                {
                    this.Method = Method;
                }
                //Log.Logs(this.Method);
                this.ArgCount = ArgCount;
            }

            public override bool Equals(object obj)
            {
                return Method.Equals(((AmlMethodInfo)obj).Method);
            }
            public bool Equals(string obj)
            {
                return Method.Equals(obj);
            }
        }
        private List<AmlMethodInfo> amlMethods;
        public AmlMethodBuilder ()
        {
            amlMethods = new List<AmlMethodInfo>();
            MethodCollector("", "_OSI", 1);
        }
        public void AddMethodCollect(byte[] amlCode)
        {
            if (amlCode == null)
            {
                return;
            }
            AmlDisassemble disassemble = new AmlDisassemble();
            disassemble.MethodCollector = MethodCollector;
            string aslCode = disassemble.DecodeAml(amlCode);
        }

        private AmlMethodInfo FindMethod (string path, string name)
        {
            // get all path...
            // name of seg
            // find all possible method
            int index = -1;
            //if (name == "HIWC")
            //{
            //    index = -1;
            //}            
            if (name.Length == 0)
            {
                index = amlMethods.IndexOf(new AmlMethodInfo(path, 0));
                if (index == -1)
                {
                    return null;
                }
                return amlMethods[index];
            }
            string fullpath = path + name;
           
            try
            {
                while (path.Length >= 0)
                {
                    index = amlMethods.IndexOf(new AmlMethodInfo(fullpath, 0));
                    if (index < 0)
                    {
                        if (path.Length == 0)
                        {
                            return null;
                        }
                        path = path.Substring(0, path.Length - 4);
                        fullpath = path + name;
                    }
                    else
                    {
                        break;
                    }
                }
                if (index == -1)
                {
                    return null;
                }
                return amlMethods[index];
            } catch (Exception e)
            {
                Log.Logs(e.Message);
            }
            return null;
        }

        public int GetArgCount(string path, string name)
        {
            AmlMethodInfo amlMethodInfo = null;
            try
            {
                path = path.Replace(".", "");
                name = name.Replace(".", "");
                if (path.StartsWith("\\"))
                {
                    path = path.Substring(1);
                }
                // build the path and name
                if (name.StartsWith("\\"))
                {
                    // full path
                    path = name.Substring(1).Replace(".", "");
                    amlMethodInfo = FindMethod(path, "");
                }
                else if (name.StartsWith("^"))
                {
                    for (int idx = 0; idx < name.Length;)
                    {
                        if (name[idx] == '^')
                        {
                            // up to one level
                            path = path.Substring(0, path.Length - 4);
                            name = name.Substring(1);
                        }
                        else
                        {
                            break;
                        }
                    }
                    amlMethodInfo = FindMethod(path, name);
                }
                else
                {
                    amlMethodInfo = FindMethod(path, name);
                }
            } catch(Exception e)
            {
                Log.Logs(e.Message);
            }
            if (amlMethodInfo != null)
            {
                return amlMethodInfo.ArgCount;
            }
            return -1;
        }

        private void MethodCollector(string path, string method, int argcount)
        {
            if (method[0] == '\\')
            {
                // skip the path
                method = method.Substring(1);
                AmlMethodInfo amlMethodInfo = new AmlMethodInfo(method, argcount);
                amlMethods.Add(amlMethodInfo);
            } else
            {
                for (int idx = 0; idx < method.Length; )
                {
                    if (method[idx] == '^')
                    {
                        // up to one level
                        path = path.Substring(0, path.Length - 4);
                        method = method.Substring(1);
                    } else
                    {
                        break;
                    }
                }
                if (path.Contains("^"))
                {
                    AmlMethodInfo amlMethodInfo = new AmlMethodInfo(path + method, argcount);
                    amlMethods.Add(amlMethodInfo);
                }
                else if (path.StartsWith("\\"))
                {
                    path = path.Substring(1);
                    AmlMethodInfo amlMethodInfo = new AmlMethodInfo(path + method, argcount);
                    amlMethods.Add(amlMethodInfo);
                }
                else
                {
                    AmlMethodInfo amlMethodInfo = new AmlMethodInfo(path + method, argcount);
                    amlMethods.Add(amlMethodInfo);
                }
                //System.Diagnostics.Debug.WriteLine(path + method);               
            }            
        }
    }
}
