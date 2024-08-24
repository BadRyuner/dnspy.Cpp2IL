using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using dnlib.DotNet;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.TreeView;
using dnSpy.Contracts.TreeView.Text;
using Microsoft.VisualStudio.Text;

namespace Cpp2ILAdapter.Analyzer;

public interface IAnalyzerService {
    ITreeView TreeView { get; }
    
    void OnClose();
    
    void Add(AnalyzerTreeNodeData node);
}

[Export(typeof(IAnalyzerService))]
sealed class AnalyzerService : IAnalyzerService {
    public static readonly Guid ANALYZER_TREEVIEW_GUID = new Guid("89818FFA-1384-4AA7-9577-3CB096195126");

    public static AnalyzerService Instance = null!;
    
    sealed class GuidObjectsProvider : IGuidObjectsProvider {
        readonly ITreeView treeView;

        public GuidObjectsProvider(ITreeView treeView) => this.treeView = treeView;

        public IEnumerable<GuidObject> GetGuidObjects(GuidObjectsProviderArgs args) {
            yield return new GuidObject(MenuConstants.GUIDOBJ_TREEVIEW_NODES_ARRAY_GUID, treeView.TopLevelSelection);
        }
    }
    
    public ITreeView TreeView { get; }

    public readonly IDocumentTabService DocumentTabService;

    [ImportingConstructor]
    AnalyzerService(IWpfCommandService wpfCommandService, IDocumentTabService documentTabService, ITreeViewService treeViewService, IMenuService menuService, IDotNetImageService dotNetImageService, IDecompilerService decompilerService, ITreeViewNodeTextElementProvider treeViewNodeTextElementProvider)
    {
        Instance = this;
        DocumentTabService = documentTabService;

        var options = new TreeViewOptions {
            CanDragAndDrop = false
        };
        TreeView = treeViewService.Create(ANALYZER_TREEVIEW_GUID, options);
        menuService.InitializeContextMenu(TreeView.UIObject, ANALYZER_TREEVIEW_GUID, new GuidObjectsProvider(TreeView));
    }
    
    public void OnClose() => ClearAll();

    void ClearAll() {
	    TreeView.Root.Children.Clear();
    }

    public void Add(AnalyzerTreeNodeData node) {
	    TreeView.Root.Children.Add(TreeView.Create(node));
	    node.TreeNode.IsExpanded = true;
	    TreeView.SelectItems(new TreeNodeData[] { node });
	    TreeView.Focus();
    }
}