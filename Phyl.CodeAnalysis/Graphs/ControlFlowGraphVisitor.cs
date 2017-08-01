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
    public class ControlFlowGraphVisitor : GraphVisitor
    {
        #region Constructors
        internal ControlFlowGraphVisitor(DiagnosticBag diagnostics, SourceRoutineSymbol routine)
        {
            _diagnostics = diagnostics;
            _routine = routine;
            Graph = new AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge>();
        }
        #endregion

        #region Overriden methods
        public override void VisitCFG(ControlFlowGraph x)
        {
            CS.Contract.Requires(x == _routine.ControlFlowGraph);
            InitializeReachabilityInfo(x);
            base.VisitCFG(x);
        }
        public override void VisitCFGBlock(BoundBlock x)
        {
            // is current block directly after the end of some try block?
            CS.Contract.Requires(inTryLevel == 0 || endOfTryBlocks.Count > 0);
            if (inTryLevel > 0 && endOfTryBlocks.Peek() == x) { --inTryLevel; endOfTryBlocks.Pop(); }
            Graph.AddVertex(CurrentVertex = new ControlFlowGraphVertex(x));
            base.VisitCFGBlock(x);
        }
  
        public override void VisitCFGTryCatchEdge(TryCatchEdge x)
        {
            // .Accept() on BodyBlocks traverses not only the try block but also the rest of the code
            ++inTryLevel;
            bool hasEndBlock = (x.NextBlock != null);                // if there's a block directly after try-catch-finally
            if (hasEndBlock)
            {
                endOfTryBlocks.Push(x.NextBlock);
            }  // -> add it as ending block
            x.BodyBlock.Accept(this);
            if (!hasEndBlock)
            {
                --inTryLevel;
            }                     // if there isn't dicrease tryLevel after going trough the try & rest (nothing)

            foreach (var c in x.CatchBlocks)
            {
                ++inCatchLevel;
                c.Accept(this);
                --inCatchLevel;
            }
            if (x.FinallyBlock != null)
            {
                ++inFinallyLevel;
                x.FinallyBlock.Accept(this);
                --inFinallyLevel;
            }
            base.VisitCFGTryCatchEdge(x);
        }

        public override void VisitCFGConditionalEdge(ConditionalEdge x)
        {
            Accept(x.Condition);
            if (x.Condition.ConstantValue.TryConvertToBool(out bool value))
            {
                // Process only the reachable branch, let the reachability of the other be checked later
                if (value)
                {
                    x.TrueTarget.Accept(this);
                }
                else
                {
                    x.FalseTarget.Accept(this);
                }
            }
            else
            {
                x.TrueTarget.Accept(this);
                x.FalseTarget.Accept(this);
            }
            Graph.AddEdge(new ControlFlowGraphEdge(x, CurrentVertex, new ControlFlowGraphVertex(x.TrueTarget)));
            Graph.AddEdge(new ControlFlowGraphEdge(x, CurrentVertex, new ControlFlowGraphVertex(x.FalseTarget)));
            base.VisitCFGConditionalEdge(x);
        }

        protected override void VisitCFGBlockInternal(BoundBlock x)
        {
            if (x.Tag != _visitedColor)
            {
                x.Tag = _visitedColor;
                base.VisitCFGBlockInternal(x);
            }
        }

        #endregion

        #region Methods
        private void InitializeReachabilityInfo(ControlFlowGraph x)
        {
            _visitedColor = x.NewColor();
        }

        private static LangElement PickFirstSyntaxNode(BoundBlock block)
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
        public AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge> Graph { get; protected set; }
        public ControlFlowGraphVertex CurrentVertex { get; protected set; }
        #endregion

        #region Fields
        private readonly DiagnosticBag _diagnostics;
        private SourceRoutineSymbol _routine;
        int inTryLevel = 0;
        int inCatchLevel = 0;
        int inFinallyLevel = 0;
        Stack<BoundBlock> endOfTryBlocks = new Stack<BoundBlock>();
        private int _visitedColor;
        #endregion
    }
}
