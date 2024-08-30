using System.Threading;
using Cpp2ILAdapter.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class DerivedTypes(TreeView.TypeNode type) : SearchNode
{
    public static readonly Guid GUID = new Guid("F769BBD5-8702-4765-A8C4-D6032723C2DB");
    public override Guid Guid => GUID;
    public override object? Text => "Derived";
    protected internal override IEnumerable<AnalyzerTreeNodeData> FetchChildren(CancellationToken ct)
    {
        if (Cpp2ILDocumentNode.CurrentInstance == null)
            yield break;

        var types = Cpp2ILDocumentNode.CurrentInstance.AllTypes;
        for (var i = 0; i < types.Length; i++)
        {
            var ty = types[i];
            if (ty.Context.BaseType == type.Context)
                yield return new TypeNode(ty);
        }
    }
}