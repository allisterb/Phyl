using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serilog;
using CommandLine;

namespace Phyl.Cli
{
    class Program : ILogged
    {
        public enum ExitResult
        {
            SUCCESS = 0,
            INVALID_OPTIONS = 1,
            ERROR_INIT_ANALYSIS_ENGINE = 2
        }
        static Dictionary<string, string> AppConfig { get; set; }
        static PhylLogger<Program> L;
        static List<string> InformationCategories { get; } = new List<string> { "cfg"};
        static StringBuilder CompilerOutput { get; } = new StringBuilder(100);
        static AnalysisEngine Engine { get; set; }
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.LiterateConsole()
            .CreateLogger();
            L = new PhylLogger<Program>();
            ParserResult<object> result = Parser.Default.ParseArguments<DumpOptions, GraphOptions>(args)
            .WithNotParsed((IEnumerable<Error> errors) =>
            {
                L.Info("The command-line options had the following errors: {errors}", errors.Select(e => e.Tag));
                Exit(ExitResult.INVALID_OPTIONS);
            })
            .WithParsed((DumpOptions o) =>
            {
                Engine = new AnalysisEngine(o.Directory, o.FileSpec.ToArray());
                if (!Engine.Initialised)
                {
                    Exit(ExitResult.ERROR_INIT_ANALYSIS_ENGINE);
                }
                else
                {
                    L.Info("Successfully initialised analysis engine.");
                    Exit(ExitResult.SUCCESS);
                }
                if (InformationCategories.Contains(o.Information))
                {
                    Dump(o);
                    Exit(ExitResult.SUCCESS);
                }
                else
                {
                    L.Info("The available information categories and structures are: {categories}.", InformationCategories);
                    Exit(ExitResult.INVALID_OPTIONS);
                }
            });
        }

        static void Dump(DumpOptions o)
        {
            
        }

        static void Exit(ExitResult result)
        {
            Log.CloseAndFlush();
            Environment.Exit((int)result);
        }

        static int ExitWithCode(ExitResult result)
        {
            Log.CloseAndFlush();
            return (int)result;
        }
    }
}
