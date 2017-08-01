using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Pchp.CodeAnalysis.Semantics.Graph;
using QuickGraph;

namespace Phyl.CodeAnalysis.Graphs
{
    public class ControlFlowGraphEdge : IEdge<ControlFlowGraphVertex>
    {
        
        #region Constructors
        public ControlFlowGraphEdge(Edge edge, ControlFlowGraphVertex source, ControlFlowGraphVertex target)
        {
            BoundBlockEdge = edge;
            Source = source;
            Target = target;
        }
        #endregion

        #region Properties
        public ControlFlowGraphVertex Source { get; }
        public ControlFlowGraphVertex Target { get; }
        #endregion

        #region Fields
        Edge BoundBlockEdge;
        #endregion
    }
}
