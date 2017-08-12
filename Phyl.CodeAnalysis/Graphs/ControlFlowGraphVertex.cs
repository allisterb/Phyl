
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Immutable;
using System.Xml.Serialization;

using Pchp.CodeAnalysis.Semantics;
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

            this.routine = routine;
            this.block = block;
            this.File = routine.ContainingFile.SyntaxTree.FilePath;
            this.Vid = this.block.Ordinal + this.File.GetHashCode();
            this.BlockName = this.block.DebugDisplay;
            this.Kind = this.block.Kind.ToString();
            this.RoutineName = this.routine.Name;
            this.Statements = this.block.Statements != null ? block.Statements.Count : 0;
            /*
            if (this.Statements > 0)
            {
                this.Kind = this.block.Statements.FirstOrDefault(s => s != null)?.Kind.ToString(); 
                LangElement l = ControlFlowGraphVisitor.PickFirstSyntaxNode(this.block);
                if (l != null)
                {
                    Tuple<int, int> pos = engine.GetLineFromTokenPosition(l.Span.Start, this.File);
                    this.Line = pos.Item1;
                    this.Column = pos.Item2;
                }
            }
            */
        }
        #endregion

        #region Overriden methods
        public override bool Equals(object obj)
        {
            if (obj is ControlFlowGraphVertex)
            {
                ControlFlowGraphVertex block = obj as ControlFlowGraphVertex;
                return this.Vid == block.Vid;
            }
            else
            {
                return false;
            }
        }
        
        public override int GetHashCode()
        {
            return this.Vid.GetHashCode();
        }
        #endregion

        #region Properties
        public int Vid { get; }
        [XmlAttribute]
        public string File { get; }
        [XmlAttribute]
        public int Line { get; }
        [XmlAttribute]
        public int Column { get; }
        [XmlAttribute]
        public string RoutineName { get; }
        [XmlAttribute]
        public string BlockName { get; }
        [XmlAttribute]
        public int Statements { get; }
        [XmlAttribute]
        public string Kind { get; }

        #endregion

        #region Methods
        public bool Equals(ControlFlowGraphVertex block)
        {
            return this.block.Ordinal == block.block.Ordinal;
        }

        public int CompareTo(ControlFlowGraphVertex block)
        {
            return this.block.Ordinal.CompareTo(block.block.Ordinal);
        }

        public int CompareTo(object obj)
        {
            if (obj is ControlFlowGraphVertex)
            {
                ControlFlowGraphVertex block = obj as ControlFlowGraphVertex;
                return this.block.Ordinal.CompareTo(block.block.Ordinal);
            }
            else throw new ArgumentOutOfRangeException("Object being compared is not of type BasicBlock.");
        }
        #endregion

        #region Fields
        AnalysisEngine engine;
        BoundBlock block;
        SourceRoutineSymbol routine;
        #endregion

        #region Operators
        public static implicit operator int (ControlFlowGraphVertex v)
        {
            return v.block.Ordinal;
        }
        #endregion

    }
}
