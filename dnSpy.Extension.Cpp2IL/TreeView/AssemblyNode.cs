using System.Linq;
using Cpp2IL.Core.Model.Contexts;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.TreeView;

public class AssemblyNode : DsDocumentNode, IReflect
{
    public static readonly Guid MyGuid = new("9aef0611-8979-428c-ae5c-5daba1af5cbe");
    
    public AssemblyNode(AssemblyAnalysisContext context, IDsDocument document) : base(document)
    {
        Context = context;
        var grouped = Context.TopLevelTypes.GroupBy(_ => _.Namespace);
        Children = grouped.Select(ns => new NamespaceNode(ns.ToArray(), Document)).ToArray();
    }

    public new readonly AssemblyAnalysisContext Context;

    public override Guid Guid => MyGuid;
    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr) => DsImages.Assembly;

    protected override void WriteCore(ITextColorWriter output, IDecompiler decompiler, DocumentNodeWriteOptions options)
    {
        output.Write(Context.CleanAssemblyName);
    }

    public readonly NamespaceNode[] Children;
    
    public override IEnumerable<TreeNodeData> CreateChildren() => Children;

    public TypeNode? SearchType(TypeAnalysisContext context)
    {
        for (var i = 0; i < Children.Length; i++)
        {
            var child = Children[i];
            var result = child.SearchType(context);
            if (result != null)
                return result;
        }

        return null;
    }
}