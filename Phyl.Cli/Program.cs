using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Serilog;
using SerilogTimings;
using CommandLine;

using Phyl.CodeAnalysis;

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
        static StringBuilder CompilerOutput { get; } = new StringBuilder(100);
        static AnalysisEngine Engine { get; set; }
        static Dictionary<string, object> EngineOptions { get; } = new Dictionary<string, object>(3);
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.LiterateConsole()
            .CreateLogger();
            L = new PhylLogger<Program>();
            ParserResult<object> result = Parser.Default.ParseArguments<DumpOptions, GraphOptions>(args)
             .WithNotParsed((IEnumerable<Error> errors) =>
             {
                 L.Info("The command-line options had the following errors: {errors}", errors.Select(e => e.Tag));
                 Exit(ExitResult.INVALID_OPTIONS);
             })
             .WithParsed((CommonOptions o) =>
             {
                 if (o.MaxConcurrencyLevel < 1 || o.MaxConcurrencyLevel > 128)
                 {
                     L.Error("The max concurrency level option must be between 1 and 128");
                     Exit(ExitResult.INVALID_OPTIONS);
                 }
            })
            .WithParsed((DumpOptions o) =>
            {
                if (!AnalysisEngine.InformationCategories.Contains(o.Information))
                {
                    L.Info("The available information categories and structures are: {categories}.", AnalysisEngine.InformationCategories);
                    Exit(ExitResult.INVALID_OPTIONS);
                }
                else
                {
                    Analyze(o);
                    Exit(ExitResult.SUCCESS);
                    
                }
            });
        }

        static void Analyze(DumpOptions o)
        {
            using (Operation programOp = L.Begin("File and analysis operations"))
            {
                foreach (PropertyInfo prop in o.GetType().GetProperties())
                {
                    EngineOptions.Add(prop.Name, prop.GetValue(o));
                }
                using (Operation engineOp = L.Begin("Initialising analysis engine"))
                {
                    Engine = new AnalysisEngine(EngineOptions, Console.Out);
                    if (!Engine.Initialised)
                    {
                        Exit(ExitResult.ERROR_INIT_ANALYSIS_ENGINE);
                    }
                    else
                    {
                        engineOp.Complete();
                    }
                }
                if (Engine.Analyze())
                {
                    programOp.Complete();
                    Exit(ExitResult.SUCCESS);
                }
            }
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
