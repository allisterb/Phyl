using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Devsense.PHP.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.CodeGen;
using Pchp.CodeAnalysis.Emit;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Semantics.Graph;
using Pchp.CodeAnalysis.Semantics.Model;
using Pchp.CodeAnalysis.Symbols;

using Devsense.PHP.Syntax.Ast;
using Roslyn.Utilities;
using Pchp.CodeAnalysis;
using QuickGraph;

using Phyl.CodeAnalysis.Graphs;


namespace Phyl.CodeAnalysis
{
    /// <summary>
    /// Performs compilation of all source methods.
    /// </summary>
    public class PhylSourceRoutinesCompiler : ILogged
    {
        #region Constructors
        private PhylSourceRoutinesCompiler(PhpCompilation compilation, PEModuleBuilder moduleBuilder, bool emittingPdb, DiagnosticBag diagnostics, CancellationToken cancellationToken)
        {
            Contract.ThrowIfNull(compilation);
            Contract.ThrowIfNull(diagnostics);
            _compilation = compilation;
            _moduleBuilder = moduleBuilder;
            _emittingPdb = emittingPdb;
            _diagnostics = diagnostics;
            _cancellationToken = cancellationToken;
        }

        internal PhylSourceRoutinesCompiler(AnalysisEngine engine, PhpCompilation compilation, CancellationToken cancellationToken)
        {
            Contract.ThrowIfNull(engine);
            Contract.ThrowIfNull(compilation);
            Engine = engine;
            _compilation = compilation;
            _cancellationToken = cancellationToken;
            _moduleBuilder = null;
            _emittingPdb = false;
            _diagnostics = new DiagnosticBag();
        }
        #endregion

        #region Properties
        internal ConcurrentDictionary<SourceRoutineSymbol, AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge>> ControlFlowGraphs { get; }
            = new ConcurrentDictionary<SourceRoutineSymbol, AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge>>();
        #endregion

        #region Methods
        internal IEnumerable<Diagnostic> BindAndAnalyzeCFG()
        {
            _worklist = new Worklist<BoundBlock>(BindBlock);
            PhpCompilation.ReferenceManager manager = _compilation.GetBoundReferenceManager();
            this.WalkRoutines(this.EnqueueRoutine);
            this.WalkTypes(this.EnqueueFieldsInitializer);
            this.ProcessWorklist();
            if (!completedDiagnostics)
            {
                this.AnalyzeandDiagnoseRoutines();
            }
            return _diagnostics.AsEnumerable();
        }

        void BindBlock(BoundBlock block) // TODO: driver
        {
            // TODO: pool of CFGAnalysis
            // TODO: async
            // TODO: in parallel
            try
            {
                block.Accept(ExpressionAnalysisFactory());
            }
            catch (Exception e)
            {
                var l = PhylDiagnosingVisitor.PickFirstSyntaxNode(block);
                Tuple<int, int> pos = Engine.GetLineFromTokenPosition(l.Span.Start, l.ContainingSourceUnit.FilePath);
                L.Error(e, "Exception thrown binding block {0} in file {1} at line {2} column {3}.", block.DebugDisplay, l.ContainingSourceUnit.FilePath, pos.Item1, pos.Item2);
            }
        }

        ExpressionAnalysis ExpressionAnalysisFactory()
        {
            return new ExpressionAnalysis(_worklist, _compilation.GlobalSemantics);
        }

        void WalkRoutines(Action<SourceRoutineSymbol> action)
        {
            foreach (SourceRoutineSymbol s in _compilation.SourceSymbolCollection.AllRoutines)
            {
                try
                {
                    action.Invoke(s);
                }
                catch (Exception e)
                {
                    L.Error(e, "Exception thrown analyzing method {0} in file {1}.", s.Name, s.ContainingFile.Name);
                }
            }
            // TODO: methodsWalker.VisitNamespace(_compilation.SourceModule.GlobalNamespace)
        }

        void WalkRoutinesInParallel(Action<SourceRoutineSymbol> action)
        {
            _compilation.SourceSymbolCollection.AllRoutines.AsParallel().WithDegreeOfParallelism(Engine.MaxConcurrencyLevel).ForEach(s =>
            {
                try
                {
                    action.Invoke(s);
                }
                catch (Exception e)
                {
                    L.Error(e, "Exception thrown analyzing source method {0} in file {1}.", s.Name, s.ContainingFile.SyntaxTree.FilePath);
                }
            });
        }

