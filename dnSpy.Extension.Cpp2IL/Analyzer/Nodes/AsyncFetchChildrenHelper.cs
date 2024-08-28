using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class AsyncFetchChildrenHelper : AsyncNodeProvider 
{
    readonly SearchNode _node;
    readonly Action _completed;
    
    public AsyncFetchChildrenHelper(SearchNode node, Action completed)
        : base(node) {
        this._node = node;
        this._completed = completed;
        Start();
    }

    protected override void ThreadMethod()
    {
        foreach (var child in _node.FetchChildren(cancellationToken))
            AddNode(child);
    }

    protected override void OnCompleted() => _completed();
}