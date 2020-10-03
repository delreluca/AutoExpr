using System.Collections.Generic;
using LLVMSharp;

namespace AutoExpr.Ipp
{
    public static class LlvmBindings
    {
        private static (string, LLVMValueRef) BuildFunctionEntry(LLVMModuleRef module, string name, LLVMTypeRef[] argumentTypes, LLVMTypeRef? returnType = null)
        {
            var fn = LLVM.AddFunction(module, name, LLVM.FunctionType(returnType ?? LLVM.Int64Type(), argumentTypes, false));
            LLVM.SetLinkage(fn, LLVMLinkage.LLVMExternalLinkage);
            return (name, fn);
        }

        public static IEnumerable<(string, LLVMValueRef)> BuildFunctions(LLVMModuleRef module)
        {
            yield return BuildFunctionEntry(module, FunctionNames.Malloc64F, new[] { LLVM.Int64Type() });
            yield return BuildFunctionEntry(module, FunctionNames.Free, new[] { LLVM.Int64Type() }, LLVM.VoidType());
            yield return BuildFunctionEntry(module, FunctionNames.Set64F, new[] { LLVM.DoubleType(), LLVM.Int64Type(), LLVM.Int64Type() });
            yield return BuildFunctionEntry(module, FunctionNames.Zero64F, new[] { LLVM.Int64Type(), LLVM.Int64Type() });
            yield return BuildFunctionEntry(module, FunctionNames.Exp64FI, new[] { LLVM.Int64Type(), LLVM.Int64Type() });
            yield return BuildFunctionEntry(module, FunctionNames.Copy64F, new[] { LLVM.Int64Type(), LLVM.Int64Type(), LLVM.Int64Type() });
            yield return BuildFunctionEntry(module, FunctionNames.Add64FI, new[] { LLVM.Int64Type(), LLVM.Int64Type(), LLVM.Int64Type() });
            yield return BuildFunctionEntry(module, FunctionNames.Mul64FI, new[] { LLVM.Int64Type(), LLVM.Int64Type(), LLVM.Int64Type() });
            yield break;
        }
    }
}