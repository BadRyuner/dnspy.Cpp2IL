using System.Reflection;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text.Classification;
using dnSpy.Contracts.TreeView.Text;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public abstract class ReferencedAnalyzerTreeNode(DsDocumentNode node) : AnalyzerTreeNodeData
{
    private static readonly MethodInfo WriteCore;
    private static readonly object BoxedNone = DocumentNodeWriteOptions.None;

    static ReferencedAnalyzerTreeNode()
    {
        WriteCore = typeof(DsDocumentNode).GetMethod("WriteCore", BindingFlags.Instance | BindingFlags.NonPublic)!;
    }
    
    static class Cache {
        static readonly TextClassifierTextColorWriter writer = new();
        public static TextClassifierTextColorWriter GetWriter() => writer;
        public static void FreeWriter(TextClassifierTextColorWriter writer) => writer.Clear();
    }

   
    public sealed override object? Text {
        get {
            if (cachedText?.Target is { } cached)
                return cached;

            var writer = Cache.GetWriter();
            try {
                WriteCore.Invoke(node, new[] { writer, null, BoxedNone });
                var classifierContext = new TreeViewNodeClassifierContext(writer.Text, AnalyzerService.Instance!.DocumentTabService.DocumentTreeView.TreeView, this, isToolTip: false, colorize: true, colors: writer.Colors);
                var elem = AnalyzerService.Instance!.TreeViewNodeTextElementProvider.CreateTextElement(classifierContext, TreeViewContentTypes.TreeViewNodeAssemblyExplorer, TextElementFlags.FilterOutNewLines);
                cachedText = new WeakReference(elem);
                return elem;
            }
            finally {
                Cache.FreeWriter(writer);
            }
        }
    }
    WeakReference? cachedText;
    
    public override object? ToolTip => node.ToolTip;
    
    public override ImageReference Icon => node.Icon;
    
    public override bool Activate()
    {
        AnalyzerService.Instance!.DocumentTabService.FollowReference(node);
        return true;
    }
}