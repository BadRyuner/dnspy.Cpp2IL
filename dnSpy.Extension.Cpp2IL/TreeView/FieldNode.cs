using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.References;
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

    public new readonly FieldAnalysisContext Context;
    
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
        
        if (Context.CustomAttributes == null)
            Context.AnalyzeCustomAttributeData();
        IL2CppHelper.DispayAttributes(Context.CustomAttributes, write);
        
        if (context.Decompiler.GenericNameUI == "IL")
        {
            write.Write("field. offset(", BoxedTextColor.Blue);
            write.Write($"0x{Context.Offset:X2}", BoxedTextColor.AsmNumber);
            write.Write(") ", BoxedTextColor.Blue);
            write.Write(Context.IsStatic ? "static " : "instance ", BoxedTextColor.Blue);
        }
        else
        {
            write.Write("[", BoxedTextColor.Local);
            write.Write("Offset", BoxedTextColor.Green);
            write.Write("(", BoxedTextColor.Local);
            write.Write($"0x{Context.Offset:X2}", BoxedTextColor.Number);
            write.WriteLine(")]", BoxedTextColor.Local);
            if (Context.Attributes.HasFlag(FieldAttributes.Public))
                write.Write("public ", BoxedTextColor.Keyword);
            if (Context.IsStatic)
                write.Write("static ", BoxedTextColor.Keyword);
        }
        write.Write(Context.FieldType!.GetName(), new Cpp2ILTypeReference(Context.FieldType!), DecompilerReferenceFlags.None, BoxedTextColor.Type);
        write.Write(" ", BoxedTextColor.Local);
        write.WriteLine(Context.FieldName, BoxedTextColor.Local);
        return true;
    }
}