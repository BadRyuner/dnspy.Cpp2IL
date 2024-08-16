using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Cpp2ILAdapter.TreeView;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.Search;
using FieldNode = Cpp2ILAdapter.TreeView.FieldNode;
using MethodNode = Cpp2ILAdapter.TreeView.MethodNode;

namespace Cpp2ILAdapter.Search;

internal sealed class SearchControlVM : ViewModelBase
{
    public class SearchSettingsContainer
    {
        SearchType SearchType { get; set; }
        public bool SyntaxHighlight { get; set; }
        public bool MatchWholeWords { get; set; }
        public bool CaseSensitive { get; set; }
        public bool MatchAnySearchTerm { get; set; }
        public bool SearchDecompiledData { get; set; }
        public bool SearchFrameworkAssemblies { get; set; }
        public bool SearchCompilerGeneratedMembers { get; set; }
    }
    
    private readonly SearchControl _control;

    public SearchSettingsContainer SearchSettings { get; set; } = new();
    
    public SearchControlVM(SearchControl control)
    {
        _control = control;
        SearchTypeVMs = new();
        
        Add(SearchType.TypeDef, "Type", DsImages.ClassPublic, "Type_Key", VisibleMembersFlags.TypeDef);
        Add(SearchType.FieldDef, "Field", DsImages.FieldPublic, "Field_Key", VisibleMembersFlags.FieldDef);
        Add(SearchType.MethodDef, "Method", DsImages.MethodPublic, "Method_Key", VisibleMembersFlags.MethodDef);
        //Add(SearchType.PropertyDef, Property, DsImages.Property, Property_Key, VisibleMembersFlags.PropertyDef);
        //Add(SearchType.EventDef, Event, DsImages.EventPublic, Event_Key, VisibleMembersFlags.EventDef);
        //Add(SearchType.ParamDef, Parameter, DsImages.Parameter, Parameter_Key, VisibleMembersFlags.ParamDef);
        //Add(SearchType.Local, Local, DsImages.LocalVariable, Local_Key, VisibleMembersFlags.Local);
        //Add(SearchType.ParamLocal, ParameterLocal, DsImages.LocalVariable, ParameterLocal_Key, VisibleMembersFlags.ParamDef | VisibleMembersFlags.Local);
        //Add(SearchType.AssemblyRef, AssemblyRef, DsImages.Reference, null, VisibleMembersFlags.AssemblyRef);
        //Add(SearchType.ModuleRef, ModuleRef, DsImages.Reference, null, VisibleMembersFlags.ModuleRef);
        //Add(SearchType.Resource, Resource, DsImages.Dialog, Resource_Key, VisibleMembersFlags.Resource | VisibleMembersFlags.ResourceElement);
        //Add(SearchType.GenericTypeDef, Generic, DsImages.Template, null, VisibleMembersFlags.GenericTypeDef);
        //Add(SearchType.NonGenericTypeDef, NonGeneric, DsImages.ClassPublic, null, VisibleMembersFlags.NonGenericTypeDef);
        Add(SearchType.EnumTypeDef, "Enum", DsImages.EnumerationPublic, null, VisibleMembersFlags.EnumTypeDef);
        Add(SearchType.InterfaceTypeDef, "Interface", DsImages.InterfacePublic, null, VisibleMembersFlags.InterfaceTypeDef);
        Add(SearchType.ClassTypeDef, "Class", DsImages.ClassPublic, null, VisibleMembersFlags.ClassTypeDef);
        Add(SearchType.StructTypeDef, "Struct", DsImages.StructurePublic, null, VisibleMembersFlags.StructTypeDef);
        //Add(SearchType.DelegateTypeDef, Delegate, DsImages.DelegatePublic, null, VisibleMembersFlags.DelegateTypeDef);
        //Add(SearchType.Member, Member, DsImages.Property, Member_Key, VisibleMembersFlags.MethodDef | VisibleMembersFlags.FieldDef | VisibleMembersFlags.PropertyDef | VisibleMembersFlags.EventDef);
        Add(SearchType.Any, "AllAbove", DsImages.ClassPublic, "AllAbove_Key", VisibleMembersFlags.TreeViewAll | VisibleMembersFlags.ParamDef | VisibleMembersFlags.Local);
        //Add(SearchType.Literal, Literal, DsImages.ConstantPublic, Literal_Key, VisibleMembersFlags.MethodBody | VisibleMembersFlags.FieldDef | VisibleMembersFlags.ParamDef | VisibleMembersFlags.PropertyDef | VisibleMembersFlags.Resource | VisibleMembersFlags.ResourceElement | VisibleMembersFlags.Attributes);

        _control.SearchListBoxDoubleClick += FollowSelectedReference;

        _control.SearchTextBox.TextChanged += TextChanged;
    }

