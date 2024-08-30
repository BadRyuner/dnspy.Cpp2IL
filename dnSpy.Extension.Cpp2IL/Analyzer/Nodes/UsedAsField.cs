using System.Linq;
using System.Threading;
using Cpp2ILAdapter.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class UsedAsField(TreeView.TypeNode type) : SearchNode
{
    public static readonly Guid GUID = new Guid("5AF4A953-D1A2-4AFC-955B-9CA41C4F2BCF");
    public override Guid Guid => GUID;
    public override object? Text => "As Field Type";
    protected internal override IEnumerable<AnalyzerTreeNodeData> FetchChildren(CancellationToken ct)
    {
        if (Cpp2ILDocumentNode.CurrentInstance == null)
            yield break;

        var types = Cpp2ILDocumentNode.CurrentInstance.AllTypes;
        for (var i = 0; i < types.Length; i++)
        {
            foreach (var fieldNode in types[i].GetTreeNodeData.OfType<TreeView.FieldNode>())
            {
                if (fieldNode.Context.FieldTypeContext == type.Context)
                    yield return new FieldNode(fieldNode);
            }
        }
    }
}