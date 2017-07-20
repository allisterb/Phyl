using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
namespace Phyl.CodeAnalysis
{
    public class CompilerErrors
    {
        public string version { get; set; }
        public Runlog[] runLogs { get; set; }

        public class Runlog
        {
            public Toolinfo toolInfo { get; set; }
            public Result[] results { get; set; }
        }

        public class Toolinfo
        {
            public string name { get; set; }
            public string version { get; set; }
            public string fileVersion { get; set; }
        }

        public class Result
        {
            public string ruleId { get; set; }
            public string kind { get; set; }
            public object[] locations { get; set; }
            public string fullMessage { get; set; }
            public bool isSuppressedInSource { get; set; }
            public string[] tags { get; set; }
            public Properties properties { get; set; }
        }

        public class Properties
        {
            public string severity { get; set; }
            public string defaultSeverity { get; set; }
            public string category { get; set; }
            public string isEnabledByDefault { get; set; }
        }
    }

    

}
