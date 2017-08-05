
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Immutable;
using System.Xml.Serialization;

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
        internal ControlFlowGraphVertex(SourceRoutineSymbol routine, BoundBlock block)
        {
            this.Routine = routine;
            this.Block = block;
            this.BlockName = Block.DebugDisplay;
            this.RoutineName = Routine.Name;
            this.Statements = Block.Statements != null ? block.Statements.Count : 0;
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

        #region Properties
        [XmlAttribute]
        public string RoutineName { get; }
        [XmlAttribute]
        public string BlockName { get; }
        [XmlAttribute]
        public int Statements { get; }
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
        SourceRoutineSymbol Routine;
        #endregion

        #region Operators
        public static implicit operator int (ControlFlowGraphVertex v)
        {
            return v.Block.Ordinal;
        }
        #endregion

    }
}
