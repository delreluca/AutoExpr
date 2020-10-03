using System;
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
                var nPaths = int.Parse(Console.ReadLine(), CultureInfo.InvariantCulture);

                // working memory
                var nVariables = 102; //100 normal draws and mu and sigma
                var valueMem = Marshal.AllocHGlobal(sizeof(double) * nPaths * (2 + nVariables));
                var gradientMem = valueMem + sizeof(double) * nPaths;
                var variablesMem = gradientMem + sizeof(double) * nPaths;
                var variables = Enumerable.Range(0, nVariables)
                    .Select(i => (i == 0 ? "mu" : i == 1 ? "sigma" : $"x{i - 2}",
                    (ptr: variablesMem + sizeof(double) * nPaths * i, seed: i == 0 ? 1.0 : 0.0))).ToDictionary(t => t.Item1, t => t.Item2);

                var xValues = new double[nPaths * nVariables];
                var rng = new Random();

                for (int i = 0; i < nPaths; i++)
                {
                    xValues[i] = 0.1; // mu
                    xValues[nPaths + i] = 0.5; // sigma
                    xValues[2 * nPaths + i] = 0.0; // x0
                }

                for (int i = nPaths * 3; i < nVariables * nPaths; i += 2)
                {
                    var normal = Statistics.BoxMuller(rng.NextDouble(), rng.NextDouble());
                    xValues[i] = normal.x;

                    if (i + 1 < nPaths * nVariables)
                    {
                        xValues[i + 1] = normal.y;
                    }
                }

                Marshal.Copy(xValues, 0, variablesMem, nPaths * nVariables);

                Expr node = new Var("x0");
                Expr mu = new Var("mu");
                Expr s = new Var("sigma");
                double timeGrid = 0.01;
                Expr dt = new Const(timeGrid);
                Expr brownianScale = new Const(Math.Sqrt(timeGrid));

                for (int i = 0; i < nVariables - 2; i++)
                {
                    var brownianIncrement = new Mul(new[] { new Var($"x{i}"), brownianScale });
                    node = new Add(new[] { new Mul(new[] { mu, dt }), new Mul(new[] { s, brownianIncrement }), node });
                }

                var final = new Exp(node);

                var state = final.Visit(new ForwardAdVisitor(), new CodeGenerator(valueMem, variables, gradientMem, (ulong)nPaths, LlvmBindings.BuildFunctions));

                var executor = new LlvmExecutor(state);
                executor.Run(env.FunctionPointers);

                Console.WriteLine("Back in main, fetching result.");
                var result = new double[nPaths];
                Marshal.Copy(valueMem, result, 0, nPaths);
                var gradResult = new double[nPaths];
                Marshal.Copy(gradientMem, gradResult, 0, nPaths);

                Console.WriteLine("E[X(100)] = {0}, expected 1.25", result.Average());
                Console.WriteLine("dE[X(100)]/dmu = {0}, expected 1.25", gradResult.Average());

                Marshal.FreeHGlobal(valueMem);
            }
        }
    }
}
