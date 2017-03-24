using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phyl.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            StringBuilder compilerOutputStringBuilder = new StringBuilder();
            StringWriter compilerOutput = new StringWriter(compilerOutputStringBuilder);
            AnalysisEngine a = new AnalysisEngine(new string[] { @"C:\Projects\d8-examples\block_example\src\Controller\BlockExampleController.php" }, compilerOutput);
            
        }
    }
}
