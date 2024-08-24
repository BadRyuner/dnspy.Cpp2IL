using dnSpy.Contracts.Images;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public abstract class SearchNode : AnalyzerTreeNodeData 
{
    public override void Initialize() => TreeNode.LazyLoading = true;
    public override ImageReference Icon => DsImages.Search;
    public override object? ToolTip => null;
}