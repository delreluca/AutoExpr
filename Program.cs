using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using AutoExpr.Ipp;

namespace AutoExpr
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var env = DynamicLibraries.LoadIpp())
            {
                Console.WriteLine("IPP available");

                Console.Write("How many paths? [e.g. 10000]: ");
                var nPaths = 1000;//int.Parse(Console.ReadLine(), CultureInfo.InvariantCulture);
                Console.WriteLine("Add to end result: ");
                var s = 0.0;//double.Parse(Console.ReadLine(), CultureInfo.InvariantCulture);

                var node = new Mul(new[] { (Expr)new Const(2.0), new Var("x") });
                var node2 = new Add(new[] { (Expr)new Const(1.0), node });
                var node3 = new Exp(node2);
                var node4 = new Add(new[] {(Expr)new Const(s), node3});

                // working memory
                var nVariables = 1;
                var vM = Marshal.AllocHGlobal(sizeof(double)*nPaths*(2+nVariables));
                var gM = vM + sizeof(double)*nPaths;
                var xM = gM + sizeof(double)*nPaths;
                var variables = new Dictionary<string, (IntPtr, double)>{
                    { "x", (xM, 1.0)}
                };

                var xValues = new double[nPaths];
                var rng = new Random();
                for (int i = 0; i < nPaths; i++)
                {
                    xValues[i] = rng.NextDouble();
                }

                Marshal.Copy(xValues, 0, xM, nPaths);

                var state = node4.Visit(new Visitor(), new CodeGenerator(vM, variables, gM, (ulong)nPaths, LlvmBindings.BuildFunctions));

                var executor = new LlvmExecutor(state);
                executor.Run(env.FunctionPointers);

                Console.WriteLine("Back in main, fetching result.");
                var result = new double[nPaths];
                Marshal.Copy(vM, result, 0, nPaths);
                var gradResult = new double[nPaths];
                Marshal.Copy(gM, gradResult, 0, nPaths);


                Console.WriteLine("MGF(1) = {0}, expected 8.68", result.Average());
                Console.WriteLine("dMGF(1) = {0}, expected 17.36", gradResult.Average());

                Marshal.FreeHGlobal(vM);
            }
        }
    }
}
