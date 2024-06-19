using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Documents.Tabs.DocViewer;

namespace Cpp2ILAdapter.References;

[ExportReferenceDocumentTabContentProvider(Order = 0)]
sealed class ReferenceProvider : IReferenceDocumentTabContentProvider
{
    public DocumentTabReferenceResult? Create(IDocumentTabService documentTabService, DocumentTabContent? sourceContent,
        object? @ref)
    {
        if (@ref is TextReference { Reference: Cpp2ILReference reference })
        {
            var node = documentTabService.DocumentTreeView.FindNode(reference);
            if (node == null)
                return null;

            var content = documentTabService.TryCreateContent(new[] { node });
            if (content == null)
                return null;

            return new DocumentTabReferenceResult(content);
        }

        return null;
    }
}