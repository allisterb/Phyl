using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Serilog;
using Phyl.CodeAnalysis;

namespace Phyl
{
    public class AnalysisEngine : ILogged
    {
        #region Constructors
        public AnalysisEngine(string baseDirectory, string[] files)
        {
            PhylCompiler compiler;
            try
            {
                compiler = new PhylCompiler(baseDirectory, files);
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
            compiler.CreateCompilation(compiler.OuputWriter, compiler.TouchedFileLogger, compiler.ErrorLogger);
            if (compiler.PhpCompilation != null)
            {
                Initialised = true;
                L.Success("Successfully initialised analysis engine.");
            }
            else
            {
                L.Error("Could not initialise analysis engine.");
            }
        }
        #endregion

        #region Properties
        public bool Initialised { get; protected set; } = false;
        PhylLogger<AnalysisEngine> L = new PhylLogger<AnalysisEngine>();
        #endregion

        #region Methods
        public bool Dump(Dictionary<string, object> dump_options)
        {
            return false;
        }
        #endregion

    }
}
