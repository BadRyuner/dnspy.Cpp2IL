using System.Windows.Controls;
using System.Windows.Input;
using dnSpy.Contracts.Utilities;

namespace Cpp2ILAdapter.Search;

public partial class SearchControl : UserControl
{
    public TextBox SearchTextBox => searchTextBox;
    public ListBox ListBox => searchListBox;
    
    public SearchControl()
    {
        InitializeComponent();
    }
    
    void searchListBox_MouseDoubleClick(object? sender, MouseButtonEventArgs e) {
        if (!UIUtilities.IsLeftDoubleClick<ListBoxItem>(searchListBox, e))
            return;
        e.Handled = true;
        SearchListBoxDoubleClick?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? SearchListBoxDoubleClick;
}