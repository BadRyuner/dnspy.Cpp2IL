using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class FieldNode(TreeView.FieldNode field) : RefencedAnalyzerTreeNode(field)
{
    public static readonly Guid GUID = new Guid("37CB861A-1049-4E26-BCA2-62A5F2D26712");
    public override Guid Guid => GUID;

    public override IEnumerable<TreeNodeData> CreateChildren() => new TreeNodeData[] { new FieldUsedByNode(field) };
}