using LLVMSharp;
using AutoExpr.Llvm;
using AutoExpr.Ipp;

namespace AutoExpr.IppLlvmBindings
{
    public static class CodeGeneratorExtensions
    {
        public static LLVMValueRef IppAdd(this CodeGenerator g, LLVMValueRef a, LLVMValueRef b, string name = "")
            => g.Call(FunctionNames.Add64FI, new[] { a, b, g.NPaths }, name);

        public static LLVMValueRef IppMul(this CodeGenerator g, LLVMValueRef a, LLVMValueRef b, string name = "")
            => g.Call(FunctionNames.Mul64FI, new[] { a, b, g.NPaths }, name);

        public static LLVMValueRef IppExp(this CodeGenerator g, LLVMValueRef a, string name = "")
            => g.Call(FunctionNames.Exp64FI, new[] { a, g.NPaths }, name);

        public static LLVMValueRef IppCpy(this CodeGenerator g, LLVMValueRef a, LLVMValueRef b, string name = "", LLVMValueRef? length = null)
            => g.Call(FunctionNames.Copy64F, new[] { a, b, length ?? g.NPaths }, name);

        public static LLVMValueRef IppSet(this CodeGenerator g, double value, LLVMValueRef vector, string name = "", LLVMValueRef? length = null) =>
            value == 0.0 ? g.Call(FunctionNames.Zero64F,
                                new[] { vector, length ?? g.NPaths },
                                name) : g.Call(FunctionNames.Set64F, new[] { g.Const(value), vector, length ?? g.NPaths }, name);

        public static IppMallocHandle Allocate(this CodeGenerator g, LLVMValueRef count, string name = "")
            => new IppMallocHandle(g, g.Call(FunctionNames.Malloc64F, new[] { count }, name));
    }
}