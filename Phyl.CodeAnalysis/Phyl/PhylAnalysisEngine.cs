using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
            OnlyTime = EngineOptions.ContainsKey("OnlyTime") && (bool) EngineOptions["OnlyTime"] == true;
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
                FileCount = args.SourceFiles.Count();
                if (FileCount == 0)
                {
                    L.Error("No PHP source files match specification {files} in directory {dir}.", FileSpec, Compiler.Arguments.BaseDirectory);
                    return;
                }
                Files = args.SourceFiles.ToArray();
                if (!ReadFiles())
                {
                    return;
                }
                CreateFileTokenIndexes();
                using (Operation engineOp = L.Begin("Lexing {0} PHP files", FileCount))
                {
                    FileTokens = new SortedSet<FileToken>[FileCount];
                    if (FileCount > MaxConcurrencyLevel)
                    {
                        Parallel.For(0, MaxConcurrencyLevel, EngineParallelOptions, (workerId, ls) =>
                         {
                             int start = FileCount * workerId / MaxConcurrencyLevel;
                             int end = FileCount * (workerId + 1) / MaxConcurrencyLevel;
                             for (int i = start; i < end; i++)
                             {
                                 TokenizeFile(i);
                             }
                         });
                    }
                    else
                    {
                        Parallel.For(0, FileCount, EngineParallelOptions, (i, ls) =>
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
                using (Operation analyzeOp = L.Begin("Analysis"))
                {
                    DumpTokens();
                    analyzeOp.Complete();
                }
            }
            return false;
        }

        protected bool ReadFiles()
        {
            using (Operation engineOp = L.Begin("Reading {0} files", Files.Length))
            {
                FileText = new string[FileCount];
                FileLines = new Dictionary<int, string[]>();
                for (int i = 0; i < FileCount; i++)
                {
                    try
                    {
                        FileText[i] = File.ReadAllText(Files[i].Path);
                    }
                    catch (IOException ioe)
                    {
                        L.Error(ioe, "I/O exception thrown attempting to read file {0}.", Files[i].Path);
                        return false;
                    }
                }
                //Regex lt = new Regex(@"\r\n|\r|\n", RegexOptions.Compiled);
                for (int i = 0; i < FileCount; i++)
                {
                    
                    FileLines.Add(i, Regex.Split(FileText[i], @"\r\n|\r|\n"));
                }
                engineOp.Complete();
                return true;
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
            StringReader sr = new StringReader(FileText[fn]);
            Dictionary<Tokens, SortedSet<int>> index = FileTokenIndex[fn];
            SortedSet<FileToken> fileTokens = new SortedSet<FileToken>();
            Lexer lexer = new Lexer(sr, Encoding.UTF8, new LexerErrorSink());
            Tokens t = 0;
            while ((t = lexer.GetNextToken())> Tokens.EOF)
            {
                
                fileTokens.Add(new FileToken(t, lexer.TokenPosition, lexer.TokenText));
                index[t].Add(lexer.TokenPosition.Start);
            }
            FileTokens[fn] = fileTokens;
            FileTokenIndex[fn] = index;
            return true;
        }

        protected void CreateFileTokenIndexes()
        {
            FileTokenIndex = new Dictionary<Tokens, SortedSet<int>>[FileCount];
            for (int i = 0; i < FileCount; i++)
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

        protected int GetLineFromTokenPosition(int p, int fn)
        {
            int count = 0;
            int lt = Environment.NewLine.Count();
            for (int i = 0; i < FileLines[fn].Length; i++)
            {
                count += (FileLines[fn][i].Length + lt);
                if (p > count)
                {
                    continue;
                }
                else
                {
                    return i + 1;
                }
            }
            throw new Exception("Position p {0} is after the end of the file {1} with length {2}.".F(p, Files[fn].Path, FileText[fn].Length));
        }

        protected bool DumpTokens()
        {
            using (Operation engineOp = L.Begin("Dumping tokens for {0} files.", FileTokens.Count()))
            {
                string lt = Environment.NewLine;
                string[] token_dumps = new string[FileTokens.Count()];
                for (int i = 0; i < FileTokens.Length; i++)
                {
                    string file = Files[i].Path;
                    StringBuilder token_dump = new StringBuilder(1000);
                    FileToken[] tokens = FileTokens[i].Where(t => t.Type != Tokens.T_WHITESPACE).ToArray();
                    int line;
                    for (int j = 0; j < tokens.Length; j++)
                    {
                        line = GetLineFromTokenPosition(tokens[j].Position.Start, i);
                        token_dump.AppendFormat("{0}File: {4}{0}Line: {1}{0}Type: {2}{0}Text: {3}{0}", lt, line, tokens[j].Type.ToString(), tokens[j].Text, file);
                    }
                    token_dumps[i] = token_dump.ToString();
                }
                if (!OnlyTime)
                {
                    for (int i = 0; i < token_dumps.Length; i++)
                    {
                        OutputStream.WriteLine(token_dumps[i]);
                    }
                }
                engineOp.Complete();
                return true;
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
        public bool OnlyTime { get; protected set; }
        public string Directory { get; protected set; }
        public int FileCount { get; protected set; }
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
        protected string[] FileText;
        protected Dictionary<int, string[]> FileLines;
        protected SortedSet<FileToken>[] FileTokens;
        protected Dictionary<Tokens, SortedSet<int>>[] FileTokenIndex;
        object engineLock = new object();
        #endregion

        #region Types
        #endregion
    }
}
