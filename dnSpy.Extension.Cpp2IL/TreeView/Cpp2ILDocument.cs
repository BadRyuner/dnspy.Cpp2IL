using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using Cpp2IL.Core;
using Cpp2IL.Core.Model.Contexts;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.TreeView;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter.TreeView;

public sealed class Cpp2ILDocument : DsDocument
{
    public static readonly Guid MyGuid = new("85d059a7-7e0c-46be-84ae-622719535ff3");

    public readonly string FilePath;
    public Cpp2IlRuntimeArgs RuntimeArgs;
    public readonly ApplicationAnalysisContext Context;

    private readonly FilenameKey _key;

    public readonly Dictionary<ulong, Il2CppMethodDefinition> MethodByRva = new(1024 * 8);
    //public readonly Dictionary<ulong, FieldAnalysisContext> FieldByRva = new(1024 * 8);
    
    public Cpp2ILDocument(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        _key = new FilenameKey(filePath);
        FilePath = fileInfo.Name;
        RuntimeArgs = new();
        if (filePath.EndsWith(".dll"))
        {
            var gameFolder = fileInfo.Directory!.FullName;
            var dataFolder = fileInfo.Directory.GetDirectories()
                .First(d => d.Name.EndsWith("_Data"));
            var gameExe = dataFolder.Name.Replace("_Data", null);
            FilePath = gameExe;
            FileHelper.ResolvePathsFromCommandLine(gameFolder, gameExe, ref RuntimeArgs);
        }
        else
        {
            FileHelper.ResolvePathsFromCommandLine(filePath, null, ref RuntimeArgs);
        }
        Cpp2IlApi.InitializeLibCpp2Il(RuntimeArgs.PathToAssembly, RuntimeArgs.PathToMetadata, RuntimeArgs.UnityVersion);
        Context = Cpp2IlApi.CurrentAppContext!;
        TryFillData();
    }

    private void TryFillData()
    {
        var methodDefs = Context.Metadata.methodDefs;
        for (var index = 0; index < methodDefs.Length; index++)
        {
            var methodDef = methodDefs[index];
            try
            {
                MethodByRva.Add(methodDef.MethodPointer, methodDef);
            }
            catch
            {
                // ignored
            }
        }
    }

    public override DsDocumentInfo? SerializedDocument => new DsDocumentInfo(FilePath, MyGuid);
    public override IDsDocumentNameKey Key => _key;
}

[Export(typeof(IDsDocumentProvider))]
internal sealed class Cpp2ILDocumentProvider : IDsDocumentProvider
{
    public double Order => 0;

    public IDsDocument? Create(IDsDocumentService documentService, DsDocumentInfo documentInfo)
        => CanCreateFor(documentInfo) ? new Cpp2ILDocument(documentInfo.Name) : null;

    public IDsDocumentNameKey? CreateKey(IDsDocumentService documentService, DsDocumentInfo documentInfo)
        => CanCreateFor(documentInfo) ? new FilenameKey(documentInfo.Name) : null;

    private static bool CanCreateFor(DsDocumentInfo documentInfo)
    {
        // Handle existing wasm documents.
        if (documentInfo.Type == Cpp2ILDocument.MyGuid)
            return true;

        if (documentInfo.Type == DocumentConstants.DOCUMENTTYPE_FILE &&
            (documentInfo.Name.EndsWith("GameAssembly.dll", StringComparison.OrdinalIgnoreCase)
            || documentInfo.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)
            || documentInfo.Name.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase)))
            return true;
        
        return false;
    }
}

public sealed class Cpp2ILDocumentNode : DsDocumentNode, IDecompileSelf
{
    public static readonly Guid MyGuid = new("9aef0611-8979-428c-ae5c-5daba1af5cbe");
    
    public Cpp2ILDocumentNode(Cpp2ILDocument document) : base(document)
    {
        IlDocument = document;
    }

    public readonly Cpp2ILDocument IlDocument;
    
    public override Guid Guid => MyGuid;
    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr) => DsImages.Binary;

    protected override void WriteCore(ITextColorWriter output, IDecompiler decompiler, DocumentNodeWriteOptions options)
    {
        output.Write(IlDocument.FilePath);
    }

    public override IEnumerable<TreeNodeData> CreateChildren()
    {
        var context = IlDocument.Context;
        foreach (var assembly in context.Assemblies)
            yield return new AssemblyNode(assembly, Document);
    }

    public bool Decompile(IDecompileNodeContext context)
    {
        var writer = context.Output;
        var ctx = IlDocument.Context;
        object color = TextColor.Green;
        writer.WriteLine($"Metadata Version: {ctx.MetadataVersion}", color);
        writer.WriteLine($"Is 32 bit: {ctx.Metadata.is32Bit}", color);
        return true;
    }
}

[ExportDsDocumentNodeProvider]
internal class Cpp2ILDocumentNodeProvider : IDsDocumentNodeProvider
{
    public DsDocumentNode? Create(IDocumentTreeView documentTreeView, DsDocumentNode? owner, IDsDocument document)
    {
        return document is Cpp2ILDocument doc
            ? new Cpp2ILDocumentNode(doc)
            : null;
    }
}