using LLVMSharp;
using AutoExpr.IppLlvmBindings;
using AutoExpr.Llvm;

namespace AutoExpr
{
    /// <summary>Generates code for a forward algorithmic differentiation pass</summary>
    public class ForwardAdVisitor : IExprVisitor<CodeGenerator, CodeGenerator>
    {
        public CodeGenerator VisitAdd(Add add, CodeGenerator g)
        {
            // Allocate both gradient and value temp in one go
            var allocLength = g.NatMul(g.NPaths, g.Const(2));

            using (var tmpHandle = g.Allocate(allocLength))
            {
                var tmpValue = tmpHandle.Pointer;
                var tmpGrad = g.NatAdd(tmpValue, g.OffsetF64(g.NPaths));

                g.IppSet(0.0, tmpValue, "", allocLength);

                foreach (var summand in add.Children)
                {
                    summand.Visit(this, g);
                    g.IppAdd(g.ValueMemory, tmpValue);
                    g.IppAdd(g.GradientMemory, tmpGrad);
                }

                g.IppCpy(tmpValue, g.ValueMemory);
                g.IppCpy(tmpGrad, g.GradientMemory);

                return g;
            }

        }

        public CodeGenerator VisitMul(Mul mul, CodeGenerator g)
        {
            // Allocate gradient and value temps in one go
            var allocLength = g.NatMul(g.NPaths, g.Const(1 + mul.Children.Count));

            using (var tmpHandle = g.Allocate(allocLength))
            {
                var tmpValue = tmpHandle.Pointer;

                var tmpGrads = new LLVMValueRef[mul.Children.Count];
                for (var i = 0; i < mul.Children.Count; ++i)
                {
                    tmpGrads[i] = g.NatAdd(tmpValue, g.OffsetF64(g.NatMul(g.Const(i + 1), g.NPaths)));
                }

                g.IppSet(1.0, tmpValue, "", allocLength);

                var j = 0;
                foreach (var factor in mul.Children)
                {
                    factor.Visit(this, g);
                    g.IppMul(g.ValueMemory, tmpValue);
                    g.IppMul(g.GradientMemory, tmpGrads[j]);

                    for (int i = 0; i < tmpGrads.Length; ++i)
                    {
                        if (i != j)
                        {
                            g.IppMul(g.ValueMemory, tmpGrads[i]);
                        }
                    }

                    j++;
                }

                g.IppCpy(tmpGrads[0], g.GradientMemory);

                for (int i = 1; i < tmpGrads.Length; ++i)
                {
                    g.IppAdd(tmpGrads[i], g.GradientMemory);
                }

                g.IppCpy(tmpValue, g.ValueMemory);

                return g;
            }
        }

        public CodeGenerator VisitExp(Exp exp, CodeGenerator g)
        {
            exp.Exponent.Visit(this, g);
            g.IppExp(g.ValueMemory);
            g.IppMul(g.ValueMemory, g.GradientMemory);
            return g;
        }

        public CodeGenerator VisitConst(Const @const, CodeGenerator g)
        {
            g.IppSet(@const.Value, g.ValueMemory);
            g.IppSet(0.0, g.GradientMemory);
            return g;
        }

        public CodeGenerator VisitVar(Var var, CodeGenerator g)
        {
            g.IppCpy(g.VarMemory[var.Name], g.ValueMemory);
            g.IppSet(g.VariableSeeds[var.Name], g.GradientMemory);
            return g;
        }
    }
}