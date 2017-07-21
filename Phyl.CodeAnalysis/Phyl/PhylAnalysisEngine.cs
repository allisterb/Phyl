using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Pchp.CodeAnalysis;

using Serilog;
using SerilogTimings;

using Microsoft.CodeAnalysis;
using Phyl.CodeAnalysis;


namespace Phyl
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
        public AnalysisEngine(string baseDirectory, string[] files, Dictionary<string, object> engineOptions)
        {
            EngineOptions = engineOptions;
            Directory = EngineOptions["FileSpec"] as string;
            FileSpec = EngineOptions["FileSpec"] as IEnumerable<string>;
            try
            {
                Compiler = new PhylCompiler(baseDirectory, files);
            }
            catch (ArgumentException ae)
            {
                if (ae.Message == "No source files specified.")
                {
                    L.Error("No PHP source files match specification {files} in directory {dir}.", files, baseDirectory);
                    return;
                }
                else
                {
                    L.Error(ae, "Exception thrown creating PHP compiler.");
                    return;
                }
            }
            catch (Exception e)
            {
                L.Error(e, "Exception thrown creating PHP compiler.");
                return;
            }
            Compiler.CreateCompilation(Compiler.OuputWriter, Compiler.TouchedFileLogger, Compiler.ErrorLogger);
            if (Compiler.PhpCompilation != null)
            {
                Initialised = true;
                L.Success("Successfully initialised analysis engine.");
            }
            else
            {
                L.Error("Could not initialise analysis engine.");
                return;
            }
            if (EngineOptions.ContainsKey("Information"))
            {
                this.Op = OperationType.DUMP;
            }
        }
        #endregion

        #region Methods
        public bool Analyze()
        {
            if (this.Op == OperationType.DUMP)
            {
                using (Operation engineOp = L.Begin("Binding symbols to types and analyzing control-flow"))
                {
                    try
                    {
                        PhylSourceCompiler sc = new PhylSourceCompiler(this.Compiler.PhpCompilation, CancellationToken.None);
                        IEnumerable<Diagnostic> d = sc.BindAndAnalyzeCFG();
                        engineOp.Complete();
                    }
                    catch (Exception e)
                    {
                        L.Error(e, "Exception thrown during bind and anlyze.");
                        return false;
                    }
                }
                    
            }
            return false;
        }
        #endregion

        #region Properties
        public bool Initialised { get; protected set; } = false;
        public Dictionary<string, object> EngineOptions { get; protected set; } 
        public string Directory { get; protected set; }
        public IEnumerable<string> FileSpec { get; protected set; }
        public OperationType Op { get; protected set; }
        PhylLogger<AnalysisEngine> L = new PhylLogger<AnalysisEngine>();
        internal PhylCompiler Compiler { get; set; }
        #endregion

    }
}
