using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;

namespace Phyl.Cli
{
    abstract class Options
    {
        [Option('v', "verbose", HelpText = "Enable verbose logging. This can degrade performance.")]
        public bool Verbose { get; set; }

        [Value(1, Required = true, HelpText = "The root directory containing your PHP source code.", MetaName = "Directory")]
        public string Directory { get; set; }

        [Value(2, Required = true, HelpText = "The files in the root directory that will be included in the analysis. Use wildcards like * and ? and separate each pattern or name with a space.", MetaName = "FileSpec", Min = 1)]
        public IEnumerable<string> FileSpec { get; set; }

        [Option('f', "output-file", HelpText = "The file the analysis output should be saved to.")]
        public string OutputFile { get; set; }

        [Option('m', "max-concurrency", Default = 16, HelpText = "Sets the maximum number of parallel or concurrent engine operations that will be attempted at once.")]
        public int MaxConcurrencyLevel { get; set; }

        [Option('t', "time", Default = false, HelpText = "Only time the engine operations and do not print anything to the console.")]
        public bool OnlyTime { get; set; }

        [Option("target-files", Required = false, Min = 1, Separator = '+', HelpText = "Specific files in the filespec that will be targeted by the analysis.")]
        public IEnumerable<string> TargetFileSpec { get; set; }

        [Option("show-warnings", Required = false, Default = false, HelpText = "Show any warnings generated during the compilation of the PHP source files.")]
        public bool ShowCompileWarnings { get; set; }

    }

    [Verb("dump", HelpText = "Dumps information and structures extracted from the source code by the analyzer.")]
    class DumpOptions : Options
    {
        [Value(0, Required = true, HelpText = "The information or structure like AST or basic blocks or CFG to dump.", MetaName = "Information")]
        public string Information { get; set; }
    }

    [Verb("graph", HelpText = "Graph structures like CFGs created from the source code by the analyzer.")]
    class GraphOptions : Options
    {
        [Value(0, Required = true, HelpText = "The information or structure like CFG or AST to graph.", MetaName = "Information")]
        public string Information { get; set; }
    }
}
