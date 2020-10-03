using System.Collections.Generic;
using System.Linq;

namespace AutoExpr.Ipp
{
    public static class FunctionNames
    {
        public const string Malloc64F = "ippsMalloc_64f";
        public const string Free = "ippsFree";
        public const string Set64F = "ippsSet_64f";
        public const string Zero64F = "ippsZero_64f";
        public const string Exp64FI = "ippsExp_64f_I";
        public const string Copy64F = "ippsCopy_64f";
        public const string Add64FI = "ippsAdd_64f_I";
        public const string Mul64FI = "ippsMul_64f_I";

        private static IEnumerable<string> Enumerate()
        {
            yield return Malloc64F;
            yield return Free;
            yield return Set64F;
            yield return Zero64F;
            yield return Exp64FI;
            yield return Copy64F;
            yield return Add64FI;
            yield return Mul64FI;
            yield break;
        }

        public static string[] All {get;} = Enumerate().ToArray();
    } 
}