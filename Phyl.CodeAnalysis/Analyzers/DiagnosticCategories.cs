using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phyl.CodeAnalysis
{
    public static class DiagnosticCategories
    {
        public const string Stateless = "SampleStatelessAnalyzers";
        public const string Stateful = "SampleStatefulAnalyzers";
        public const string AdditionalFile = "SampleAdditionalFileAnalyzers";
    }
}
