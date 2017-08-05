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

using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis;
using Pchp.CodeAnalysis.CommandLine;
using Pchp.CodeAnalysis.Symbols;
using Pchp.CodeAnalysis.Semantics.Graph;
using Pchp.CodeAnalysis.FlowAnalysis;
using Devsense.PHP.Syntax;

using Serilog;
using SerilogTimings;

using Phyl.CodeAnalysis.Graphs;

namespace Phyl.CodeAnalysis
{
    public class AnalysisEngine : ILogged
    {
        #region Enums
        public enum OperationType
        {
            DUMP,
            GRAPH
        }
        #endregion
        
        #region Constructors
        public AnalysisEngine(Dictionary<string, object> engineOptions, TextWriter output)
        {
            EngineOptions = engineOptions;
            OutputStream = output;
            BaseDirectory = EngineOptions["Directory"] as string;
            FileSpec = EngineOptions["FileSpec"] as IEnumerable<string>;
            if (EngineOptions.ContainsKey("TargetFileSpec"))
            {
                TargetFileSpec = (IEnumerable<string>)EngineOptions["TargetFileSpec"];
            }
            MaxConcurrencyLevel = (int)EngineOptions["MaxConcurrencyLevel"];
            OnlyTime = EngineOptions.ContainsKey("OnlyTime") && (bool) EngineOptions["OnlyTime"] == true;
            EngineParallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrencyLevel,
                TaskScheduler = TaskScheduler.Default
            };
            AnalysisOperation = (OperationType)EngineOptions["OperationType"];
            if (EngineOptions.ContainsKey("Information"))
            {
                this.Information = EngineOptions["Information"] as string;
            }

            if (!(GetFileSpecifications() && ReadFiles()))
            {
                return;
            }
            
