using System.Linq;
using Cpp2IL.Core.Model.Contexts;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.TreeView;

public class TypeNode : DsDocumentNode, IDecompileSelf
{
    public static readonly Guid MyGuid = new("fe6cebe4-dbe5-47b4-a84e-b6a71757c413");
    
    public TypeNode(TypeAnalysisContext context, IDsDocument document) : base(document)
    {
        Context = context;
    }

    public new readonly TypeAnalysisContext Context;

    public TreeNodeData[] GetTreeNodeData
    {
        get
        {
            if (_treeNodeData == null)
                _treeNodeData = CreateTreeNodeData().ToArray();
            return _treeNodeData;
        }
    }
    
    private TreeNodeData[]? _treeNodeData;
    
    public override Guid Guid => MyGuid;
    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr) 
        => Context.IsValueType 
            ? DsImages.StructurePublic
            : Context.IsInterface 
            ? DsImages.InterfacePublic
            : DsImages.ClassPublic;

    protected override void WriteCore(ITextColorWriter output, IDecompiler decompiler, DocumentNodeWriteOptions options)
    {
        output.Write(Context.Name);
    }

    public override IEnumerable<TreeNodeData> CreateChildren() => GetTreeNodeData;
    private IEnumerable<TreeNodeData> CreateTreeNodeData()
    {
        if (Context.NestedTypes.Count > 0)
            foreach (var nestedType in Context.NestedTypes)
                yield return new TypeNode(nestedType, Document);
        
        if (Context.Fields.Count > 0)
            foreach (var field in Context.Fields)
                yield return new FieldNode(field, Document);
        
        if (Context.Methods.Count > 0)
            foreach (var method in Context.Methods)
                yield return new MethodNode(method, Document);
    }

    public bool Decompile(IDecompileNodeContext context)
    {
        var write = context.Output;

        if (context.Decompiler.GenericNameUI == "IL")
        {
            write.WriteLine($".type {Context.Name} : {Context.BaseType?.Name}", BoxedTextColor.Type);
        }
        else
        {
            write.Write(Context.IsValueType ? "struct " : "class ", BoxedTextColor.Keyword);
            write.Write(Context.Name, BoxedTextColor.Type);
            write.Write(" : ", BoxedTextColor.Local);
            write.WriteLine(Context.BaseType?.Name ?? string.Empty, BoxedTextColor.Type);
        }
        write.WriteLine("{", BoxedTextColor.Local);
        
        write.IncreaseIndent();
        foreach (var node in GetTreeNodeData)
        {
            if (node is IDecompileSelf decompileSelf)
                decompileSelf.Decompile(context);
            write.WriteLine();
        }
        write.DecreaseIndent();
        write.WriteLine("}", BoxedTextColor.Local);

        return true;
    }
}