using System.ComponentModel.Composition;
using Cpp2ILAdapter.Analyzer;
using Cpp2ILAdapter.References;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.ToolWindows.App;

namespace Cpp2ILAdapter.Commands;

[ExportMenuItem(Header = "Analyze", Icon = DsImagesAttribute.Search, Group = "11,9FFB6ECB-A406-4F56-9856-3E33C6399602", Order = 0)]
sealed class AnalyzeCommand : MenuItemBase
{
    private readonly IDocumentTreeView _documentTreeView;
    private readonly IDsToolWindowService _toolWindowService;
    private readonly IAnalyzerService _analyzerService;

    [ImportingConstructor]
    public AnalyzeCommand(IDocumentTreeView documentTreeView, IDsToolWindowService toolWindowService, IAnalyzerService analyzerService)
    {
        _documentTreeView = documentTreeView;
        _toolWindowService = toolWindowService;
        _analyzerService = analyzerService;
    }

    
    public override bool IsVisible(IMenuItemContext context)
    {
        if (context.Find<IDocumentViewer>() is null)
            return false;

        return context.Find<TextReference>()?.Reference is TreeView.MethodNode 
            or TreeView.FieldNode 
            or Cpp2ILMethodReference 
            or Cpp2ILMethodReferenceFromRef
            or Cpp2ILFieldReference
            or Cpp2ILDirectReference
            or Cpp2ILTypeDefReference
            or TreeView.TypeNode;
    }

    public override void Execute(IMenuItemContext context)
    {
        AnalyzerTreeNodeData? node = context.Find<TextReference>()?.Reference switch
        {
            TreeView.MethodNode mn => new Analyzer.Nodes.MethodNode(mn),
            TreeView.FieldNode fn => new Analyzer.Nodes.FieldNode(fn),
            TreeView.TypeNode tn => new Analyzer.Nodes.TypeNode(tn),
            Cpp2ILMethodReference mr => _documentTreeView.FindNode(mr) is TreeView.MethodNode mnn ? new Analyzer.Nodes.MethodNode(mnn) : null,
            Cpp2ILMethodReferenceFromRef mr => _documentTreeView.FindNode(mr) is TreeView.MethodNode mnn ? new Analyzer.Nodes.MethodNode(mnn) : null,
            Cpp2ILFieldReference fr => _documentTreeView.FindNode(fr) is TreeView.FieldNode fnn ? new Analyzer.Nodes.FieldNode(fnn) : null,
            Cpp2ILDirectReference dr => new Analyzer.Nodes.TypeNode(dr.Node),
            Cpp2ILTypeDefReference tn => _documentTreeView.FindNode(tn) is TreeView.TypeNode tnn ? new Analyzer.Nodes.TypeNode(tnn) : null,
            _ => null
        };
        
        if (node == null)
            return;
        
        _analyzerService.Add(node);
        _toolWindowService.Show(AnalyzerToolWindowContent.THE_GUID);
    }
}