using System.Linq;
using System.Threading;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

/*
public class FieldUsedByNode(TreeView.FieldNode field) : SearchNode
{
    public static readonly Guid GUID = new Guid("6EA8F293-8579-4EC7-BE73-C1A272A15F16");
    public override Guid Guid => GUID;
    public override object? Text => "Used by";
    
    protected internal sealed override IEnumerable<AnalyzerTreeNodeData> FetchChildren(CancellationToken ct)
    {
        if (Cpp2ILDocumentNode.CurrentInstance == null)
            yield break;
        
        var pass = new AnalyzerPass(field);
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
    
    sealed class AnalyzerPass(TreeView.FieldNode field) : BasePass
    {
        public bool Found = false;
        public override void AcceptExpression(ref Expression expression)
        {
            if (Found) return;
            if (expression is Expression { Right: AccessField reference }
                && reference.Field == field.Context)
                Found = true;
            else if (expression is Expression { Left: AccessField reference2 }
                     && reference2.Field == field.Context)
                Found = true;
        }

        public override void AcceptBlock(Block block)
        {
        }
    }
}
*/