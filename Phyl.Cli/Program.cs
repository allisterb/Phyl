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
    class Program
    {
        public enum ExitResult
        {
            SUCCESS = 0,
            INVALID_OPTIONS = 1,
        }
        static Dictionary<string, string> AppConfig { get; set; }
        static ILogger L;
        static List<string> InformationCategories { get; } = new List<string> { "cfg"};
        static StringBuilder CompilerOutput { get; } = new StringBuilder(100);
        static AnalysisEngine Engine { get; set; }
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.LiterateConsole()
            .CreateLogger();
            L = Log.ForContext<Program>();
            var result = Parser.Default.ParseArguments<DumpOptions, GraphOptions>(args)
            .WithNotParsed((IEnumerable<Error> errors) =>
            {
                L.Information("The command-line options had the following errors: {errors}", errors.Select(e => e.Tag));
                Exit(ExitResult.INVALID_OPTIONS);
            })
            .WithParsed((DumpOptions o) =>
            {
                if (InformationCategories.Contains(o.Information))
                {
                    Dump(o);
                    if (InformationCategories.Contains(o.Information))
                    {
                        Dump(o);
                        Exit(ExitResult.SUCCESS);
                    }
                    else
                    {
                        L.Information("The available information categories and structures are: {categories}.", InformationCategories);
                        Exit(ExitResult.INVALID_OPTIONS);
                    }
                }
                else
                {
                    Exit(ExitResult.INVALID_OPTIONS);
                }

            });
        }

        static void Dump(DumpOptions o)
        {
            Engine = new AnalysisEngine(o.Directory, o.FileSpec.ToArray(), new StringWriter(CompilerOutput));
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
