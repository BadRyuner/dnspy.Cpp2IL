using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter.References;

public abstract record Cpp2ILReference;
public sealed record Cpp2ILTypeReference(Il2CppType? Type) : Cpp2ILReference;
public sealed record Cpp2ILMethodReference(MethodAnalysisContext Method) : Cpp2ILReference;
public sealed record Cpp2ILFieldReference() : Cpp2ILReference;