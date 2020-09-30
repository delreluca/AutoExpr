using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LLVMSharp;

namespace AutoExpr
{

    public class LlvmState
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void JitEntryPoint();

        public LLVMValueRef Malloc { get; }

        public LLVMValueRef Free;

        public LLVMValueRef Set;

        public LLVMValueRef Zro;

        public LLVMValueRef Add;

        public LLVMValueRef Mul;

        public LLVMValueRef Exp;

        public LLVMValueRef Cpy;

        public LLVMValueRef NPaths;

        public LLVMValueRef WorkingMemory;

        public Dictionary<string, LLVMValueRef> VarMemory;

        public LLVMModuleRef Module { get; }

        public LLVMBuilderRef Builder { get; }

        public LLVMValueRef EntryPoint { get; private set; }

        private int Counter = 0;

        public int GetId() => Counter++;

        public void Run(NativeLibEnv env)
        {
            LLVM.BuildRetVoid(Builder);

            if (LLVM.VerifyModule(Module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != new LLVMBool(0))
            {
                Console.WriteLine($"Error in module: {error}");
            }
            else
            {
                Console.WriteLine("Module verified.");
            }

            LLVM.LinkInMCJIT();

            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();

            var options = new LLVMMCJITCompilerOptions { NoFramePointerElim = 1 };
            LLVM.InitializeMCJITCompilerOptions(options);
            if (LLVM.CreateMCJITCompilerForModule(out var engine, Module, options, out error) != new LLVMBool(0))
            {
                Console.WriteLine($"Error: {error}");
            }
            else
            {
                Console.WriteLine("JIT created.");
            }

            LLVM.AddGlobalMapping(engine, Malloc, env.FunctionPointers["ippsMalloc_64f"]);
            LLVM.AddGlobalMapping(engine, Free, env.FunctionPointers["ippsFree"]);
            LLVM.AddGlobalMapping(engine, Zro, env.FunctionPointers["ippsZero_64f"]);
            LLVM.AddGlobalMapping(engine, Set, env.FunctionPointers["ippsSet_64f"]);
            LLVM.AddGlobalMapping(engine, Cpy, env.FunctionPointers["ippsCopy_64f"]);
            LLVM.AddGlobalMapping(engine, Add, env.FunctionPointers["ippsAdd_64f_I"]);
            LLVM.AddGlobalMapping(engine, Mul, env.FunctionPointers["ippsMul_64f_I"]);
            LLVM.AddGlobalMapping(engine, Exp, env.FunctionPointers["ippsExp_64f_I"]);

            var jitty = (JitEntryPoint)Marshal.GetDelegateForFunctionPointer(LLVM.GetPointerToGlobal(engine, EntryPoint), typeof(JitEntryPoint));

            LLVM.DumpModule(Module);
            Console.WriteLine("Running...");
            jitty();
            Console.WriteLine("Returned.");

            LLVM.DisposeBuilder(Builder);
            LLVM.DisposeExecutionEngine(engine);
        }

        private LLVMValueRef GetIppFn(string name, LLVMTypeRef[] argumentTypes, LLVMTypeRef? returnType = null)
        {
            var fnType = LLVM.FunctionType(returnType ?? LLVM.Int64Type(), argumentTypes, false);
            var fn = LLVM.AddFunction(Module, name, fnType);
            LLVM.SetLinkage(fn, LLVMLinkage.LLVMExternalLinkage);
            return fn;
        }

        public void InitBuilder()
        {
            EntryPoint = LLVM.AddFunction(Module, "FUNC", LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[0], false));
            var block = LLVM.AppendBasicBlock(EntryPoint, "ENTRY");
            LLVM.PositionBuilderAtEnd(Builder, block);
        }