            if (this.AnalysisOperation == OperationType.GRAPH)
            {
                if (!CompileFiles())
                {
                    return;
                }
                this.GraphCFG();
            }
            else if (this.AnalysisOperation == OperationType.DUMP && this.Information == "tokens")
            {
                CreateFileTokenIndexes();
                using (Operation engineOp = L.Begin("Tokenizing {0} files", FileCount))
                {
                    FileTokens = new SortedSet<FileToken>[FileCount];
                    ExecuteConcurrentOperation(TokenizeFile, FileCount);
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
            bool result = false;
            if (AnalysisOperation == OperationType.DUMP && Information == "tokens")
            {
                using (Operation engineOp = L.Begin("Running lexical analysis"))
                {
                    if (result = DumpTokens())
                    {
                        engineOp.Complete();
                    }
                }
            }
            else if (AnalysisOperation == OperationType.DUMP && Information == "basic-blocks")
            {

            }
            if (AnalysisOperation == OperationType.GRAPH && Information == "tokens")
            {
                using (Operation engineOp = L.Begin("Running lexical analysis"))
                {
                    if (result = DumpTokens())
                    {
                        engineOp.Complete();
                    }
                }
            }

            return result;
        }

        protected bool GetFileSpecifications()
        {
            using (Operation engineOp = L.Begin("Scanning directory for file specification"))
            {
                if (Directory.Exists(BaseDirectory))
                {
                    BaseDirectoryInfo = new DirectoryInfo(BaseDirectory);
                    BaseDirectory = BaseDirectoryInfo.FullName;
                }
                else
                {
                    L.Error("The base directory {0} could not be found.", BaseDirectory);
                    return false;
                }
                CompilerArguments = PhpCommandLineParser.Default.Parse(FileSpec.ToArray(), BaseDirectory, RuntimeEnvironment.GetRuntimeDirectory(),
                    Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL"));
                FileCount = CompilerArguments.SourceFiles.Count();
                if (FileCount == 0)
                {
                    L.Error("No PHP source files match specification {files} in directory {dir}.", FileSpec, BaseDirectory);
                    return false;
                }
                else
                {
                    Files = CompilerArguments.SourceFiles.ToArray();
                }
                if (TargetFileSpec != null && TargetFileSpec.Count() > 0)
                {
                    CommandLineArguments targetFilesArgs = PhpCommandLineParser.Default.Parse(TargetFileSpec.ToArray(), BaseDirectory, RuntimeEnvironment.GetRuntimeDirectory(),
                        Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\assembly\GAC_MSIL"));
                    TargetFilePaths = new HashSet<string>(targetFilesArgs.SourceFiles.Select(f => f.Path));
                    if (TargetFilePaths.Count() == 0)
                    {
                        L.Error("No PHP source files match target specification {files} in directory {dir}.", TargetFileSpec, Compiler.Arguments.BaseDirectory);
                        return false;
                    }
                    else
                    {
                        List<int> targetFileIndex = new List<int>(TargetFilePaths.Count);
                        for (int i = 0; i < FileCount; i++)
                        {
                            if (TargetFilePaths.Contains(Files[i].Path))
                            {
                                targetFileIndex.Add(i);
                            }
                        }
                        TargetFileIndex = targetFileIndex.ToArray();
                    }
                }
                else
                {
                    TargetFileIndex = Enumerable.Range(0, FileCount).ToArray();
                }
                engineOp.Complete();
                return true;
            }
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
                    catch (FileNotFoundException)
                    {
                        L.Error("The file {0} could not be found.", Files[i].Path);
                        return false;
                    }
                    catch (IOException ioe)
                    {
                        L.Error(ioe, "I/O exception thrown attempting to read file {0}.", Files[i].Path);
                        return false;
                    }
                }
                for (int i = 0; i < FileCount; i++)
                {
                    
                    FileLines.Add(i, Regex.Split(FileText[i], @"\r\n|\r|\n"));
                }
                engineOp.Complete();
                return true;
            }
        }

        protected void CreateFileTokenIndexes()
        {
            FileTokenIndex = new Dictionary<Tokens, SortedSet<int>>[FileCount];
            for (int i = 0; i < FileCount; i++)
            {
                FileTokenIndex[i] = new Dictionary<Tokens, SortedSet<int>>();
            }
            int[] all = Enum.GetValues(typeof(Tokens)).Cast<int>().ToArray();
            ExecuteConcurrentOperation((j) =>
            {
                for (int k = 1; k < all.Length; k++)
                {
                    FileTokenIndex[j].Add((Tokens)all[k], new SortedSet<int>());
                }
            }, FileTokenIndex.Count());
        }

        protected Tuple<int, int> GetLineFromTokenPosition(int p, int fn)
        {
            int count = 0;
            int lt = Environment.NewLine.Count();
            for (int i = 0; i < FileLines[fn].Length; i++)
            {
                count += (FileLines[fn][i].Length + lt);
                if (p >= count)
                {
                    continue;
                }
                else
                {
                    return new Tuple<int, int>(i + 1, p - FileLines[fn].Take(i).Sum(l => l.Length + lt) + 1);
                }
            }
            throw new Exception("Position p {0} is after the end of the file {1} with length {2}.".F(p, Files[fn].Path, FileText[fn].Length));
        }

        protected bool TokenizeFile(int fn)
        {
            StringReader sr = new StringReader(FileText[fn]);
            Dictionary<Tokens, SortedSet<int>> index = FileTokenIndex[fn];
            SortedSet<FileToken> fileTokens = new SortedSet<FileToken>();
            Lexer lexer = new Lexer(sr, Encoding.UTF8, new LexerErrorSink());
            //lexer.GetCompressedState
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

        protected bool DumpTokens()
        {
            using (Operation engineOp = L.Begin("Dumping tokens for {0} file(s)", TargetFileIndex.Length))
            {
                string lt = Environment.NewLine;
                string[] token_dumps = new string[TargetFileIndex.Count()];
                ExecuteConcurrentOperation((i) =>
                {
                    string file = Files[TargetFileIndex[i]].Path;
                    StringBuilder token_dump = new StringBuilder(1000);
                    FileToken[] tokens = FileTokens[i].Where(t => t.Type != Tokens.T_WHITESPACE).ToArray();
                    Tuple<int, int> line;
                    for (int j = 0; j < tokens.Length; j++)
                    {
                        line = GetLineFromTokenPosition(tokens[j].Position.Start, i);
                        token_dump.AppendFormat("{0}File: {5}{0}Line: {1}{0}Col: {2}{0}Type: {3}{0}Text: {4}{0}", lt, line.Item1, line.Item2, tokens[j].Type.ToString(), tokens[j].Text, file);
                    }
                    token_dumps[i] = token_dump.ToString();
                    
                }, TargetFileIndex.Length);
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

        internal bool ParseFiles()
        {
            using (Operation engineOp = L.Begin("Parsing {0} PHP file(s).", FileCount))
            {
                SyntaxTrees = new PhpSyntaxTree[FileCount];
                bool hasErrors = false;
                ExecuteConcurrentOperation((i) =>
                {
                    PhpSyntaxTree result = PhpSyntaxTree.ParseCode(FileText[i], PhpParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose), PhpParseOptions.Default.WithKind(SourceCodeKind.Script), Files[i].Path);
                    if (result == null)
                    {
                        L.Error("Unknown error parsing file {0}.", Files[i]);
                        hasErrors = true;
                        return false;
                    }
                    else if (result != null && result.Diagnostics.HasAnyErrors())
                    {
                        L.Error("Error(s) reported parsing file {0}: {1}", Files[i].Path, result.Diagnostics);
                        hasErrors = true;
                        return false;
                    }
                    else
                    {
                        SyntaxTrees[i] = result;
                        return true;
                    }
                }, FileCount);

                if (hasErrors)
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

        internal bool CompileFiles()
        {
            using (Operation engineOp = L.Begin("Compiling {0} files in {1}", FileCount, BaseDirectory))
            {
                try
                {
                    Compiler = new PhylCompiler(this, Files.First().Path);
                }
                catch (Exception e)
                {
                    L.Error(e, "Exception thrown compiling files.");
                    return false;
                }
                if (Compiler.PhpCompilation == null)
                {
                    L.Error("Error compiling files.");
                    return false;
                }
                else
                {
                    if (BindandAnalyzeCFG())
                    {
                        engineOp.Complete();
                        return true;
                    }
                    else
                    {
                        engineOp.Cancel();
                        return false;
                    }
                }
            }

        }

        protected bool BindandAnalyzeCFG()
        {
            using (Operation engineOp = L.Begin("Binding types to symbols and analyzing control-flow"))
            {
                try
                {
                    SourceMethodsCompiler = new PhylSourceMethodsCompiler(this.Compiler.PhpCompilation, CancellationToken.None);
                    BindAndanalyzeDiagnostics = SourceMethodsCompiler.BindAndAnalyzeCFG()?.ToArray();
                    if (BindAndanalyzeDiagnostics != null && BindAndanalyzeDiagnostics.Length > 0)
                    {
                        L.Info("{0} warnings from bind and analyze phase.", BindAndanalyzeDiagnostics.Count());
                        foreach (Diagnostic d in BindAndanalyzeDiagnostics)
                        {
                            L.Warn($"{d}");
                        }
                    }
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

        protected bool GraphCFG()
        {
            SourceMethodsAnalysis = new Dictionary<SourceRoutineSymbol, ControlFlowGraphVisitor>(Compiler.PhpCompilation.SourceSymbolCollection.AllRoutines.Count());
            SourceMethodsCompiler.AnalyzeSourceMethods(AnalyzeSourceMethodDelegate);
            string o;
            GraphSerializer.SerializeControlFlowGraph(SourceMethodsAnalysis.First().Value.Graph, out o);
            L.Info("Printing GraphML for control-flow graph.");
            OutputStream.Write(o);
            return true;
        }

        private void AnalyzeSourceMethodDelegate(SourceRoutineSymbol routine)
        {
            Contract.ThrowIfNull(routine);
            if (routine.ControlFlowGraph != null)   // non-abstract method
            {
                ControlFlowGraphVisitor cfgVisitor = new ControlFlowGraphVisitor(routine);
                SourceMethodsAnalysis.Add(routine, cfgVisitor);
                cfgVisitor.VisitCFG(routine.ControlFlowGraph);
            }
        }

        private void ExecuteConcurrentOperation(Action<int> func, int upperBound)
        {
            if (MaxConcurrencyLevel == 1)
            {
                for (int i = 0; i < upperBound; i++)
                {
                    func(i);
                }
            }
            else if (upperBound - 1 > MaxConcurrencyLevel)
            {
                Parallel.For(0, MaxConcurrencyLevel, EngineParallelOptions, (workerId, ls) =>
                {
                    int start = upperBound * workerId / MaxConcurrencyLevel;
                    int end = upperBound * (workerId + 1) / MaxConcurrencyLevel;
                    for (int i = start; i < end; i++)
                    {
                        func(i);
                    }
                });
            }
            else
            {
                Parallel.For(0, upperBound, EngineParallelOptions, (i, ls) =>
                {
                    func(i);
                });
            }
        }

        private void ExecuteConcurrentOperation(Func<int, bool> func, int upperBound)
        {
            if (MaxConcurrencyLevel == 1)
            {
                for (int i = 0; i < upperBound; i++)
                {
                    if (!func(i))
                    {
                        break;
                    }
                }
            }
            else if (upperBound - 1 > MaxConcurrencyLevel)
            {
                Parallel.For(0, MaxConcurrencyLevel, EngineParallelOptions, (workerId, ls) =>
                {
                    if (ls.ShouldExitCurrentIteration)
                    {
                        return;
                    }
                    int start = upperBound * workerId / MaxConcurrencyLevel;
                    int end = upperBound * (workerId + 1) / MaxConcurrencyLevel;
                    for (int i = start; i < end; i++)
                    {
                        if (!func(i))
                        {
                            ls.Break();
                        }
                    }
                });
            }
            else
            {
                Parallel.For(0, upperBound, EngineParallelOptions, (i, ls) =>
                {
                    if (ls.ShouldExitCurrentIteration)
                    {
                        return;
                    }
                    if (!func(i))
                    {
                        ls.Break();
                    }
                });
            }
        }
        #endregion

        #region Properties
        public bool Initialised { get; protected set; } = false;
        public Dictionary<string, object> EngineOptions { get; protected set; }
        public int MaxConcurrencyLevel { get; protected set; }
        public bool OnlyTime { get; protected set; }
        public string BaseDirectory { get; protected set; }
        public DirectoryInfo BaseDirectoryInfo { get; protected set; }
        public int FileCount { get; protected set; }
        public IEnumerable<string> FileSpec { get; protected set; }
        public IEnumerable<string> TargetFileSpec { get; protected set; }
        public OperationType AnalysisOperation { get; protected set; }
        public string Information { get; protected set; }
        #endregion

        #region Fields
        public static List<string> DumpInformationCategories = new List<string> { "tokens", "cfg" };
        public static List<string> GraphInformationCategories = new List<string> { "tokens", "cfg", "ast"};
        TextWriter OutputStream;
        PhylLogger<AnalysisEngine> L = new PhylLogger<AnalysisEngine>();
        PhylCompiler Compiler;
        PhylSourceMethodsCompiler SourceMethodsCompiler;
        protected ParallelOptions EngineParallelOptions;
        internal CommandLineArguments CompilerArguments;
        protected CommandLineSourceFile[] Files ;
        protected HashSet<string> TargetFilePaths;
        protected int[] TargetFileIndex;
        protected string[] FileText;
        protected Dictionary<int, string[]> FileLines;
        protected SortedSet<FileToken>[] FileTokens;
        protected Dictionary<Tokens, SortedSet<int>>[] FileTokenIndex;
        protected ImmutableArray<Diagnostic> ParseDiagnostics;
        protected Diagnostic[] BindAndanalyzeDiagnostics;
        internal PhpSyntaxTree[] SyntaxTrees;
        Dictionary<SourceRoutineSymbol, ControlFlowGraphVisitor> SourceMethodsAnalysis;
        object engineLock = new object();
        #endregion
    }
}
