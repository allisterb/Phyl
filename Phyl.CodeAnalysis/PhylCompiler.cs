using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using Pchp.CodeAnalysis.CommandLine;
namespace Phyl
{
    internal class PhylCompiler : PhpCompiler
    {
        #region Constructor
        public PhylCompiler(string[] args, TextWriter output)
            :base(
                 PhpCommandLineParser.Default,
                 Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ResponseFileName),
                 CreateCompilerArgs(args),
                 AppDomain.CurrentDomain.BaseDirectory,
                 Directory.GetCurrentDirectory(),
                 System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                 ReferenceDirectories,
                 new SimpleAnalyzerAssemblyLoader())
        {
            Output = output;
            ErrorsStream = new MemoryStream();
            ErrorLogger = new ErrorLogger(ErrorsStream, "Phyl", string.Empty, new Version(0, 0));
            CreateCompilation(output, TouchedFileLogger, ErrorLogger);
        }

        #endregion

        #region Overriden methods
        public override Compilation CreateCompilation(TextWriter consoleOutput, TouchedFileLogger touchedFilesLogger, ErrorLogger errorLogger)
        {
            return PhpCompilation = base.CreateCompilation(consoleOutput, touchedFilesLogger, errorLogger);
        }
        #endregion

        #region Properties
        public Compilation PhpCompilation { get; protected set; }
        public TextWriter Output { get; protected set; }
        public MemoryStream ErrorsStream { get; protected set; } 
        public TouchedFileLogger TouchedFileLogger = new TouchedFileLogger();
        public ErrorLogger ErrorLogger { get; protected set; } 
        #endregion

        #region Methods

        static string[] CreateCompilerArgs(string[] args)
        {
            // implicit references
            var assemblies = new List<Assembly>()
            {
                typeof(object).Assembly,            // mscorlib (or System.Runtime)
                typeof(HashSet<>).Assembly,         // System.Core
                typeof(Pchp.Core.Context).Assembly,      // Peachpie.Runtime
                typeof(Pchp.Library.Strings).Assembly,   // Peachpie.Library
            };
            var refs = assemblies.Distinct().Select(ass => "/r:" + ass.Location);

            Debug.Assert(refs.Any(r => r.Contains("System.Core")));
            Debug.Assert(refs.Any(r => r.Contains("Peachpie.Runtime")));
            Debug.Assert(refs.Any(r => r.Contains("Peachpie.Library")));

            //
            return refs.Concat(args).ToArray();
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
