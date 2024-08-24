using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public abstract class RefencedAnalyzerTreeNode(DsDocumentNode node) : AnalyzerTreeNodeData
{
    public override object? Text => node.Text;
    public override object? ToolTip => node.ToolTip;
    
    public override ImageReference Icon => node.Icon;

    public override bool Activate()
    {
        AnalyzerService.Instance!.DocumentTabService.FollowReference(node);
        return true;
    }
}