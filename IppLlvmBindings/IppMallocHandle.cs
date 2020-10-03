using System;
using AutoExpr.Ipp;
using AutoExpr.Llvm;
using LLVMSharp;

namespace AutoExpr.IppLlvmBindings
{
    public class IppMallocHandle : IDisposable
    {
        private CodeGenerator Generator { get; }

        private bool Disposed { get; set; } = false;

        public LLVMValueRef Pointer { get; }

        public IppMallocHandle(CodeGenerator generator, LLVMValueRef pointer)
        {
            Generator = generator;
            Pointer = pointer;
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                Generator.Call(FunctionNames.Free, new[] { Pointer }, "");
                Disposed = true;
            }
        }
    }
}