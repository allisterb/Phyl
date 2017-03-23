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
            string content = File.ReadAllText(Path.Combine("Examples", "program.php"), Encoding.UTF8);
            PhylAnalyzer a = new PhylAnalyzer(new string[] { Path.Combine("Examples", "program.php") }, Console.Out);
            
        }
    }
}
