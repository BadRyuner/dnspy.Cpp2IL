using System.IO;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.IsilEcho;
using Cpp2ILAdapter.TreeView;
using Echo.Ast;
using Echo.Ast.Construction;
using Echo.ControlFlow;
using Echo.ControlFlow.Construction.Static;
using Echo.ControlFlow.Construction.Symbolic;

namespace Cpp2ILAdapter;

public static class IsilLifter
{
    public static List<object> Lift(MethodAnalysisContext context, Cpp2ILDocument document)
    {
        var arch = new IsilArchitecture(document);
        var transitioner = new IsilStateTransitioner(arch, context.ConvertedIsil);
        var builder = new SymbolicFlowGraphBuilder<InstructionSetIndependentInstruction>(arch, context.ConvertedIsil, transitioner);
        var cfg = builder.ConstructFlowGraph(1, Array.Empty<long>());
        //var ast = cfg.Lift(IsilPurityClassifier.Shared); // bad works without stack shit
            
        var list = new List<object>();
        return list;
    }
}