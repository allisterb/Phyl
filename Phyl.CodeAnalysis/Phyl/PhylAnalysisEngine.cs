using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Pchp.CodeAnalysis;

using Serilog;
using SerilogTimings;

using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.CommandLine;
using Phyl.CodeAnalysis;
using Devsense.PHP.Text;
using Devsense.PHP.Syntax;

namespace Phyl.CodeAnalysis
{
    public class AnalysisEngine : ILogged
    {
        #region Enums
        public enum OperationType
        {
            DUMP
        }
        #endregion
        
        #region Constructors
        public AnalysisEngine(Dictionary<string, object> engineOptions)
        {
            EngineOptions = engineOptions;
            Directory = EngineOptions["Directory"] as string;
            FileSpec = EngineOptions["FileSpec"] as IEnumerable<string>;
            MaxConcurrencyLevel = (int)EngineOptions["MaxConcurrencyLevel"];
            EngineParallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrencyLevel,
                TaskScheduler = TaskScheduler.Default
            };
            if (EngineOptions.ContainsKey("Information"))
            {
                this.Information = EngineOptions["Information"] as string;
                this.Op = OperationType.DUMP;
            }
            else throw new ArgumentException("Unknown engine operation requested.");
            if (this.Op == OperationType.DUMP && this.Information != "tokens")
            {
                try
                {
                    Compiler = new PhylCompiler(Directory, FileSpec.ToArray());
                }
                catch (ArgumentException ae)
                {
                    if (ae.Message == "No source files specified.")
                    {
                        L.Error("No PHP source files match specification {files} in directory {dir}.", FileSpec, Directory);
                        return;
                    }
                    else
                    {
                        L.Error(ae, "Exception thrown initialising PHP compiler.");
                        return;
                    }
                }
                catch (Exception e)
                {
                    L.Error(e, "Exception thrown initialising PHP compiler.");
                    return;
                }

                if (Compiler.Arguments.SourceFiles.Count() == 0)
                {
                    L.Error("No PHP source files match specification {files} in directory {dir}.", FileSpec, Compiler.Arguments.BaseDirectory);
                    return;
                }

                if (!this.ParseFiles())
                {
                    L.Error("Could not initialise analysis engine.");
                    return;
                }
                else
                {
                    Initialised = true;
                    L.Success("Successfully initialised analysis engine.");
                }
            }
            else if (this.Op == OperationType.DUMP && this.Information == "tokens")
            {   
                CommandLineArguments args = PhpCommandLineParser.Default.Parse(FileSpec.ToArray(), Directory, RuntimeEnvironment.GetRuntimeDirectory(), 
                    Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL"));
                int fileCount = args.SourceFiles.Count();
                if (fileCount == 0)
                {
                    L.Error("No PHP source files match specification {files} in directory {dir}.", FileSpec, Compiler.Arguments.BaseDirectory);
                    return;
                }
                Files = args.SourceFiles;
                int stopped = 0;
                using (Operation engineOp = L.Begin("Lexing {0} PHP files", fileCount))
                {
                    FileTokens = new SortedSet<FileToken>[fileCount];
                    FileTokenIndex = new Dictionary<Tokens, SortedSet<int>>[fileCount];
                    if (fileCount > MaxConcurrencyLevel)
                    {
                        Parallel.For(0, MaxConcurrencyLevel, EngineParallelOptions, (workerId, ls) =>
                         {
                             if (ls.ShouldExitCurrentIteration)
                             {
                                 Interlocked.Increment(ref stopped);
                                 ls.Break();
                             }
                             int start = fileCount * workerId / MaxConcurrencyLevel;
                             int end = fileCount * (workerId + 1) / MaxConcurrencyLevel;
                             for (int i = start; i < end; i++)
                             {
                                 if (!TokenizeFile(i))
                                 {
                                     ls.Stop();
                                 }
                             }
                         });
                    }
                    else
                    {
                        Parallel.For(0, fileCount, EngineParallelOptions, (i, ls) =>
                        {
                            if (ls.ShouldExitCurrentIteration)
                            {
                                Interlocked.Increment(ref stopped);
                                ls.Break();
                            }
                            if (!TokenizeFile(i))
                            {
                                ls.Stop();
                            }
                        });
                    }
                    if (stopped == 0)
                    {
                        engineOp.Complete();
                        Initialised = true;
                    }
                    else
                    {
                        L.Error("An error occurred during lexing.");
                        return;
                    }
                }
            }
            return;
        }
        #endregion

