using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.PseudoC.Passes;
using Cpp2ILAdapter.TreeView;
using LibCpp2IL;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class MethodUsedByNode(TreeView.MethodNode method) : SearchNode
{
    public static readonly Guid GUID = new Guid("D1E0CAD0-FB1E-4AD8-B45D-B032C2020DF3");
    public override Guid Guid => GUID;
    public override object? Text => "Used by";

    protected internal sealed override IEnumerable<AnalyzerTreeNodeData> FetchChildren(CancellationToken ct)
    {
        if (Cpp2ILDocumentNode.CurrentInstance == null)
            yield break;
        
        var pass = new AnalyzerPass(method);
        foreach (var typeNode in Cpp2ILDocumentNode.CurrentInstance.AllTypes)
        {
            foreach (var methodNode in typeNode.GetTreeNodeData.OfType<TreeView.MethodNode>())
            {
                pass.Found = false;
                foreach (var emitBlock in methodNode.Lifted.Value)
                {
                    if (pass.Found) break;
                    emitBlock.AcceptPass(pass);
                }

                if (pass.Found)
                    yield return new MethodNode(methodNode);
            }
        }
    }

    sealed class AnalyzerPass(TreeView.MethodNode method) : BasePass
    {
        public bool Found = false;
        
        [SkipLocalsInit]
        public override void AcceptExpression(ref Expression expression)
        {
            if (Found) return;
            if (expression is { Kind: ExpressionKind.Call, Left: ManagedFunctionReference reference }
                && reference.Method == method.Context)
                Found = true;
            else if (expression is { Left: MetadataReference metadataReference })
            {
                if ((metadataReference.Metadata.Value is Il2CppMethodDefinition def && def == method.Context.Definition)
                    || (metadataReference.Metadata.Value is Cpp2IlMethodRef mref && mref.BaseMethod == method.Context.Definition))
                    Found = true;
            }
            else if (Unsafe.As<Expression, IEmit>(ref expression) is MetadataReference metadataReference2)
            {
                if ((metadataReference2.Metadata.Value is Il2CppMethodDefinition def && def == method.Context.Definition)
                    || (metadataReference2.Metadata.Value is Cpp2IlMethodRef mref && mref.BaseMethod == method.Context.Definition))
                    Found = true;
            }
        }

        public override void AcceptBlock(Block block)
        {
        }
    }
}