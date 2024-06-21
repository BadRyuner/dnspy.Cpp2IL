using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.PseudoC;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter.References;

public abstract record Cpp2ILReference;
public sealed record Cpp2ILTypeReference(Il2CppType? Type) : Cpp2ILReference;
public sealed record Cpp2ILTypeDefReference(Il2CppTypeDefinition? Type) : Cpp2ILReference;
public sealed record Cpp2ILMethodReference(MethodAnalysisContext Method) : Cpp2ILReference;
public sealed record Cpp2ILFieldReference(FieldAnalysisContext Field) : Cpp2ILReference;
public sealed record Cpp2IlVariableReference(Variable Variable) : Cpp2ILReference;