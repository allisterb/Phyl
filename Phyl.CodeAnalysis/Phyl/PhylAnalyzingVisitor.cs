using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

namespace Phyl.CodeAnalysis
{
    internal class PhylGraphVisitor : GraphVisitor
    {
        private readonly DiagnosticBag _diagnostics;
        private SourceRoutineSymbol _routine;

        int inTryLevel = 0;
        int inCatchLevel = 0;
        int inFinallyLevel = 0;

        Stack<BoundBlock> endOfTryBlocks = new Stack<BoundBlock>();

        public PhylGraphVisitor(DiagnosticBag diagnostics, SourceRoutineSymbol routine)
        {
            _diagnostics = diagnostics;
            _routine = routine;
        }

        public override void VisitCFG(ControlFlowGraph x)
        {
            Debug.Assert(x == _routine.ControlFlowGraph);

            InitializeReachabilityInfo(x);

            base.VisitCFG(x);

            // TODO: Report also unreachable code caused by situations like if (false) { ... }
            CheckUnreachableCode(x);
        }

        public override void VisitEval(BoundEvalEx x)
        {
            
            base.VisitEval(x);
        }

        public override void VisitTypeRef(BoundTypeRef typeRef)
        {
            if (typeRef != null)
            {
                CheckUndefinedType(typeRef);
                base.VisitTypeRef(typeRef);
            }
        }

        public override void VisitGlobalFunctionCall(BoundGlobalFunctionCall x)
        {
            CheckUndefinedFunctionCall(x);
            base.VisitGlobalFunctionCall(x);
        }

        public override void VisitInstanceFunctionCall(BoundInstanceFunctionCall call)
        {
            // TODO: Enable the diagnostic when several problems are solved (such as __call())
            //CheckUndefinedMethodCall(call, call.Instance?.ResultType, call.Name);
            base.VisitInstanceFunctionCall(call);
        }

        public override void VisitStaticFunctionCall(BoundStaticFunctionCall call)
        {
            // TODO: Enable the diagnostic when the __callStatic() method is properly processed during analysis
            //CheckUndefinedMethodCall(call, call.TypeRef?.ResolvedType, call.Name);
            base.VisitStaticFunctionCall(call);
        }

        public override void VisitVariableRef(BoundVariableRef x)
        {
            CheckUninitializedVariableUse(x);
            base.VisitVariableRef(x);
        }

        public override void VisitSynthesizedVariableRef(BoundSynthesizedVariableRef x)
        {
            // do not make diagnostics on syntesized variables
        }

        public override void VisitDeclareStatement(BoundDeclareStatement x)
        {
            _diagnostics.Add(
                _routine,
                ((DeclareStmt)x.PhpSyntax).GetDeclareClauseSpan(),
                ErrorCode.WRN_NotYetImplementedIgnored,
                "Declare construct");

            base.VisitDeclareStatement(x);
        }

        private void CheckUndefinedFunctionCall(BoundGlobalFunctionCall x)
        {
            if (x.Name.IsDirect && x.TargetMethod.IsErrorMethod())
            {
                var errmethod = (ErrorMethodSymbol)x.TargetMethod;
                if (errmethod != null && errmethod.ErrorKind == ErrorMethodKind.Missing)
                {
                    _diagnostics.Add(_routine, ((FunctionCall)x.PhpSyntax).NameSpan.ToTextSpan(), ErrorCode.WRN_UndefinedFunctionCall, x.Name.NameValue.ToString());
                }
            }
        }

        private void CheckUndefinedMethodCall(BoundRoutineCall x, TypeSymbol type, BoundRoutineName name)
        {
            if (name.IsDirect && x.TargetMethod.IsErrorMethod() && type != null && !type.IsErrorType())
            {
                _diagnostics.Add(_routine, ((FunctionCall)x.PhpSyntax).NameSpan.ToTextSpan(), ErrorCode.WRN_UndefinedMethodCall, name.NameValue.ToString(), type.Name);
            }
        }

        private void CheckUninitializedVariableUse(BoundVariableRef x)
        {
            if (x.MaybeUninitialized && !x.Access.IsQuiet)
            {
                _diagnostics.Add(_routine, x.PhpSyntax, ErrorCode.WRN_UninitializedVariableUse, x.Name.NameValue.ToString());
            }
        }

