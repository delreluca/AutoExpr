using System;
using System.Collections.Generic;

namespace AutoExpr
{
    public abstract class Expr
    {
        public abstract IReadOnlyList<Expr> Children { get; }

        public abstract T Visit<T, S>(IExprVisitor<T, S> vtor, S state);
    }

    public sealed class Add : Expr
    {
        public Add(IReadOnlyList<Expr> children)
        {
            Children = children;
        }

        public override IReadOnlyList<Expr> Children { get; }

        public override T Visit<T, S>(IExprVisitor<T, S> vtor, S state) => vtor.VisitAdd(this, state);
    }

    public sealed class Mul : Expr
    {
        public Mul(IReadOnlyList<Expr> children)
        {
            Children = children;
        }

        public override IReadOnlyList<Expr> Children { get; }

        public override T Visit<T, S>(IExprVisitor<T, S> vtor, S state) => vtor.VisitMul(this, state);
    }

    public sealed class Exp : Expr
    {
        public Exp(Expr exponent)
        {
            Exponent = exponent;
            Children = new[] { exponent };
        }

        public Expr Exponent { get; }

        public override IReadOnlyList<Expr> Children { get; }

        public override T Visit<T, S>(IExprVisitor<T, S> vtor, S state) => vtor.VisitExp(this, state);
    }

    public sealed class Const : Expr
    {
        public Const(double value)
        {
            Value = value;
        }

        public double Value {get;}

        public override IReadOnlyList<Expr> Children => Array.Empty<Expr>();

        public override T Visit<T, S>(IExprVisitor<T, S> vtor, S state) => vtor.VisitConst(this, state);
    }

    public sealed class Var : Expr
    {
        public Var(string name)
        {
            Name = name;
        }

        public string Name {get;}

        public override IReadOnlyList<Expr> Children => Array.Empty<Expr>();

        public override T Visit<T, S>(IExprVisitor<T, S> vtor, S state) => vtor.VisitVar(this, state);
    }

    public interface IExprVisitor<T, S>
    {
        T VisitAdd(Add add, S state);

        T VisitMul(Mul mul, S state);

        T VisitExp(Exp exp, S state);

        T VisitConst(Const @const, S state);

        T VisitVar(Var constVector, S state);
    }
}