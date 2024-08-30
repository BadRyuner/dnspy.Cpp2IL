using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class TypeNode(TreeView.TypeNode type): ReferencedAnalyzerTreeNode(type)
{
    public static readonly Guid GUID = new Guid("D6BF6C5B-132B-4D7F-BF6B-EEAE518DCA2A");
    public override Guid Guid => GUID;
    public override IEnumerable<TreeNodeData> CreateChildren() => new TreeNodeData[] { new DerivedTypes(type), new UsedAsField(type), new UsedInParams(type), new ReturnedInMethods(type) };
}