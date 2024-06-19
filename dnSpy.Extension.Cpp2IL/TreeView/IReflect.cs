using Cpp2IL.Core.Model.Contexts;

namespace Cpp2ILAdapter.TreeView;

public interface IReflect
{
    TypeNode? SearchType(TypeAnalysisContext context);
}