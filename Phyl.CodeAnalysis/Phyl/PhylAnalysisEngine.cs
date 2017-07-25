using System;
using System.Collections;
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
        public AnalysisEngine(Dictionary<string, object> engineOptions, TextWriter output)
        {
            EngineOptions = engineOptions;
            OutputStream = output;
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
                Files = args.SourceFiles.ToArray(); ;
                fileCount = ReadFiles();
                CreateFileTokenIndexes();
                using (Operation engineOp = L.Begin("Lexing {0} PHP files", fileCount))
                {
                    FileTokens = new SortedSet<FileToken>[fileCount];
                    if (fileCount > MaxConcurrencyLevel)
                    {
                        Parallel.For(0, MaxConcurrencyLevel, EngineParallelOptions, (workerId, ls) =>
                         {
                             int start = fileCount * workerId / MaxConcurrencyLevel;
                             int end = fileCount * (workerId + 1) / MaxConcurrencyLevel;
                             for (int i = start; i < end; i++)
                             {
                                 TokenizeFile(i);
                             }
                         });
                    }
                    else
                    {
                        Parallel.For(0, fileCount, EngineParallelOptions, (i, ls) =>
                        {
                            TokenizeFile(i);
                        });
                    }
                    engineOp.Complete();
                    Initialised = true;
                }
            }
            return;
        }
        #endregion

        #region Methods
        public bool Analyze()
        { 
            if (Op == OperationType.DUMP && Information == "tokens")
            {
                for (int i = 0; i < Files.Length; i++)
                {
                    for (int j = 0; j < FileTokens[i].Count; j++)
                    {

                    }
                }
            }
            return false;
        }

        protected int ReadFiles()
        {
            using (Operation engineOp = L.Begin("Reading {0} files", Files.Length))
            {
                FileLines = new Dictionary<int, string[]>(Files.Count());
                int read = 0;
                for (int i = 0; i < Files.Count(); i++)
                {
                    try
                    {
                        FileLines.Add(i, File.ReadAllLines(Files[i].Path, Encoding.UTF8));
                        read++;
                    }
                    catch (IOException ioe)
                    {
                        L.Error(ioe, "I/O exception thrown attempting to read file {0}.", Files[i].Path);
                    }
                }
                engineOp.Complete();
                return read;
            }
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
            StringReader sr = new StringReader(string.Join(Environment.NewLine, FileLines[fn]));
            Dictionary<Tokens, SortedSet<int>> index = FileTokenIndex[fn];
            SortedSet<FileToken> fileTokens = new SortedSet<FileToken>();
            CompliantLexer lexer = new CompliantLexer(new Lexer(sr, Encoding.UTF8, new LexerErrorSink()));
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

        protected void CreateFileTokenIndexes()
        {
            FileTokenIndex = new Dictionary<Tokens, SortedSet<int>>[FileLines.Count];
            for (int i = 0; i < FileLines.Count; i++)
            {
                FileTokenIndex[i] = new Dictionary<Tokens, SortedSet<int>>();
            }       
            int[] all = Enum.GetValues(typeof(Tokens)).Cast<int>().ToArray();
            for (int i = 1; i < all.Length; i++)
            {
                for (int j = 0; j < FileLines.Count; j++)
                {
                    FileTokenIndex[j].Add((Tokens)all[i], new SortedSet<int>());
                }
            }
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
        #endregion

        #region Fields
        public static List<string> InformationCategories = new List<string> { "tokens", "cfg" };
        TextWriter OutputStream;
        PhylLogger<AnalysisEngine> L = new PhylLogger<AnalysisEngine>();
        PhylCompiler Compiler;
        protected ParallelOptions EngineParallelOptions;
        protected CommandLineSourceFile[] Files;
        protected Dictionary<int, string[]> FileLines;
        protected SortedSet<FileToken>[] FileTokens;
        protected Dictionary<Tokens, SortedSet<int>>[] FileTokenIndex;
        object engineLock = new object();
        #endregion

        #region Types
        #endregion
    }
}
