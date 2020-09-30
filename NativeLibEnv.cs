using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AutoExpr
{
    public class NativeLibEnv : IDisposable
    {
        private IntPtr[] libPtrs;

        private IReadOnlyDictionary<string, IntPtr> funPtrs;

        public NativeLibEnv(IntPtr[] libPtrs, IReadOnlyDictionary<string, IntPtr> funPtrs)
        {
            this.libPtrs = libPtrs;
            this.funPtrs = funPtrs;
        }

        public IReadOnlyDictionary<string, IntPtr> FunctionPointers => funPtrs;

        public void Dispose()
        {
            foreach (var libPtr in libPtrs)
            {
                NativeLibrary.Free(libPtr);
            }
        }


        internal static NativeLibEnv LoadIpp()
        {
            var imports = new Dictionary<string, string[]> {
                { "core", Array.Empty<string>()},
                {
                    "vm", Array.Empty<string>()
                },
                {
                    "s", new[] { "ippsAdd_64f_I", "ippsMul_64f_I", "ippsExp_64f_I",
                    "ippsZero_64f", "ippsCopy_64f", "ippsSet_64f", "ippsMalloc_64f", "ippsFree"}
                }
            };

            var libPtrs = new IntPtr[imports.Keys.Count];

            int i = 0;

            var funPtrs = imports.SelectMany(domAndSyms =>
            {
                var libPtr = NativeLibrary.Load(GetLibraryPath(domAndSyms.Key));

                libPtrs[i++] = libPtr;

                return domAndSyms.Value.Select(sym => (sym, NativeLibrary.GetExport(libPtr, sym)));
            }).ToDictionary(sp => sp.sym, sp => sp.Item2);

            return new NativeLibEnv(libPtrs, funPtrs);
        }

        private static string GetLibraryPath(string ippDomain) => Path.Combine("/opt/intel/ipp/lib", $"libipp{ippDomain}.dylib");

    }
}