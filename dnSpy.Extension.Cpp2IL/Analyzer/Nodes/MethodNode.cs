using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public sealed class MethodNode(TreeView.MethodNode method) : RefencedAnalyzerTreeNode(method)
{
    public static readonly Guid GUID = new Guid("37CB861A-0049-4E26-BCA6-62A5F2D26712");
    public override Guid Guid => GUID;

    public override IEnumerable<TreeNodeData> CreateChildren() => new[] { new MethodUsedByNode(method) };
}