        public LlvmState(IntPtr workingMemory, IReadOnlyDictionary<string, IntPtr> varMemory, LLVMModuleRef module, LLVMBuilderRef builder, Dictionary<string, LLVMValueRef> namedValues, Stack<LLVMValueRef> valueStack)
        {
            this.Module = module;
            this.Builder = builder;
            InitBuilder();
            Malloc = GetIppFn("ippsMalloc_64f", new[] { LLVM.Int64Type() });
            Free = GetIppFn("ippsFree", new[] { LLVM.Int64Type() }, LLVM.VoidType());
            // IppStatus ippsSet_64f(Ipp64f val, Ipp64f* pDst, int len);
            Set = GetIppFn("ippsSet_64f", new[] { LLVM.DoubleType(), LLVM.Int64Type(), LLVM.Int64Type() });
            // IppStatus ippsZero_64f(Ipp64f* pDst, int len)
            Zro = GetIppFn("ippsZero_64f", new[] { LLVM.Int64Type(), LLVM.Int64Type() });
            // IppStatus ippsExp_64f_I(Ipp64f* pSrcDst, int len)
            Exp = GetIppFn("ippsExp_64f_I", new[] { LLVM.Int64Type(), LLVM.Int64Type() });
            // IppStatus ippsCopy_64f(const Ipp64f* pSrc, Ipp64f* pDst, int len);
            Cpy = GetIppFn("ippsCopy_64f", new[] { LLVM.Int64Type(), LLVM.Int64Type(), LLVM.Int64Type() });
            // IppStatus ippsAdd_64f_I(const Ipp64f* pSrc, Ipp64f* pSrcDst, int len);
            Add = GetIppFn("ippsAdd_64f_I", new[] { LLVM.Int64Type(), LLVM.Int64Type(), LLVM.Int64Type() });
            // IppStatus ippsMul_64f_I(const Ipp64f* pSrc, Ipp64f* pSrcDst, int len);
            Mul = GetIppFn("ippsMul_64f_I", new[] { LLVM.Int64Type(), LLVM.Int64Type(), LLVM.Int64Type() });
            NPaths = LLVM.ConstInt(LLVM.Int64Type(), 10000, new LLVMBool(0));
            WorkingMemory = LLVM.ConstInt(LLVM.Int64Type(), (ulong)workingMemory.ToInt64(), new LLVMBool(0));
            VarMemory = varMemory.Select(kvp => (kvp.Key, LLVM.ConstInt(LLVM.Int64Type(), (ulong)kvp.Value.ToInt64(), new LLVMBool(0)))).ToDictionary(t => t.Key, t => t.Item2);

        }

        public LlvmState(IntPtr workingMemory, IReadOnlyDictionary<string, IntPtr> varMemory) : this(workingMemory, varMemory, LLVM.ModuleCreateWithName("CodeGen"), LLVM.CreateBuilder(), new Dictionary<string, LLVMValueRef>(), new Stack<LLVMValueRef>())
        {

        }


    }

    public class LlvmVisitor : IExprVisitor<LlvmState, LlvmState>
    {
        public LlvmState VisitAdd(Add add, LlvmState state)
        {
            var tmp = LLVM.BuildCall(state.Builder, state.Malloc, new LLVMValueRef[] { state.NPaths }, $"tmpsum{state.GetId()}");

            LLVM.BuildCall(state.Builder, state.Zro, new LLVMValueRef[] { tmp, state.NPaths }, "");

            foreach (var summand in add.Children)
            {
                summand.Visit(this, state);
                LLVM.BuildCall(state.Builder, state.Add, new LLVMValueRef[] { state.WorkingMemory, tmp, state.NPaths }, "");
            }

            LLVM.BuildCall(state.Builder, state.Cpy, new LLVMValueRef[] { tmp, state.WorkingMemory, state.NPaths }, "");

            LLVM.BuildCall(state.Builder, state.Free, new[] { tmp }, "");

            return state;
        }

        public LlvmState VisitMul(Mul mul, LlvmState state)
        {
            var tmpProd = LLVM.BuildCall(state.Builder, state.Malloc, new LLVMValueRef[] { state.NPaths }, $"tmpprd{state.GetId()}");

            LLVM.BuildCall(state.Builder, state.Set, new LLVMValueRef[] { LLVM.ConstReal(LLVM.DoubleType(), 1), tmpProd, state.NPaths }, "");

            foreach (var factor in mul.Children)
            {
                factor.Visit(this, state);
                LLVM.BuildCall(state.Builder, state.Mul, new LLVMValueRef[] { state.WorkingMemory, tmpProd, state.NPaths }, "");
            }

            LLVM.BuildCall(state.Builder, state.Cpy, new LLVMValueRef[] { tmpProd, state.WorkingMemory, state.NPaths }, "");

            LLVM.BuildCall(state.Builder, state.Free, new[] { tmpProd }, "");

            return state;
        }

        public LlvmState VisitExp(Exp exp, LlvmState state)
        {
            exp.Exponent.Visit(this, state);
            LLVM.BuildCall(state.Builder, state.Exp, new LLVMValueRef[] { state.WorkingMemory, state.NPaths }, "");
            return state;
        }

        public LlvmState VisitConst(Const @const, LlvmState state)
        {
            LLVM.BuildCall(state.Builder, state.Set, new LLVMValueRef[] { LLVM.ConstReal(LLVM.DoubleType(), @const.Value), state.WorkingMemory, state.NPaths }, "");
            return state;
        }

        public LlvmState VisitVar(Var constVector, LlvmState state)
        {
            LLVM.BuildCall(state.Builder, state.Cpy, new [] { state.VarMemory[constVector.Name], state.WorkingMemory, state.NPaths}, "");
            return state;
        }
    }
}