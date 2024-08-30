using System.Linq;
using System.Threading;
using Cpp2ILAdapter.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class UsedInParams(TreeView.TypeNode type) : SearchNode
{
    public static readonly Guid GUID = new Guid("94E28384-7F4D-4CD2-834F-6D45564E16D4");
    public override Guid Guid => GUID;
    public override object? Text => "As Parameter";
    protected internal override IEnumerable<AnalyzerTreeNodeData> FetchChildren(CancellationToken ct)
    {
        if (Cpp2ILDocumentNode.CurrentInstance == null)
            yield break;

        var types = Cpp2ILDocumentNode.CurrentInstance.AllTypes;
        for (var i = 0; i < types.Length; i++)
        {
            foreach (var methodNode in types[i].GetTreeNodeData.OfType<TreeView.MethodNode>())
            {
                if (methodNode.Context.Parameters.Any(p => p.ParameterTypeContext == type.Context))
                    yield return new MethodNode(methodNode);
            }
        }
    }
}