        void WalkTypes(Action<SourceTypeSymbol> action)
        {
            _compilation.SourceSymbolCollection.GetTypes().Foreach(action);
        }

        void WalkTypesInParallel(Action<SourceTypeSymbol> action)
        {
            _compilation.SourceSymbolCollection.GetTypes().AsParallel().WithDegreeOfParallelism(Engine.MaxConcurrencyLevel).ForEach(t =>
            {
                try
                {
                    action.Invoke(t);
                }
                catch (Exception e)
                {
                    L.Error(e, "Exception thrown analyzing source type {0} in file {1}.", t.Name, t.ContainingFile.SyntaxTree.FilePath);
                }
            });
        }


        /// <summary>
        /// Enqueues routine's start block for analysis.
        /// </summary>
        void EnqueueRoutine(SourceRoutineSymbol routine)
        {
            Contract.ThrowIfNull(routine);

            // lazily binds CFG and
            // adds their entry block to the worklist

            // TODO: reset LocalsTable, FlowContext and CFG

            _worklist.Enqueue(routine.ControlFlowGraph?.Start);

            // enqueue routine parameter default values
            routine.Parameters.OfType<SourceParameterSymbol>().Foreach(p =>
            {
                if (p.Initializer != null)
                {
                    EnqueueExpression(p.Initializer, routine.TypeRefContext, routine.GetNamingContext());
                }
            });
        }

        /// <summary>
        /// Enqueues the standalone expression for analysis.
        /// </summary>
        void EnqueueExpression(BoundExpression expression, TypeRefContext/*!*/ctx, NamingContext naming)
        {
            Contract.ThrowIfNull(expression);
            Contract.ThrowIfNull(ctx);

            var dummy = new BoundBlock()
            {
                FlowState = new FlowState(new FlowContext(ctx, null)),
                Naming = naming
            };

            dummy.Add(new BoundExpressionStatement(expression));

            _worklist.Enqueue(dummy);
        }

        /// <summary>
        /// Enqueues initializers of a class fields and constants.
        /// </summary>
        void EnqueueFieldsInitializer(SourceTypeSymbol type)
        {
            type.GetMembers().OfType<SourceFieldSymbol>().Foreach(f =>
            {
                if (f.Initializer != null)
                {
                    EnqueueExpression(
                        f.Initializer,
                        TypeRefFactory.CreateTypeRefContext(type), //the context will be lost, analysis resolves constant values only and types are temporary
                        NameUtils.GetNamingContext(type.Syntax));
                }
            });
        }

        internal void ProcessWorklist()
        {
            _worklist.DoAll();
        }

        internal void AnalyzeBlocks(Worklist<BoundBlock>.AnalyzeBlockDelegate[] block_analyzers)
        {
            _worklist = new Worklist<BoundBlock>(block_analyzers);
            this.WalkRoutines(this.EnqueueRoutine);
            this.WalkTypes(this.EnqueueFieldsInitializer);
            this.ProcessWorklist();
        }

        internal void AnalyzeSourceRoutines(params Action<SourceRoutineSymbol>[] routine_analyzers)
        {
            foreach (Action<SourceRoutineSymbol> a in routine_analyzers)
            {
                this.WalkRoutinesInParallel(a);
            }
        }

        internal void AnalyzeandDiagnoseRoutines()
        {
            this.WalkRoutinesInParallel(AnalyzeandDiagnoseRoutine);
            completedDiagnostics = true;
        }

        private void AnalyzeandDiagnoseRoutine(SourceRoutineSymbol routine)
        {
            Contract.ThrowIfNull(routine);

            if (routine.ControlFlowGraph != null)   // non-abstract method
            {
                PhylDiagnosingVisitor diagnosingVisitor = new PhylDiagnosingVisitor(_diagnostics, routine);
                diagnosingVisitor.VisitCFG(routine.ControlFlowGraph);
                AdjacencyGraph<ControlFlowGraphVertex, ControlFlowGraphEdge> g = diagnosingVisitor.Graph;
                string r = routine.ContainingFile.SyntaxTree.FilePath + "_" + routine.Name;
                ControlFlowGraphs.TryAdd(routine, g);
            }
        }

        private void AnalyzeTypes()
        {
            this.WalkTypes(AnalyzeType);
        }

        private void AnalyzeType(SourceTypeSymbol type)
        {
            // resolves base types in here
            var btype = type.BaseType;

            // ...
        }

