using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using QuickGraph;
using QuickGraph.Graphviz;
using Phyl.QuickGraph.Serialization;

namespace Phyl.CodeAnalysis.Graphs
{
    public class GraphSerializer
    {
        public static bool SerializeControlFlowGraph(AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge> graph, out string output)
        {
            output = string.Empty;
            StringBuilder sb = new StringBuilder();
            using (XmlWriter w = XmlWriter.Create(sb))
            {
             
                graph.SerializeToGraphML<ControlFlowGraphVertex, ControlFlowGraphEdge, AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge>>(w);
                w.Flush();
                output = sb.ToString();
            }
            return true;
            
        }

        public static bool SerializeControlFlowGraph(AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge> graph, XmlWriter writer)
        {
            graph.SerializeToGraphML<ControlFlowGraphVertex, ControlFlowGraphEdge, AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge>>(writer);
            return true;

        }
    }
}
