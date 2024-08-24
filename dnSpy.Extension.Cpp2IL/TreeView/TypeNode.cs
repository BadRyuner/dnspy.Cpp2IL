using System.Linq;
using System.Reflection;
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

    public string DisplayName => Context.FullName;
    
    public bool IsAbstract => (Context.TypeAttributes & TypeAttributes.Abstract) != 0;
    public bool IsSealed => (Context.TypeAttributes & TypeAttributes.Sealed) != 0;
    public bool IsInternal => IsAbstract && IsSealed;

    public bool IsPrivate => (Context.TypeAttributes & TypeAttributes.NestedPrivate) != 0 ;

    public bool IsPublic => (Context.TypeAttributes & TypeAttributes.Public) != 0
                            || (Context.TypeAttributes & TypeAttributes.NestedPublic) != 0 ;

    public bool IsStatic => (Context.TypeAttributes & TypeAttributes.Abstract) != 0 
                            && (Context.TypeAttributes & TypeAttributes.Sealed) != 0;
    
    private TreeNodeData[]? _treeNodeData;
    
    public override Guid Guid => MyGuid;

    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr)
    {
        if (Context.IsEnumType)
        {
            if (IsPublic)
                return DsImages.EnumerationPublic;

            if (IsPrivate)
                return DsImages.EnumerationPrivate;

            if (IsInternal)
                return DsImages.EnumerationInternal;

            return DsImages.EnumerationShortcut;
        }
        
        if (Context.IsValueType)
        {
            if (IsPublic)
                return DsImages.StructurePublic;

            if (IsPrivate)
                return DsImages.StructurePrivate;

            if (IsInternal)
                return DsImages.StructureInternal;

            return DsImages.StructureShortcut;
        }

        if (Context.IsInterface)
        {
            if (IsPublic)
                return DsImages.InterfacePublic;

            if (IsPrivate)
                return DsImages.InterfacePrivate;

            if (IsInternal)
                return DsImages.InterfaceInternal;

            return DsImages.InterfaceShortcut;
        }


        if (IsPublic)
            return DsImages.ClassPublic;

        if (IsPrivate)
            return DsImages.ClassPrivate;

        if (IsInternal)
            return DsImages.ClassInternal;

        return DsImages.ClassShortcut;
    }

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

        if (Context.CustomAttributes == null)
            Context.AnalyzeCustomAttributeData();
        IL2CppHelper.DispayAttributes(Context.CustomAttributes, write);
        
        if (Context.IsEnumType) // display as enum
        {
            write.Write("enum ", BoxedTextColor.Keyword);
            write.Write(Context.Name, BoxedTextColor.Type);
            write.Write(" : ", BoxedTextColor.Punctuation);
            write.WriteLine(Context.Fields[0]?.FieldType?.GetName() ?? string.Empty, BoxedTextColor.Type);
            write.WriteLine("{", BoxedTextColor.Punctuation);
            write.IncreaseIndent();
            for (var i = 1; i < Context.Fields.Count; i++)
            {
                var field = Context.Fields[i];
                write.Write(field.Name, BoxedTextColor.Local);
                if (field.BackingData?.DefaultValue != null)
                {
                    write.Write(" = ", BoxedTextColor.Punctuation);
                    write.Write(field.BackingData.DefaultValue.ToString() ?? string.Empty, BoxedTextColor.Number);
                }
                write.WriteLine(",", BoxedTextColor.Punctuation);
            }
            write.DecreaseIndent();
            write.WriteLine("}", BoxedTextColor.Punctuation);
            return true;
        }

        if (context.Decompiler.GenericNameUI == "IL")
        {
            write.Write(".type ", BoxedTextColor.Keyword);
            write.Write(Context.Name, this, DecompilerReferenceFlags.None, BoxedTextColor.Type);
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
            write.Write(Context.Name, this, DecompilerReferenceFlags.None, BoxedTextColor.Type);
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