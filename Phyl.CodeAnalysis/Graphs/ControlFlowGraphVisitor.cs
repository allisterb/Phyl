using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CS = System.Diagnostics.Contracts;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Pchp.CodeAnalysis.Errors;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Semantics.Graph;
using Pchp.CodeAnalysis.Symbols;
using Pchp.CodeAnalysis.FlowAnalysis;
using Devsense.PHP.Syntax.Ast;

using Pchp.CodeAnalysis;

using QuickGraph;

namespace Phyl.CodeAnalysis.Graphs
{
    internal class ControlFlowGraphVisitor : GraphVisitor
    {
        #region Constructors
        internal ControlFlowGraphVisitor(AnalysisEngine engine, SourceRoutineSymbol routine) : base()
        {
            Engine = engine;
            _routine = routine;
        }
        #endregion

        #region Overriden methods
        public override void VisitCFGBlock(BoundBlock x)
        {
            Vertices.Add(new ControlFlowGraphVertex(_routine, x));
            base.VisitCFGBlock(x);
        }

        public override void VisitCFGSimpleEdge(SimpleEdge x)
        {
            ControlFlowGraphVertex v = new ControlFlowGraphVertex(_routine, x.NextBlock);
            this.Edges.Add(new ControlFlowGraphEdge(x, this.Vertices.Last(), v));
            base.VisitCFGSimpleEdge(x);
        }

        public override void VisitCFGConditionalEdge(ConditionalEdge x)
        {
            ControlFlowGraphVertex t = new ControlFlowGraphVertex(_routine, x.TrueTarget);
            this.Edges.Add(new ControlFlowGraphEdge(x, this.Vertices.Last(), t));
            ControlFlowGraphVertex f = new ControlFlowGraphVertex(_routine, x.FalseTarget);
            this.Edges.Add(new ControlFlowGraphEdge(x, this.Vertices.Last(), f));
            this.Vertices.Add(t);
            this.Vertices.Add(f);
            base.VisitCFGConditionalEdge(x);
        }
        #endregion

        #region Methods
        internal static LangElement PickFirstSyntaxNode(BoundBlock block)
        {
            var syntax = block.Statements.FirstOrDefault(st => st.PhpSyntax != null)?.PhpSyntax;
            if (syntax != null)
            {
                return syntax;
            }

            // TODO: Mark the first keyword (if, switch, foreach,...) instead
            switch (block.NextEdge)
            {
                case ForeachEnumereeEdge edge:
                    return edge.Enumeree.PhpSyntax;

                case SimpleEdge edge:
                    return edge.PhpSyntax;

                case ConditionalEdge edge:
                    return edge.Condition.PhpSyntax;

                case TryCatchEdge edge:
                    return PickFirstSyntaxNode(edge.BodyBlock);

                case SwitchEdge edge:
                    return edge.SwitchValue.PhpSyntax;

                default:
                    return null;
            }
        }
        #endregion

        #region Properties
        public AnalysisEngine Engine { get; protected set; }
        public List<ControlFlowGraphVertex> Vertices { get; } = new List<ControlFlowGraphVertex>();
        public List<ControlFlowGraphEdge> Edges { get; } = new List<ControlFlowGraphEdge>();
        #endregion

        #region readonFields
        private SourceRoutineSymbol _routine;
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        #endregion
    }
}