        #region Methods
        public bool Analyze()
        {
            
            return false;
        }

        protected bool ParseFiles()
        {
            using (Operation engineOp = L.Begin("Parsing {0} PHP files.", Compiler.Arguments.SourceFiles.Count()))
            {
                Compiler.CreateCompilation(Compiler.OuputWriter, Compiler.TouchedFileLogger, Compiler.ErrorLogger);
                if (Compiler.PhpCompilation != null)
                {
                    return false;
                }
                else
                {
                    engineOp.Complete();
                    return true;
                }
            }
            
        }

        protected bool TokenizeFile(int fn)
        {
            Dictionary<Tokens, SortedSet<int>> index = FileTokenIndex[fn] = new Dictionary<Tokens, SortedSet<int>>();
            int[] all = Enum.GetValues(typeof(Tokens)).Cast<int>().ToArray();
            for(int i = 1; i < all.Length; i++)
            {
                index.Add((Tokens)all[i], new SortedSet<int>());
            }
            TextReader tr;
            try
            {
                 tr = File.OpenText(Files[fn].Path);
            }
            catch (Exception e)
            {
                L.Error(e, "Exception thrown attempting to open file {0}", fn);
                return false;
            }
            SortedSet<FileToken> fileTokens = new SortedSet<FileToken>();
            CompliantLexer lexer = new CompliantLexer(new Lexer(tr, Encoding.UTF8, new LexerErrorSink()));
            int t = 0;
            while ((t = lexer.GetNextToken()) > 0)
            {
                fileTokens.Add(new FileToken((Tokens)t, lexer.TokenPosition, lexer.TokenText));
                index[(Tokens)t].Add(lexer.TokenPosition.Start);
            }
            FileTokens[fn] = fileTokens;
            FileTokenIndex[fn] = index;
            return true;
        }

        protected bool BindandAnalyze()
        {
            using (Operation engineOp = L.Begin("Binding symbols to types and analyzing control-flow"))
            {
                try
                {
                    PhylSourceCompiler sc = new PhylSourceCompiler(this.Compiler.PhpCompilation, CancellationToken.None);
                    IEnumerable<Diagnostic> d = sc.BindAndAnalyzeCFG();
                    engineOp.Complete();
                    return true;
                }
                catch (Exception e)
                {
                    L.Error(e, "Exception thrown during bind and analyze.");
                    return false;
                     
                }
            }
        }
        #endregion

        #region Properties
        public bool Initialised { get; protected set; } = false;
        public Dictionary<string, object> EngineOptions { get; protected set; }
        public int MaxConcurrencyLevel { get; protected set; }
        public string Directory { get; protected set; }
        public IEnumerable<string> FileSpec { get; protected set; }
        public OperationType Op { get; protected set; }
        public string Information { get; protected set; }
        public static List<string> InformationCategories { get; } = new List<string> { "tokens", "cfg" };
        PhylLogger<AnalysisEngine> L = new PhylLogger<AnalysisEngine>();
        PhylCompiler Compiler { get; set; }
        protected ParallelOptions EngineParallelOptions { get; set; }
        ImmutableArray<CommandLineSourceFile> Files { get; set; }
        SortedSet<FileToken>[] FileTokens { get; set; }
        Dictionary<Tokens, SortedSet<int>>[] FileTokenIndex { get; set; }
        #endregion

        #region Fields
        object engineLock = new object();
        #endregion

        #region Types
        #endregion
    }
}
