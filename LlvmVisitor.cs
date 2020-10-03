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

        public LLVMValueRef ValueMemory;

        public LLVMValueRef GradientMemory;

        public Dictionary<string, LLVMValueRef> VarMemory;

        public Dictionary<string, double> SeedMemory;

        public LLVMModuleRef Module { get; }

        public LLVMBuilderRef Builder { get; }

        public LLVMValueRef EntryPoint { get; private set; }

        public class MallocDisposer : IDisposable
        {
            private LlvmState state1;
            private LLVMValueRef ptr1;

            public MallocDisposer(LlvmState state, LLVMValueRef ptr)
            {
                state1 = state;
                ptr1 = ptr;
            }

            public void Dispose()
            {
                state1.Call(state1.Free, new[] { ptr1 }, "");
            }

            public LLVMValueRef Pointer => ptr1;
        }

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

        public LLVMValueRef LAdd(LLVMValueRef a, LLVMValueRef b, string name = "") => LLVM.BuildAdd(Builder, a, b, name);
        public LLVMValueRef LMul(LLVMValueRef a, LLVMValueRef b, string name = "") => LLVM.BuildMul(Builder, a, b, name);

        public LLVMValueRef Const(int val) => LLVM.ConstInt(LLVM.Int64Type(), (ulong)val, new LLVMBool(0));

        public LLVMValueRef Const(double val) => LLVM.ConstReal(LLVM.DoubleType(), val);

        public LLVMValueRef OffsetF64(int n) => LMul(SizeF64(), Const(n));
        public LLVMValueRef OffsetF64(LLVMValueRef n) => LMul(SizeF64(), n);

        public LLVMValueRef SizeF64() => LLVM.SizeOf(LLVM.DoubleType());

        public LLVMValueRef IppAdd(LLVMValueRef a, LLVMValueRef b, string name = "") => Call(Add, new[] { a, b, NPaths }, name);
        public LLVMValueRef IppMul(LLVMValueRef a, LLVMValueRef b, string name = "") => Call(Mul, new[] { a, b, NPaths }, name);
        public LLVMValueRef IppExp(LLVMValueRef a, string name = "") => Call(Exp, new[] { a, NPaths }, name);

        public LLVMValueRef IppCpy(LLVMValueRef a, LLVMValueRef b, string name = "", LLVMValueRef? length = null) => Call(Cpy, new[] { a, b, length ?? NPaths }, name);

        public LLVMValueRef IppSet(double value, LLVMValueRef vector, string name = "", LLVMValueRef? length = null) =>
            value == 0.0 ? Call(Zro,
                                new[] { vector, length ?? NPaths },
                                name) : Call(Set, new[] { Const(value), vector, length ?? NPaths }, name);

        public MallocDisposer Allocate(LLVMValueRef bytes, string name = "")
        {
            var mem = Call(Malloc, new[] { bytes }, name);
            return new MallocDisposer(this, mem);
        }

        public LLVMValueRef Call(LLVMValueRef fn, LLVMValueRef[] args, string name = "") => LLVM.BuildCall(Builder, fn, args, name);

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

        public LlvmState(IntPtr valueMemory, IReadOnlyDictionary<string, (IntPtr, double)> varMemory, LLVMModuleRef module, LLVMBuilderRef builder, IntPtr gradientMemory, ulong nPaths)
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
            NPaths = LLVM.ConstInt(LLVM.Int64Type(), nPaths, new LLVMBool(0));
            ValueMemory = LLVM.ConstInt(LLVM.Int64Type(), (ulong)valueMemory.ToInt64(), new LLVMBool(0));
            GradientMemory = LLVM.ConstInt(LLVM.Int64Type(), (ulong)gradientMemory.ToInt64(), new LLVMBool(0));
            VarMemory = varMemory.Select(kvp => (kvp.Key, LLVM.ConstInt(LLVM.Int64Type(), (ulong)kvp.Value.Item1.ToInt64(), new LLVMBool(0)))).ToDictionary(t => t.Key, t => t.Item2);
            SeedMemory = varMemory.Select(kvp => (kvp.Key, kvp.Value.Item2)).ToDictionary(kvp => kvp.Key, kvp => kvp.Item2);
        }

        public LlvmState(IntPtr valueMemory, IntPtr gradientMemory, IReadOnlyDictionary<string, (IntPtr, double)> varMemory, ulong nPaths) : this(valueMemory, varMemory, LLVM.ModuleCreateWithName("CodeGen"), LLVM.CreateBuilder(), gradientMemory, nPaths)
        {

        }


    }

    public class LlvmVisitor : IExprVisitor<LlvmState, LlvmState>
    {
        public LlvmState VisitAdd(Add add, LlvmState state)
        {
            // Allocate both gradient and value temp in one go
            var allocLength = state.LMul(state.NPaths, state.Const(2));

            using (var tmpHandle = state.Allocate(allocLength))
            {
                var tmpValue = tmpHandle.Pointer;
                var tmpGrad = state.LAdd(tmpValue, state.OffsetF64(state.NPaths));

                state.IppSet(0.0, tmpValue, "", allocLength);

                foreach (var summand in add.Children)
                {
                    summand.Visit(this, state);
                    state.IppAdd(state.ValueMemory, tmpValue);
                    state.IppAdd(state.GradientMemory, tmpGrad);
                }

                state.IppCpy(tmpValue, state.ValueMemory);
                state.IppCpy(tmpGrad, state.GradientMemory);

                return state;
            }
        }

        public LlvmState VisitMul(Mul mul, LlvmState state)
        {
            // Allocate gradient and value temps in one go
            var allocLength = state.LMul(state.NPaths, state.Const(1 + mul.Children.Count));

            using (var tmpHandle = state.Allocate(allocLength))
            {
                var tmpValue = tmpHandle.Pointer;

                var tmpGrads = new LLVMValueRef[mul.Children.Count];
                for (var i = 0; i < mul.Children.Count; ++i)
                {
                    tmpGrads[i] = state.LAdd(tmpValue, state.OffsetF64(state.LMul(state.Const(i + 1), state.NPaths)));
                }

                state.IppSet(1.0, tmpValue, "", allocLength);

                var j = 0;
                foreach (var factor in mul.Children)
                {
                    factor.Visit(this, state);
                    state.IppMul(state.ValueMemory, tmpValue);
                    state.IppMul(state.GradientMemory, tmpGrads[j]);

                    for (int i = 0; i < tmpGrads.Length; ++i)
                    {
                        if (i != j)
                        {
                            state.IppMul(state.ValueMemory, tmpGrads[i]);
                        }
                    }

                    j++;
                }

                state.IppCpy(tmpGrads[0], state.GradientMemory);

                for (int i = 1; i < tmpGrads.Length; ++i)
                {
                    state.IppAdd(tmpGrads[i], state.GradientMemory);
                }

                state.IppCpy(tmpValue, state.ValueMemory);

                return state;
            }
        }

        public LlvmState VisitExp(Exp exp, LlvmState state)
        {
            exp.Exponent.Visit(this, state);
            state.IppExp(state.ValueMemory);
            state.IppMul(state.ValueMemory, state.GradientMemory);
            return state;
        }

        public LlvmState VisitConst(Const @const, LlvmState state)
        {
            state.IppSet(@const.Value, state.ValueMemory);
            state.IppSet(0.0, state.GradientMemory);
            return state;
        }

        public LlvmState VisitVar(Var var, LlvmState state)
        {
            state.IppCpy(state.VarMemory[var.Name], state.ValueMemory);
            state.IppSet(state.SeedMemory[var.Name], state.GradientMemory);
            return state;
        }
    }
}