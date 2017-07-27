using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phyl
{
    public static class Extensions
    {
        [DebuggerStepThrough]
        public static string F(this string str, params object[] args)
        {
            return string.Format(str, args);
        }
    }
}
