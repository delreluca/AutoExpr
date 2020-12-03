using System;
using System.Collections.Generic;
using System.Linq;
using LLVMSharp;

namespace AutoExpr
{
    public class CodeGenerator
    {
        public IReadOnlyDictionary<string, LLVMValueRef> KnownFunctions { get; }

        public LLVMValueRef NPaths;

        public LLVMValueRef ValueMemory;

        public LLVMValueRef GradientMemory;

        public Dictionary<string, LLVMValueRef> VarMemory;

        public Dictionary<string, double> VariableSeeds;

        public LLVMModuleRef Module { get; }

        public LLVMBuilderRef Builder { get; }

        public LLVMValueRef EntryPoint { get; private set; }

        public void InitBuilder()
        {
            EntryPoint = LLVM.AddFunction(Module, "FUNC", LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[0], false));
            var block = LLVM.AppendBasicBlock(EntryPoint, "ENTRY");
            LLVM.PositionBuilderAtEnd(Builder, block);
        }

        public CodeGenerator(IntPtr valueMemory, IReadOnlyDictionary<string, (IntPtr, double)> varMemory, IntPtr gradientMemory, ulong nPaths, Func<LLVMModuleRef, IEnumerable<(string, LLVMValueRef)>> functionIntroducer)
        {
            Module = LLVM.ModuleCreateWithName("CodeGen");
            Builder = LLVM.CreateBuilder();

            InitBuilder();
            KnownFunctions = functionIntroducer(Module).ToDictionary(p => p.Item1, p => p.Item2);


            NPaths = LLVM.ConstInt(LLVM.Int32Type(), nPaths, new LLVMBool(0));
            ValueMemory = LLVM.ConstInt(LLVM.Int64Type(), (ulong)valueMemory.ToInt64(), new LLVMBool(0));
            GradientMemory = LLVM.ConstInt(LLVM.Int64Type(), (ulong)gradientMemory.ToInt64(), new LLVMBool(0));
            VarMemory = varMemory.Select(kvp => (kvp.Key, LLVM.ConstInt(LLVM.Int64Type(), (ulong)kvp.Value.Item1.ToInt64(), new LLVMBool(0)))).ToDictionary(t => t.Key, t => t.Item2);
            VariableSeeds = varMemory.Select(kvp => (kvp.Key, kvp.Value.Item2)).ToDictionary(kvp => kvp.Key, kvp => kvp.Item2);
        }
    }
}