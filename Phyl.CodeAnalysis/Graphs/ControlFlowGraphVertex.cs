
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Symbols;
using Devsense.PHP.Syntax.Ast;
using Devsense.PHP.Syntax;

using Pchp.CodeAnalysis.Semantics.Graph;

namespace Phyl.CodeAnalysis.Graphs
{
    public class ControlFlowGraphVertex : IEquatable<ControlFlowGraphVertex>, IComparable<ControlFlowGraphVertex>, IComparable
    {
        #region Constructors
        public ControlFlowGraphVertex(BoundBlock block)
        {
            this.Block = block;
        }
        #endregion

        #region Overriden methods
        public override bool Equals(object obj)
        {
            if (obj is ControlFlowGraphVertex)
            {
                ControlFlowGraphVertex block = obj as ControlFlowGraphVertex;
                return this.Block.Ordinal == block.Block.Ordinal;
            }
            else
            {
                return false;
            }
        }
        
        public override int GetHashCode()
        {
            return this.Block.Ordinal.GetHashCode();
        }
        #endregion
        
        #region Methods
        public bool Equals(ControlFlowGraphVertex block)
        {
            return this.Block.Ordinal == block.Block.Ordinal;
        }

        public int CompareTo(ControlFlowGraphVertex block)
        {
            return this.Block.Ordinal.CompareTo(block.Block.Ordinal);
        }

        public int CompareTo(object obj)
        {
            if (obj is ControlFlowGraphVertex)
            {
                ControlFlowGraphVertex block = obj as ControlFlowGraphVertex;
                return this.Block.Ordinal.CompareTo(block.Block.Ordinal);
            }
            else throw new ArgumentOutOfRangeException("Object being compared is not of type BasicBlock.");
        }
        #endregion

        #region Fields
        BoundBlock Block;
        #endregion

    }
}
