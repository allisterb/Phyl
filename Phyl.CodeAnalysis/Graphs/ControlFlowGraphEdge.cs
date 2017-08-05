using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Pchp.CodeAnalysis.Semantics.Graph;
using QuickGraph;

namespace Phyl.CodeAnalysis.Graphs
{
    public class ControlFlowGraphEdge : TaggedEdge<ControlFlowGraphVertex, string>, IEdge<ControlFlowGraphVertex>
    {
        #region Constructors
        public ControlFlowGraphEdge(Edge edge, ControlFlowGraphVertex source, ControlFlowGraphVertex target) : 
            base(source, target, "tag")
        {
            BoundBlockEdge = edge;
        }
        #endregion

        #region Fields
        Edge BoundBlockEdge;
        #endregion
    }
}
