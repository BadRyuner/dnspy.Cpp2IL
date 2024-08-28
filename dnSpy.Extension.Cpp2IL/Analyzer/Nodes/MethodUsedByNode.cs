using System.Linq;
using System.Threading;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.PseudoC.Passes;
using Cpp2ILAdapter.TreeView;

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
        public override void AcceptExpression(ref Expression expression)
        {
            if (Found) return;
            if (expression is Expression { Kind: ExpressionKind.Call, Left: ManagedFunctionReference reference }
                && reference.Method == method.Context)
                Found = true;
        }

        public override void AcceptBlock(Block block)
        {
        }
    }
}