using dnSpy.Contracts.Extension;

namespace Cpp2ILAdapter;

public class Extension : IExtension
{
    public IEnumerable<string> MergedResourceDictionaries => Array.Empty<string>();

    public ExtensionInfo ExtensionInfo => new()
    {
        ShortDescription = "Cpp2IL Adapter"
    };

    public void OnEvent(ExtensionEvent @event, object? obj)
    {
        if (@event == ExtensionEvent.AppExit)
            FileHelper.CleanupExtractedFiles();
    }
}