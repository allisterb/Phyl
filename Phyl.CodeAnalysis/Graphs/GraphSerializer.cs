using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using QuickGraph;
using Phyl.QuickGraph.Serialization;
using Phyl.QuickGraph.Serialization.DirectedGraphML;

namespace Phyl.CodeAnalysis.Graphs
{
    public class GraphSerializer
    {
        public static bool SerializeControlFlowGraph(AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge> graph, out string output)
        {
            output = string.Empty;
            DirectedGraph dg = graph.ToDirectedGraphML();
            graph.
            dg.WriteXml("graph.dhml");
            return true;
            
        }
    }
}
