using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Phyl.CodeAnalysis;

namespace Phyl
{
    public class AnalysisEngine
    {
        public AnalysisEngine(string baseDirectory, string[] files, TextWriter output)
        {

            PhylCompiler compiler = new PhylCompiler(baseDirectory, files, Console.Out);
            IEnumerable<Diagnostic> d = PhylSourceCompiler.BindAndAnalyze(compiler.PhpCompilation);
            
             
        }

        

    }
}
