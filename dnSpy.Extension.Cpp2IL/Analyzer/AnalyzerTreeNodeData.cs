using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer;

public abstract class AnalyzerTreeNodeData : TreeNodeData
{
    public override void OnRefreshUI()
    {
    }
    
    public static void CancelSelfAndChildren(TreeNodeData node) {
        foreach (var c in node.DescendantsAndSelf()) {
            if (c is IAsyncCancellable id)
                id.Cancel();
        }
    }
}

public interface IAsyncCancellable
{
    void Cancel();
}