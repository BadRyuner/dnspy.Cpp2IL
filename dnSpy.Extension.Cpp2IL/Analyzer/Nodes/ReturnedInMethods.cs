using System.Linq;
using System.Threading;
using Cpp2ILAdapter.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class ReturnedInMethods(TreeView.TypeNode type) : SearchNode
{
    public static readonly Guid GUID = new Guid("CC2F6F0D-DCED-4EA0-8389-C9A4E58F03F3");
    public override Guid Guid => GUID;
    public override object? Text => "As Return Result";
    protected internal override IEnumerable<AnalyzerTreeNodeData> FetchChildren(CancellationToken ct)
    {
        if (Cpp2ILDocumentNode.CurrentInstance == null)
            yield break;

        var types = Cpp2ILDocumentNode.CurrentInstance.AllTypes;
        for (var i = 0; i < types.Length; i++)
        {
            foreach (var methodNode in types[i].GetTreeNodeData.OfType<TreeView.MethodNode>())
            {
                if (methodNode.Context.ReturnTypeContext == type.Context)
                    yield return new MethodNode(methodNode);
            }
        }
    }
}