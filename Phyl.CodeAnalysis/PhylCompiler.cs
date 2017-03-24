using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Semantics;
using Microsoft.CodeAnalysis.Diagnostics.Telemetry;
using Roslyn.Utilities;

using Pchp.CodeAnalysis.CommandLine;

namespace Phyl.CodeAnalysis
{
    internal class PhylCompiler : PhpCompiler
    {
        #region Constructor
        public PhylCompiler(string[] files, TextWriter output)
            :base(
                 PhpCommandLineParser.Default,
                 Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ResponseFileName),
                 CreateCompilerArgs(files),
                 AppDomain.CurrentDomain.BaseDirectory,
                 Directory.GetCurrentDirectory(),
                 System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                 ReferenceDirectories,
                 new SimpleAnalyzerAssemblyLoader())
        {
            Output = output;
            ErrorsStream = new MemoryStream();
            TouchedFileLogger = new TouchedFileLogger();
            CreateCompilation(output, TouchedFileLogger, ErrorLogger);
            
            
            //ErrorLogger = new ErrorLogger(ErrorsStream, "Phyl", "1.0", new Version(1, 0));
        }

        #endregion

        #region Overriden methods
        public override Compilation CreateCompilation(TextWriter consoleOutput, TouchedFileLogger touchedFilesLogger, ErrorLogger errorLogger)
        {
            ImmutableArray<DiagnosticAnalyzer> a = base.ResolveAnalyzersFromArguments(new List<DiagnosticInfo>(), base.MessageProvider, touchedFilesLogger);

            List<DiagnosticAnalyzer> analyzers = new List<DiagnosticAnalyzer>() { new CompilationAnalyzer() };
            ImmutableArray<DiagnosticAnalyzer> aa = ImmutableArray.CreateRange(analyzers);
            AsyncQueue<CompilationEvent> events = new AsyncQueue<CompilationEvent>();
            PhpCompilation = base.CreateCompilation(consoleOutput, touchedFilesLogger, errorLogger).WithEventQueue(events);
            /*
            PhpCompilation.SyntaxTrees.First().v
            
            PhpCompilationWithAnalyzers = PhpCompilation.WithAnalyzers(aa, new CompilationWithAnalyzersOptions(AnalyzerOptions.Empty, null, null, true, false));
            System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource();
            //AnalyzerDriver ad = AnalyzerDriver.CreateAndAttachToCompilation(PhpCompilation, aa, AnalyzerOptions.Empty, new AnalyzerManager(), null, false, out nc, cts.Token);
            //var r = ad.GetDiagnosticsAsync(PhpCompilation).Result;
            var r = PhpCompilationWithAnalyzers.GetAnalyzerCompilationDiagnosticsAsync(aa, cts.Token);
            r.Wait();
            */
            return PhpCompilation;
            
        }
        #endregion

        #region Properties
        public Compilation PhpCompilation { get; protected set; }
        public CompilationWithAnalyzers PhpCompilationWithAnalyzers { get; protected set; }
        public TextWriter Output { get; protected set; }
        public MemoryStream ErrorsStream { get; protected set; } 
        public TouchedFileLogger TouchedFileLogger { get; protected set; }
        public ErrorLogger ErrorLogger { get; protected set; } 
        #endregion

        #region Methods

        static string[] CreateCompilerArgs(string[] args)
        {
            // implicit references
            List<Assembly> assemblies = new List<Assembly>()
            {
                typeof(object).Assembly,            // mscorlib (or System.Runtime)
                typeof(HashSet<>).Assembly,         // System.Core
                typeof(Pchp.Core.Context).Assembly,      // Peachpie.Runtime
                typeof(Pchp.Library.Strings).Assembly,   // Peachpie.Library
            };
            IEnumerable<string> refs = assemblies.Distinct().Select(ass => "/r:" + ass.Location);

            Debug.Assert(refs.Any(r => r.Contains("System.Core")));
            Debug.Assert(refs.Any(r => r.Contains("Peachpie.Runtime")));
            Debug.Assert(refs.Any(r => r.Contains("Peachpie.Library")));
            List<string> compiler_options = new List<string>()
            {
                "/target:library",
                "/debug+",
                "/analyzer:foo"
            };
            return compiler_options.Concat(refs).Concat(args).ToArray();
        }
        #endregion

        static string ReferenceDirectories
        {
            get
            {
                var libs = Environment.GetEnvironmentVariable("LIB");
                var gac = Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL");
                return libs + ";" + gac;
            }
        }
    }

    class SimpleAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
            throw new NotImplementedException();
        }

        public Assembly LoadFromPath(string fullPath)
        {
            throw new NotImplementedException();
        }
    }
}
