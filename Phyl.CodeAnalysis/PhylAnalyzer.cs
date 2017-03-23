using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;


namespace Phyl
{
    public class PhylAnalyzer
    {
        public PhylAnalyzer(string[] files, TextWriter output)
        {
            PhylCompiler compiler = new PhylCompiler(files, output);
            SyntaxTree st = compiler.PhpCompilation.SyntaxTrees.First();
            
            
        }
    }
}
