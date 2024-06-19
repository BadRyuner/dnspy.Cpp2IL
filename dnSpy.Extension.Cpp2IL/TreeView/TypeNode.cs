using System.Linq;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.References;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.TreeView;

public class TypeNode : DsDocumentNode, IDecompileSelf, IReflect
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
            write.Write(".type ", BoxedTextColor.Keyword);
            write.Write(Context.Name, BoxedTextColor.Type);
            if (Context.BaseType != null)
            {
                write.Write(" : ", BoxedTextColor.Local);
                write.Write(Context.BaseType.Name, new Cpp2ILTypeReference(Context.Definition?.RawBaseType), DecompilerReferenceFlags.None, BoxedTextColor.Type);
            }
            write.WriteLine();
        }
        else
        {
            write.Write(Context.IsValueType ? "struct " : "class ", BoxedTextColor.Keyword);
            write.Write(Context.Name, BoxedTextColor.Type);
            if (Context.BaseType != null)
            {
                write.Write(" : ", BoxedTextColor.Local);
                write.Write(Context.BaseType.Name ?? string.Empty, new Cpp2ILTypeReference(Context.Definition?.RawBaseType), DecompilerReferenceFlags.None, BoxedTextColor.Type);
            }
            write.WriteLine();
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

    public TypeNode? SearchType(TypeAnalysisContext context)
    {
        if (context.Definition == Context.Definition)
            return this;

        var types = GetTreeNodeData
            .Where(_ => _ is TypeNode)
            .Cast<TypeNode>();
        
        foreach (var typeNode in types)
        {
            var result = typeNode.SearchType(context);
            if (result != null)
                return result;
        }
        
        return null;
    }
}