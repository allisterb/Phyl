using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;

namespace Phyl.Cli
{
    [Verb("dump", HelpText = "Dumps information and structures extracted from the source code by the analyzer.")]
    class DumpOptions
    {
        [Value(0, Required = true, HelpText = "The information or structure like AST or basic blocks or CFG to dump.", MetaName = "Information")]
        public string Information { get; set; }

        [Value(1, Required = true, HelpText = "The root directory containing your PHP source code.", MetaName = "Directory")]
        public string Directory { get; set; }

        [Value(2, Required = true, HelpText = "The root directory containing your PHP source code.", MetaName = "FileSpec", Min = 1)]
        public IEnumerable<string> FileSpec { get; set; }
    }

    [Verb("graph", HelpText = "Dumps information and structures extracted from the source code by the analyzer.")]
    class GraphOptions
    {
    }
}
