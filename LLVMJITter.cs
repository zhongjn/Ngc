using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ngc
{
    public class LLVMJITter
    {
        public delegate int Main();

        public Main Compile(LLVMModuleRef module)
        {
            string error = null;

            if (LLVM.VerifyModule(module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out error))
            {
                throw new LLVMJITterException(error);
            }

            LLVM.LinkInMCJIT();

            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            LLVMMCJITCompilerOptions options = new LLVMMCJITCompilerOptions() { OptLevel = (int)LLVMCodeGenOptLevel.LLVMCodeGenLevelAggressive };
            LLVM.InitializeMCJITCompilerOptions(options);
            if (LLVM.CreateMCJITCompilerForModule(out var ee, module, options, out error))
            {
                throw new LLVMJITterException(error);
            }

            var fp = LLVM.GetPointerToGlobal(ee, LLVM.GetNamedFunction(module, "main"));
            var main = (Main)Marshal.GetDelegateForFunctionPointer(fp, typeof(Main));

            return main;
        }

        public class LLVMJITterException : Exception
        {
            public LLVMJITterException(string msg) : base(msg)
            {

            }
        }
    }
}
