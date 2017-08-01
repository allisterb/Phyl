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
            UNHANDLED_EXCEPTION = 1,
            INVALID_OPTIONS = 2,
            ANALYSIS_ENGINE_INIT_ERROR = 3,
            ANALYSIS_ERROR = 4
        }
        static Dictionary<string, string> AppConfig { get; set; }
        static PhylLogger<Program> L;
        static StringBuilder CompilerOutput { get; } = new StringBuilder(100);
        static AnalysisEngine Engine { get; set; }
        static Dictionary<string, object> EngineOptions { get; } = new Dictionary<string, object>(3);
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += Program_UnhandledException;
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
                 foreach (PropertyInfo prop in o.GetType().GetProperties())
                 {
                     EngineOptions.Add(prop.Name, prop.GetValue(o));
                 }
             })
            .WithParsed((DumpOptions o) =>
            {
                if (!AnalysisEngine.DumpInformationCategories.Contains(o.Information))
                {
                    L.Info("The available information categories and structures are: {categories}.", AnalysisEngine.DumpInformationCategories);
                    Exit(ExitResult.INVALID_OPTIONS);
                }
                else
                {
                    EngineOptions.Add("OperationType", AnalysisEngine.OperationType.DUMP);
                    Analyze();
                }
            })
            .WithParsed((GraphOptions o) =>
            {
                if (!AnalysisEngine.GraphInformationCategories.Contains(o.Information))
                {
                    L.Info("The available information categories and structures for graphing are: {categories}.", AnalysisEngine.GraphInformationCategories);
                    Exit(ExitResult.INVALID_OPTIONS);
                }
                else
                {
                    EngineOptions.Add("OperationType", AnalysisEngine.OperationType.GRAPH);
                    Analyze();
                }
            });
        }

        static void Analyze()
        {
            using (Operation programOp = L.Begin("File and analysis operations"))
            {
                using (Operation engineOp = L.Begin("Initialising analysis engine"))
                {
                    Engine = new AnalysisEngine(EngineOptions, Console.Out);
                    if (!Engine.Initialised)
                    {
                        Exit(ExitResult.ANALYSIS_ENGINE_INIT_ERROR);
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
                else
                {
                    Exit(ExitResult.ANALYSIS_ERROR);
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

        static void Program_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Logger.Error(e.ExceptionObject as Exception, "An unhandled exception occurred.");
            if (e.IsTerminating)
            {
                Log.CloseAndFlush();
                Environment.Exit((int) ExitResult.UNHANDLED_EXCEPTION);
            }
        }
    }
}
