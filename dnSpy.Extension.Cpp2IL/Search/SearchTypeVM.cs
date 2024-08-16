using dnSpy.Contracts.Images;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.Search;

namespace Cpp2ILAdapter.Search;

// ReSharper disable once InconsistentNaming
sealed class SearchTypeVM : ViewModelBase
{
    public ImageReference Image { get; }
    public string Name { get; }
    public string? ToolTip { get; }
    public SearchType SearchType { get; }
    public VisibleMembersFlags Flags { get; }

    public SearchTypeVM(SearchType searchType, string name, string? toolTip, ImageReference imageReference, VisibleMembersFlags flags) {
        SearchType = searchType;
        Name = name;
        ToolTip = toolTip;
        Image = imageReference;
        Flags = flags;
    }

    // ReSharper disable once InconsistentNaming
    public void RefreshUI() => OnPropertyChanged(nameof(Name));
}