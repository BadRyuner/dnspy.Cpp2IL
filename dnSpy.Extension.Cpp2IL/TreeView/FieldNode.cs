using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter.TreeView;

public class FieldNode : DsDocumentNode, IDecompileSelf
{
    public static readonly Guid MyGuid = new("d279bd05-ff2a-4eee-90f8-5f727c9fecc9");
    
    public FieldNode(FieldAnalysisContext context, IDsDocument document) : base(document)
    {
        Context = context;
    }

    public readonly FieldAnalysisContext Context;
    
    public override Guid Guid => MyGuid;
    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr) 
        => Context.FieldAttributes.HasFlag(FieldAttributes.Public) ? DsImages.FieldPublic : DsImages.FieldPrivate;

    protected override void WriteCore(ITextColorWriter output, IDecompiler decompiler, DocumentNodeWriteOptions options)
    {
        output.Write(Context.Name);
    }


    public bool Decompile(IDecompileNodeContext context)
    {
        var write = context.Output;
        write.Write("field. ", BoxedTextColor.Blue);
        write.Write(Context.FieldType!.Type switch
        {
            Il2CppTypeEnum.IL2CPP_TYPE_I => "nint",
            Il2CppTypeEnum.IL2CPP_TYPE_CLASS or Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE => Context.FieldType.AsClass()!.Name!,
            Il2CppTypeEnum.IL2CPP_TYPE_I1 => "sbyte",
            Il2CppTypeEnum.IL2CPP_TYPE_I2 => "short",
            Il2CppTypeEnum.IL2CPP_TYPE_I4 => "int",
            Il2CppTypeEnum.IL2CPP_TYPE_I8 => "long",
            Il2CppTypeEnum.IL2CPP_TYPE_U => "nuint",
            Il2CppTypeEnum.IL2CPP_TYPE_U1 => "byte",
            Il2CppTypeEnum.IL2CPP_TYPE_U2 => "ushort",
            Il2CppTypeEnum.IL2CPP_TYPE_U4 => "uint",
            Il2CppTypeEnum.IL2CPP_TYPE_U8 => "ulong",
            Il2CppTypeEnum.IL2CPP_TYPE_R4 => "float",
            Il2CppTypeEnum.IL2CPP_TYPE_R8 => "double",
            _ => "UnknownType"
        }, BoxedTextColor.Blue);
        write.Write(" ", BoxedTextColor.White);
        write.WriteLine(Context.FieldName, BoxedTextColor.White);
        return true;
    }
}