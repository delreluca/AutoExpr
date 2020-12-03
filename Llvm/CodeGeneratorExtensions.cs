using LLVMSharp;

namespace AutoExpr.Llvm
{
    public static class CodeGeneratorExtensions
    {
        public static LLVMValueRef NatAdd(this CodeGenerator g, LLVMValueRef a, LLVMValueRef b, string name = "")
            => LLVM.BuildAdd(g.Builder, a, b, name);
        public static LLVMValueRef NatMul(this CodeGenerator g, LLVMValueRef a, LLVMValueRef b, string name = "")
            => LLVM.BuildMul(g.Builder, a, b, name);

        public static LLVMValueRef Const(this CodeGenerator g, int val)
            => LLVM.ConstInt(LLVM.Int32Type(), (ulong)val, new LLVMBool(0));

        public static LLVMValueRef Const(this CodeGenerator g, double val)
            => LLVM.ConstReal(LLVM.DoubleType(), val);

        public static LLVMValueRef OffsetF64(this CodeGenerator g, int n) => g.NatMul(g.SizeF64(), g.Const(n));
        public static LLVMValueRef OffsetF64(this CodeGenerator g, LLVMValueRef n) => g.NatMul(g.SizeF64(), LLVM.BuildIntCast(g.Builder, n, LLVM.Int64Type(), ""));

        public static LLVMValueRef SizeF64(this CodeGenerator g) => LLVM.SizeOf(LLVM.DoubleType());

        public static LLVMValueRef Call(this CodeGenerator g, string fn, LLVMValueRef[] args, string name = "")
            => LLVM.BuildCall(g.Builder, g.KnownFunctions[fn], args, name);
    }
}