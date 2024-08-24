using System.ComponentModel.Composition;
using System.Windows;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.ToolWindows;
using dnSpy.Contracts.ToolWindows.App;

namespace Cpp2ILAdapter.Analyzer;

[Export(typeof(IToolWindowContentProvider))]
sealed class AnalyzerToolWindowContentProvider : IToolWindowContentProvider {
    readonly Lazy<IAnalyzerService> analyzerService;

    public AnalyzerToolWindowContent DocumentTreeViewWindowContent => analyzerToolWindowContent ??= new AnalyzerToolWindowContent(analyzerService);
    AnalyzerToolWindowContent? analyzerToolWindowContent;

    [ImportingConstructor]
    AnalyzerToolWindowContentProvider(Lazy<IAnalyzerService> analyzerService) => this.analyzerService = analyzerService;

    public IEnumerable<ToolWindowContentInfo> ContentInfos {
        get { yield return new ToolWindowContentInfo(AnalyzerToolWindowContent.THE_GUID, AnalyzerToolWindowContent.DEFAULT_LOCATION, AppToolWindowConstants.DEFAULT_CONTENT_ORDER_BOTTOM_ANALYZER, false); }
    }

    public ToolWindowContent? GetOrCreate(Guid guid) => guid == AnalyzerToolWindowContent.THE_GUID ? DocumentTreeViewWindowContent : null;
}

sealed class AnalyzerToolWindowContent : ToolWindowContent, IFocusable {
    public static readonly Guid THE_GUID = new Guid("5827D694-A2DF-4D65-B1F8-ACF249508A96");
    public const AppToolWindowLocation DEFAULT_LOCATION = AppToolWindowLocation.DefaultHorizontal;
    public override IInputElement? FocusedElement => null;
    public override FrameworkElement? ZoomElement => analyzerService.Value.TreeView.UIObject;
    public override Guid Guid => THE_GUID;
    public override string Title => "IL2CPP Analyzer";
    public override object? UIObject => analyzerService.Value.TreeView.UIObject;
    public bool CanFocus => true;
    readonly Lazy<IAnalyzerService> analyzerService;
    public AnalyzerToolWindowContent(Lazy<IAnalyzerService> analyzerService) => this.analyzerService = analyzerService;
    public override void OnVisibilityChanged(ToolWindowContentVisibilityEvent visEvent) {
        if (visEvent == ToolWindowContentVisibilityEvent.Removed)
            analyzerService.Value.OnClose();
    }
    public void Focus() => analyzerService.Value.TreeView.Focus();
}