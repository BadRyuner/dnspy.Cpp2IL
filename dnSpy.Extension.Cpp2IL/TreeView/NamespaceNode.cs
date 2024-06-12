using Cpp2IL.Core.Model.Contexts;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.TreeView;

public class NamespaceNode : DsDocumentNode
{
    public static readonly Guid MyGuid = new("9aef0611-8979-428c-ae5c-5daba1af5cbe");
    
    public NamespaceNode(TypeAnalysisContext[] types, IDsDocument document) : base(document)
    {
        Types = types;
    }

    public readonly TypeAnalysisContext[] Types;

    public override Guid Guid => MyGuid;
    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr) => DsImages.Namespace;

    protected override void WriteCore(ITextColorWriter output, IDecompiler decompiler, DocumentNodeWriteOptions options)
    {
        output.Write(Types[0].Namespace);
    }

    public override IEnumerable<TreeNodeData> CreateChildren()
    {
        foreach (var type in Types)
            yield return new TypeNode(type, Document);
    }
}