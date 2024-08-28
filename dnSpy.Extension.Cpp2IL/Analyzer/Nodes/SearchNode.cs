using System.Threading;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public abstract class SearchNode : AnalyzerTreeNodeData 
{
    public override void Initialize() => TreeNode.LazyLoading = true;
    public override ImageReference Icon => DsImages.Search;
    public override object? ToolTip => null;

    private AsyncFetchChildrenHelper? _helper;
    protected internal abstract IEnumerable<AnalyzerTreeNodeData> FetchChildren(CancellationToken ct);

    public sealed override IEnumerable<TreeNodeData> CreateChildren()
    {
        _helper = new(this, () => _helper = null);
        yield break;
    }

    public sealed override void OnIsVisibleChanged() {
        if (!TreeNode.IsVisible && _helper is not null && !_helper.CompletedSuccessfully) {
            CancelAndClearChildren();
            TreeNode.LazyLoading = true;
        }
    }

    public sealed override void OnIsExpandedChanged(bool isExpanded) {
        if (!isExpanded && _helper is not null && !_helper.CompletedSuccessfully) {
            CancelAndClearChildren();
            TreeNode.LazyLoading = true;
        }
    }
    
    void CancelAndClearChildren() {
        AnalyzerTreeNodeData.CancelSelfAndChildren(this);
        TreeNode.Children.Clear();
        TreeNode.LazyLoading = true;
    }
}