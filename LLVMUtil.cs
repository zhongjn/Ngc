using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ngc
{
    public static class LLVMUtil
    {
        public static string ModuleToString(LLVMModuleRef module)
        {
            var pStr = LLVM.PrintModuleToString(module);
            return Marshal.PtrToStringAnsi(pStr);
        }
    }
}