    private void TextChanged(object sender, TextChangedEventArgs e)
    {
        var selectedItem = CreateSearchWindowCommand.SharedDocumentTabService.DocumentTreeView.TreeView.SelectedItem;
        if (selectedItem.GetTopNode() is not Cpp2ILDocumentNode documentNode)
            return;

        var list = _control.ListBox;

        var searchType = selectedSearchTypeVM.SearchType;
        if (searchType == SearchType.TypeDef)
        {
            if (SearchSettings.MatchWholeWords)
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.FullName == _control.SearchTextBox.Text);
            else 
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.FullName.Contains(_control.SearchTextBox.Text));
        }
        if (searchType == SearchType.ClassTypeDef)
        {
            if (SearchSettings.MatchWholeWords)
                list.ItemsSource = documentNode.AllTypes.Where(t => !t.Context.IsValueType && t.Context.FullName == _control.SearchTextBox.Text);
            else 
                list.ItemsSource = documentNode.AllTypes.Where(t => !t.Context.IsValueType && t.Context.FullName.Contains(_control.SearchTextBox.Text));
        }
        if (searchType == SearchType.StructTypeDef)
        {
            if (SearchSettings.MatchWholeWords)
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.IsValueType && t.Context.FullName == _control.SearchTextBox.Text);
            else 
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.IsValueType && t.Context.FullName.Contains(_control.SearchTextBox.Text));
        }
        if (searchType == SearchType.InterfaceTypeDef)
        {
            if (SearchSettings.MatchWholeWords)
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.IsInterface && t.Context.FullName == _control.SearchTextBox.Text);
            else 
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.IsInterface && t.Context.FullName.Contains(_control.SearchTextBox.Text));
        }
        if (searchType == SearchType.EnumTypeDef)
        {
            if (SearchSettings.MatchWholeWords)
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.IsEnumType  && t.Context.FullName == _control.SearchTextBox.Text);
            else 
                list.ItemsSource = documentNode.AllTypes.Where(t => t.Context.IsEnumType && t.Context.FullName.Contains(_control.SearchTextBox.Text));
        }
        else if (searchType == SearchType.FieldDef)
        {
            var source = documentNode.AllTypes.SelectMany(_ => _.GetTreeNodeData.OfType<FieldNode>());
            if (SearchSettings.MatchWholeWords)
                list.ItemsSource = source.Where(t => t.Context.Name == _control.SearchTextBox.Text);
            else 
                list.ItemsSource = source.Where(t => t.Context.Name.Contains(_control.SearchTextBox.Text));
        }
        else if (searchType == SearchType.MethodDef)
        {
            var source = documentNode.AllTypes.SelectMany(_ => _.GetTreeNodeData.OfType<MethodNode>());
            if (SearchSettings.MatchWholeWords)
                list.ItemsSource = source.Where(t => t.Context.Name == _control.SearchTextBox.Text);
            else 
                list.ItemsSource = source.Where(t => t.Context.Name.Contains(_control.SearchTextBox.Text));
        }
    }

    private void FollowSelectedReference(object? sender, EventArgs e)
    {
        bool newTab = Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift;
        var @ref = _control.searchListBox.SelectedItem;
        CreateSearchWindowCommand.SharedDocumentTabService.FollowReference(@ref, newTab, true);
    }
    
    public ObservableCollection<SearchTypeVM> SearchTypeVMs { get; }
    
    public SearchTypeVM SelectedSearchTypeVM {
        get => selectedSearchTypeVM;
        set {
            if (selectedSearchTypeVM != value) {
                selectedSearchTypeVM = value;
                OnPropertyChanged(nameof(SelectedSearchTypeVM));
            }
        }
    }
    SearchTypeVM selectedSearchTypeVM;
    
    void Add(SearchType searchType, string name, ImageReference icon, string? toolTip, VisibleMembersFlags flags) =>
        SearchTypeVMs.Add(new SearchTypeVM(searchType, name, toolTip, icon, flags));
}