        internal void EmitMethodBodies()
        {
            Debug.Assert(_moduleBuilder != null);

            // source routines
            this.WalkRoutines(this.EmitMethodBody);
        }

        internal void EmitSynthesized()
        {
            // TODO: Visit every symbol with Synthesize() method and call it instead of followin

            // ghost stubs
            this.WalkRoutines(f => f.SynthesizeGhostStubs(_moduleBuilder, _diagnostics));

            // initialize RoutineInfo
            _compilation.SourceSymbolCollection.GetFunctions()
                .ForEach(f => f.EmitInit(_moduleBuilder));

            _compilation.SourceSymbolCollection.GetLambdas()
                .ForEach(f => f.EmitInit(_moduleBuilder));

            // __statics.Init, .phpnew, .ctor
            WalkTypes(t => t.EmitInit(_moduleBuilder, _diagnostics));

            // realize .cctor if any
            _moduleBuilder.RealizeStaticCtors();
        }

        /// <summary>
        /// Generates analyzed method.
        /// </summary>
        void EmitMethodBody(SourceRoutineSymbol routine)
        {
            Contract.ThrowIfNull(routine);

            if (routine.ControlFlowGraph != null)   // non-abstract method
            {
                Debug.Assert(routine.ControlFlowGraph.Start.FlowState != null);

                var body = MethodGenerator.GenerateMethodBody(_moduleBuilder, routine, 0, null, _diagnostics, _emittingPdb);
                _moduleBuilder.SetMethodBody(routine, body);
            }
        }

        void CompileEntryPoint()
        {
            if (_compilation.Options.OutputKind.IsApplication() && _moduleBuilder != null)
            {
                var entryPoint = _compilation.GetEntryPoint(_cancellationToken);
                if (entryPoint != null)
                {
                    // wrap call to entryPoint within real <Script>.EntryPointSymbol
                    _moduleBuilder.CreateEntryPoint((MethodSymbol)entryPoint, _diagnostics);

                    //
                    Debug.Assert(_moduleBuilder.ScriptType.EntryPointSymbol != null);
                    _moduleBuilder.SetPEEntryPoint(_moduleBuilder.ScriptType.EntryPointSymbol, _diagnostics);
                }
            }
        }

        void CompileReflectionEnumerators()
        {
            Debug.Assert(_moduleBuilder != null);

            _moduleBuilder.CreateEnumerateReferencedFunctions(_diagnostics);
            _moduleBuilder.CreateEnumerateReferencedTypes(_diagnostics);
            _moduleBuilder.CreateEnumerateScriptsSymbol(_diagnostics);
            _moduleBuilder.CreateEnumerateConstantsSymbol(_diagnostics);
        }

        internal static void CompileSources(
            PhpCompilation compilation,
            PEModuleBuilder moduleBuilder,
            bool emittingPdb,
            bool hasDeclarationErrors,
            DiagnosticBag diagnostics,
            CancellationToken cancellationToken)
        {
            Debug.Assert(moduleBuilder != null);

            // ensure flow analysis and collect diagnostics
            var declarationDiagnostics = compilation.GetDeclarationDiagnostics(cancellationToken);
            diagnostics.AddRange(declarationDiagnostics);

            if (hasDeclarationErrors |= declarationDiagnostics.HasAnyErrors() || cancellationToken.IsCancellationRequested)
            {
                // cancel the operation if there are errors
                return;
            }

            //
            var compiler = new PhylSourceRoutinesCompiler(compilation, moduleBuilder, emittingPdb, diagnostics, cancellationToken);

            // Emit method bodies
            //   a. declared routines
            //   b. synthesized symbols
            compiler.EmitMethodBodies();
            compiler.EmitSynthesized();
            compiler.CompileReflectionEnumerators();

            // Entry Point (.exe)
            compiler.CompileEntryPoint();
        }
        #endregion

        #region Fields
        protected PhylLogger<PhylSourceRoutinesCompiler> L = new PhylLogger<PhylSourceRoutinesCompiler>();
        readonly AnalysisEngine Engine;
        readonly PhpCompilation _compilation;
        readonly PEModuleBuilder _moduleBuilder;
        readonly bool _emittingPdb;
        readonly DiagnosticBag _diagnostics;
        readonly CancellationToken _cancellationToken;
        bool completedDiagnostics = false;
        Worklist<BoundBlock> _worklist;
        #endregion
    }
}
