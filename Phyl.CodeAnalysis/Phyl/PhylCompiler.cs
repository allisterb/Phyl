using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Semantics;
using Microsoft.CodeAnalysis.Diagnostics.Telemetry;
using Roslyn.Utilities;

using Pchp.CodeAnalysis.CommandLine;
using Pchp.CodeAnalysis.Errors;

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
                 RuntimeEnvironment.GetRuntimeDirectory(),
                 ReferenceDirectories,
                 new SimpleAnalyzerAssemblyLoader())
        {
            Output = output;
            ErrorStream = new MemoryStream();
            ErrorLogger = new ErrorLogger(ErrorStream, "Phyl", "0.1.0", new Version(0, 1, 0));
            TouchedFileLogger = new TouchedFileLogger();
            CreateCompilation(output, TouchedFileLogger, ErrorLogger);
            ErrorStream.Position = 0;
            StreamReader sr = new StreamReader(ErrorStream);
            Errors = sr.ReadToEnd();
        }

        #endregion

        #region Overriden methods
        public override Compilation CreateCompilation(TextWriter consoleOutput, TouchedFileLogger touchedFilesLogger, ErrorLogger errorLogger)
        {
            var a = base.ResolveAnalyzersFromArguments(new List<DiagnosticInfo>(), new MessageProvider(), touchedFilesLogger);
            return PhpCompilation = base.CreateCompilation(consoleOutput, touchedFilesLogger, errorLogger);   
        }
        #endregion

        #region Properties
        public Compilation PhpCompilation { get; protected set; }
        public CompilationWithAnalyzers PhpCompilationWithAnalyzers { get; protected set; }
        public TextWriter Output { get; protected set; }
        public MemoryStream ErrorStream { get; protected set; } = new MemoryStream();
        public TouchedFileLogger TouchedFileLogger { get; protected set; }
        public ErrorLogger ErrorLogger { get; protected set; } 
        public string Errors { get; protected set; }
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
                return Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL");
                
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
