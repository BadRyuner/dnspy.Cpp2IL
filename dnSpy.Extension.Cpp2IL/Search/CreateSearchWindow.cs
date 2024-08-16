using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Input;
using Cpp2IL.Core;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Extension;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.ToolBars;
using dnSpy.Contracts.ToolWindows;
using dnSpy.Contracts.ToolWindows.App;

namespace Cpp2ILAdapter.Search;

[ExportToolBarButton(Header = "IL2Cpp Search", Icon = DsImagesAttribute.Search, Group = "10000,FEFB775B-7999-4A48-BE1C-C4314D00971F", Order = 0)]
sealed class CreateSearchWindow : ToolBarButtonCommand
{
    public CreateSearchWindow() : base(CreateSearchWindowCommand.SearchRoutedCommand)
    {
    }
    
    //public override bool IsVisible(IToolBarItemContext context) => true;
}

[ExportAutoLoaded]
sealed class CreateSearchWindowCommand : IAutoLoaded
{
    public static readonly RoutedCommand SearchRoutedCommand;
    public static IDocumentTabService SharedDocumentTabService = null!;
    
    static CreateSearchWindowCommand() {
        SearchRoutedCommand = new RoutedCommand("CreateSearchWindowCommand", typeof(CreateSearchWindowCommand));
        SearchRoutedCommand.InputGestures.Add(new KeyGesture(Key.K, ModifierKeys.Control | ModifierKeys.Shift));
    }

    readonly IDsToolWindowService toolWindowService;

    [ImportingConstructor]
    CreateSearchWindowCommand(IDsToolWindowService toolWindowService, IWpfCommandService wpfCommandService, IDocumentTabService documentTabService) {
        this.toolWindowService = toolWindowService;
        SharedDocumentTabService = documentTabService;

        var cmds = wpfCommandService.GetCommands(ControlConstants.GUID_MAINWINDOW);
        cmds.Add(SearchRoutedCommand, Search, CanSearch);
    }

    void CanSearch(object? sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;

    void Search(object? sender, ExecutedRoutedEventArgs e) => toolWindowService.Show(SearchWindowTool.THE_GUID);
}

[Export(typeof(IToolWindowContentProvider))]
sealed class SearchWindowToolContentProvider : IToolWindowContentProvider {
    SearchWindowTool SearchToolWindowContent => searchToolWindowContent ??= new SearchWindowTool();
    SearchWindowTool? searchToolWindowContent;
    
    public IEnumerable<ToolWindowContentInfo> ContentInfos {
        get { yield return new ToolWindowContentInfo(SearchWindowTool.THE_GUID, SearchWindowTool.DEFAULT_LOCATION, AppToolWindowConstants.DEFAULT_CONTENT_ORDER_TOP_SEARCH, false); }
    }
    
    public ToolWindowContent? GetOrCreate(Guid guid) {
        if (guid == SearchWindowTool.THE_GUID)
            return SearchToolWindowContent;
        return null;
    }
}

internal sealed class SearchWindowTool : ToolWindowContent, IFocusable
{
    public static readonly Guid THE_GUID = new("589eb17e-e4f0-4fde-ac2e-3679e7c26cb3");
    public const AppToolWindowLocation DEFAULT_LOCATION = AppToolWindowLocation.DefaultHorizontal;

    public SearchWindowTool()
    {
        _control = new SearchControl();
        _controlVm = new SearchControlVM(_control);
        _control.DataContext = _controlVm;
    }
    
    private readonly SearchControl _control;
    private readonly SearchControlVM _controlVm;
    public override object? UIObject => _control;
    public override IInputElement? FocusedElement => _control.searchTextBox;
    public override FrameworkElement? ZoomElement => _control;
    public override Guid Guid => THE_GUID;
    public override string Title => "Il2Cpp Search";
    public bool CanFocus => true;
    public void Focus() => _control.Focus();
}