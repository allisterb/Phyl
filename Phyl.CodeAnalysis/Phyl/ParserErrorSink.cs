using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serilog;
using Microsoft.CodeAnalysis;
using Devsense.PHP.Errors;
using Devsense.PHP.Syntax;
using Devsense.PHP.Text;
using Pchp.CodeAnalysis.Errors;

namespace Phyl.CodeAnalysis
{
    internal class LexerErrorSink : IErrorSink<Span>
    {
        public void Error(Span span, ErrorInfo info, params string[] argsOpt)
        {
            Log.Error("Error at {0}.", span.Start);
        }


    }
}
