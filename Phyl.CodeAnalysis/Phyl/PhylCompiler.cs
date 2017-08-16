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

using Pchp.CodeAnalysis;
using Pchp.CodeAnalysis.CommandLine;
using Pchp.CodeAnalysis.Errors;
using Devsense.PHP.Text;
using Devsense.PHP.Syntax.Ast;
using Devsense.PHP.Syntax;

using SerilogTimings;
using Newtonsoft.Json;

namespace Phyl.CodeAnalysis
{
    internal class PhylCompiler : PhpCompiler, ILogged
    {
        #region Constructors
        public PhylCompiler(AnalysisEngine engine, string firstFilePath)
            :base(
                 PhpCommandLineParser.Default,
                 Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ResponseFileName),
                 CreateCompilerArgs(new string[] { firstFilePath }),
                 AppDomain.CurrentDomain.BaseDirectory,
                 engine.BaseDirectory,
                 RuntimeEnvironment.GetRuntimeDirectory(),
                 ReferenceDirectories,
                 new SimpleAnalyzerAssemblyLoader())
        {
            Engine = engine;
            OuputWriter = new StringWriter(compilerOutput);
            ErrorLogger = new ErrorLogger(ErrorStream, "Phyl", Assembly.GetExecutingAssembly().GetName().Version.ToString(), Assembly.GetExecutingAssembly().GetName().Version);
            TouchedFileLogger = new TouchedFileLogger();
            this.CreateCompilation(OuputWriter, TouchedFileLogger, ErrorLogger);
        }

        #endregion

        #region Overriden methods
        public override Compilation CreateCompilation(TextWriter output, TouchedFileLogger touchedFilesLogger, ErrorLogger errorLogger)
        {
            using (Operation op = L.Begin("Creating PHP compilation"))
            {
                if (!Engine.ParseFiles())
                {
                    return null;
                }

                DesktopAssemblyIdentityComparer assemblyIdentityComparer = DesktopAssemblyIdentityComparer.Default;
                LoggingXmlFileResolver xmlFileResolver = new LoggingXmlFileResolver(Engine.BaseDirectory, touchedFilesLogger);
                LoggingSourceFileResolver sourceFileResolver = new LoggingSourceFileResolver(ImmutableArray<string>.Empty, Engine.CompilerArguments.BaseDirectory,
                    Engine.CompilerArguments.PathMap, touchedFilesLogger);
                MetadataReferenceResolver referenceDirectiveResolver;
                List<DiagnosticInfo> resolvedReferencesDiagnostics = new List<DiagnosticInfo>();
                List<MetadataReference> resolvedReferences = ResolveMetadataReferences(resolvedReferencesDiagnostics, touchedFilesLogger, out referenceDirectiveResolver);
                if (ReportErrors(resolvedReferencesDiagnostics, output, errorLogger))
                {
                    L.Error("Error(s) reported resolving references: {0}", resolvedReferencesDiagnostics);
                    return null;
                }
                MetadataReferenceResolver referenceResolver = GetCommandLineMetadataReferenceResolver(touchedFilesLogger);
                LoggingStrongNameProvider strongNameProvider = new LoggingStrongNameProvider(Engine.CompilerArguments.KeyFileSearchPaths, touchedFilesLogger);
                try
                {
                    PhpCompilation = PhpCompilation.Create(
                        Engine.CompilerArguments.CompilationName,
                        Engine.SyntaxTrees,
                        resolvedReferences,
                        Arguments.CompilationOptions.
                            WithMetadataReferenceResolver(referenceResolver).
                            WithAssemblyIdentityComparer(assemblyIdentityComparer).
                            WithStrongNameProvider(strongNameProvider).
                            WithXmlReferenceResolver(xmlFileResolver).
                            WithSourceReferenceResolver(sourceFileResolver)
                            );
                }
                catch (Exception e)
                {
                    L.Error(e, "An exception was thrown during parsing.");
                }
                finally
                {
                    /*
                    ErrorStream.Flush();
                    ErrorStream.Position = 0;
                    StreamReader sr = new StreamReader(ErrorStream);
                    Errors = sr.ReadToEnd();
                    */
                }

                if (!string.IsNullOrEmpty(Output))
                {
                    L.Info("Compiler output: {0}", Output);
                }
                if (PhpCompilation == null)
                {
                    op.Cancel();
                }
                else
                {
                    op.Complete();
                }
                return PhpCompilation;
            }
        }
        #endregion

        #region Properties
        public PhpCompilation PhpCompilation { get; protected set; }
        public StringWriter OuputWriter { get; }
        public string Output
        {
            get
            {
                return this.compilerOutput.ToString();
            }
        }
        public MemoryStream ErrorStream { get; protected set; } = new MemoryStream();
        public TouchedFileLogger TouchedFileLogger { get; protected set; }
        public ErrorLogger ErrorLogger { get; protected set; } 
        public string Errors { get; protected set; }
        protected PhylLogger<PhylCompiler> L = new PhylLogger<PhylCompiler>();
        static string ReferenceDirectories
        {
            get
            {
                return Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL");

            }
        }
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

            //Debug.Assert(refs.Any(r => r.Contains("System.Core")));
            Debug.Assert(refs.Any(r => r.Contains("Peachpie.Runtime")));
            Debug.Assert(refs.Any(r => r.Contains("Peachpie.Library")));
            List<string> compiler_options = new List<string>()
            {
                "/target:library",
            };
            return compiler_options.Concat(refs).Concat(args).ToArray();
        }

        #endregion

        #region Fields
        StringBuilder compilerOutput = new StringBuilder(100);
        AnalysisEngine Engine;
        #endregion
    }

    #region Types
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
    #endregion
}
