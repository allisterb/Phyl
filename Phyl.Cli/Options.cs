using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;

namespace Phyl.Cli
{
    class CommonOptions
    {
        [Option('m', "max-concurrency", Default = 16, HelpText = "Sets the maximum number of concurrent operations that will be performed.")]
        public int MaxConcurrencyLevel { get; set; }

        [Option('t', "time", Default = 16, HelpText = "Only time the engine operations and do not print anything to the console.")]
        public bool OnlyTime { get; set; }
    }

    [Verb("dump", HelpText = "Dumps information and structures extracted from the source code by the analyzer.")]
    class DumpOptions : CommonOptions
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
