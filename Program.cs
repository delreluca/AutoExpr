using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AutoExpr
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var env = NativeLibEnv.LoadIpp())
            {
                Console.WriteLine("IPP available");

                var node = new Mul(new[] { (Expr)new Const(2.0), new Var("x") });
                var node2 = new Add(new[] { (Expr)new Const(1.0), node });
                var node3 = new Exp(node2);

                // working memory
                var wm1 = Marshal.AllocHGlobal(sizeof(double)*20000);
                var wm2 = wm1 + sizeof(double)*10000;
                var variables = new Dictionary<string, IntPtr>{
                    { "x", wm2}
                };

                var xValues = new double[10000];
                var rng = new Random();
                for (int i = 0; i < 10000; i++)
                {
                    xValues[i] = rng.NextDouble();
                }

                Marshal.Copy(xValues, 0, wm2, 10000);

                var state = node3.Visit(new LlvmVisitor(), new LlvmState(wm1, variables));

                state.Run(env);

                Console.WriteLine("Back in main, fetching result.");
                var result = new double[10000];
                Marshal.Copy(wm1, result, 0, 10000);


                Console.WriteLine("MGF(1) = {0}, expected 8.68", result.Average());

                Marshal.FreeHGlobal(wm1);
            }
        }
    }
}
