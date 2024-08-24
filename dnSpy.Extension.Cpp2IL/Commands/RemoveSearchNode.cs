using System.Linq;
using System.Windows;
using Cpp2ILAdapter.Analyzer;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Commands;

[ExportMenuItem(Header = "Remove", InputGestureText = "res:ShortCutKeyDelete", Icon = DsImagesAttribute.RemoveCommand, Group = "11,9FFB6ECB-A406-4F56-9856-3E33C6399633", Order = 0)]
sealed class RemoveSearchNode : MenuItemBase
{
    public override bool IsVisible(IMenuItemContext context)
    {
        if (context.CreatorObject.Guid != AnalyzerService.ANALYZER_TREEVIEW_GUID)
            return false;
        return true;
    }

    public override void Execute(IMenuItemContext context)
    {
        if (context.CreatorObject.Guid != AnalyzerService.ANALYZER_TREEVIEW_GUID)
            return;

        var nodes = GetNodes(context.Find<TreeNodeData[]>());
        DeleteNodes(nodes);
    }
    
    internal static TreeNodeData[]? GetNodes(TreeNodeData[] nodes) {
        if (nodes is null)
            return null;
        if (nodes.Length == 0 || !nodes.All(a => a.TreeNode.Parent is not null && a.TreeNode.Parent.Parent is null))
            return null;
        return nodes;
    }
    
    internal static void DeleteNodes(TreeNodeData[]? nodes) {
        if (nodes is not null) {
            foreach (var node in nodes) {
                //AnalyzerTreeNodeData.CancelSelfAndChildren(node);
                node.TreeNode.Parent!.Children.Remove(node.TreeNode);
            }
        }
    }
}