        private void CheckUndefinedType(BoundTypeRef typeRef)
        {
            // Ignore indirect types (e.g. $foo = new $className())
            if (typeRef.IsDirect && (typeRef.ResolvedType == null || typeRef.ResolvedType.IsErrorType()))
            {
                var errtype = typeRef.ResolvedType as ErrorTypeSymbol;
                if (errtype != null && errtype.CandidateReason == CandidateReason.Ambiguous)
                {
                    // type is declared but ambiguously,
                    // warning with declaration ambiguity was already reported, we may skip following
                    return;
                }

                if (typeRef.TypeRef is ReservedTypeRef)
                {
                    // unresolved parent, self ?
                }
                else
                {
                    var name = typeRef.TypeRef.QualifiedName?.ToString();
                    _diagnostics.Add(this._routine, typeRef.TypeRef, ErrorCode.WRN_UndefinedType, name);
                }
            }
        }

        public override void VisitCFGBlock(BoundBlock x)
        {
            // is current block directly after the end of some try block?
            Debug.Assert(inTryLevel == 0 || endOfTryBlocks.Count > 0);
            if (inTryLevel > 0 && endOfTryBlocks.Peek() == x) { --inTryLevel; endOfTryBlocks.Pop(); }

            base.VisitCFGBlock(x);
        }

        public override void VisitCFGTryCatchEdge(TryCatchEdge x)
        {
            // .Accept() on BodyBlocks traverses not only the try block but also the rest of the code
            ++inTryLevel;
            var hasEndBlock = (x.NextBlock != null);                // if there's a block directly after try-catch-finally
            if (hasEndBlock) { endOfTryBlocks.Push(x.NextBlock); }  // -> add it as ending block
            x.BodyBlock.Accept(this);
            if (!hasEndBlock) { --inTryLevel; }                     // if there isn't dicrease tryLevel after going trough the try & rest (nothing)

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
        }

        public override void VisitStaticStatement(BoundStaticVariableStatement x)
        {
            base.VisitStaticStatement(x);
        }

        public override void VisitYieldStatement(BoundYieldStatement boundYieldStatement)
        {

            // TODO: Start supporting yielding from exception handling constructs.
            if (inTryLevel > 0 || inCatchLevel > 0 || inFinallyLevel > 0)
            {
                _diagnostics.Add(_routine, boundYieldStatement.PhpSyntax, ErrorCode.ERR_NotYetImplemented, "Yielding from an exception handling construct (try, catch, finally)");
            }
        }

        
            private int _visitedColor;

            Queue<BoundBlock> _unreachableQueue = new Queue<BoundBlock>();

            private void InitializeReachabilityInfo(ControlFlowGraph x)
            {
                _visitedColor = x.NewColor();
            }

            protected override void VisitCFGBlockInternal(BoundBlock x)
            {
                if (x.Tag != _visitedColor)
                {
                    x.Tag = _visitedColor;
                    base.VisitCFGBlockInternal(x);
                }
            }

            public override void VisitCFGConditionalEdge(ConditionalEdge x)
            {
                Accept(x.Condition);

                if (x.Condition.ConstantValue.TryConvertToBool(out bool value))
                {
                    // Process only the reachable branch, let the reachability of the other be checked later
                    if (value)
                    {
                        _unreachableQueue.Enqueue(x.FalseTarget);
                        x.TrueTarget.Accept(this);
                    }
                    else
                    {
                        _unreachableQueue.Enqueue(x.TrueTarget);
                        x.FalseTarget.Accept(this);
                    }
                }
                else
                {
                    x.TrueTarget.Accept(this);
                    x.FalseTarget.Accept(this);
                }
            }

            private void CheckUnreachableCode(ControlFlowGraph graph)
            {
                graph.UnreachableBlocks.ForEach(_unreachableQueue.Enqueue);

                while (_unreachableQueue.Count > 0)
                {
                    var block = _unreachableQueue.Dequeue();

                    // Skip the block if it was either proven reachable before or if it was already processed
                    if (block.Tag == _visitedColor)
                    {
                        continue;
                    }

                    block.Tag = _visitedColor;

                    var syntax = PickFirstSyntaxNode(block);
                    if (syntax != null)
                    {
                        // Report the diagnostic for the first unreachable statement
                        _diagnostics.Add(this._routine, syntax, ErrorCode.WRN_UnreachableCode);
                    }
                    else
                    {
                        // If there is no statement to report the diagnostic for, search further
                        // - needed for while, do while and scenarios such as if (...) { return; } else { return; } ...
                        block.NextEdge?.Targets.ForEach(_unreachableQueue.Enqueue);
                    }

                }
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
    }
}
