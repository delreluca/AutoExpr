using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LLVMSharp;

namespace AutoExpr
{
    public class LlvmExecutor
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void JitEntryPoint();

        private CodeGenerator Generator {get;}

        public LlvmExecutor(CodeGenerator generator)
        {
            Generator = generator;
        }

        ///<summary>Runs the LLVM IR by compiling it and using the given function pointers</summary>
        public void Run(IReadOnlyDictionary<string, IntPtr> env)
        {
            LLVM.BuildRetVoid(Generator.Builder);

            if (LLVM.VerifyModule(Generator.Module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != new LLVMBool(0))
            {
                Console.WriteLine($"Error in State.Module: {error}");
            }

            LLVM.LinkInMCJIT();

            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            var options = new LLVMMCJITCompilerOptions { NoFramePointerElim = 1 };
            LLVM.InitializeMCJITCompilerOptions(options);
            if (LLVM.CreateMCJITCompilerForModule(out var engine, Generator.Module, options, out error) != new LLVMBool(0))
            {
                Console.WriteLine($"JIT init error: {error}");
            }
            
            foreach (var fn in env)
            {
                LLVM.AddGlobalMapping(engine, Generator.KnownFunctions[fn.Key], fn.Value);
            }

            var jitty = (JitEntryPoint)Marshal.GetDelegateForFunctionPointer(LLVM.GetPointerToGlobal(engine, Generator.EntryPoint), typeof(JitEntryPoint));

            //LLVM.DumpModule(Generator.Module);
            var s = Stopwatch.StartNew();
            Console.WriteLine("Call into JITted code...");
            jitty();
            var t = s.ElapsedMilliseconds;
            Console.WriteLine("Returned after {0} ms", t);

            LLVM.DisposeBuilder(Generator.Builder);
            LLVM.DisposeExecutionEngine(engine);
        }
    }
}