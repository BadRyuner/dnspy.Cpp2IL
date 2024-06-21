using System.Globalization;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.References;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;
using LibCpp2IL;

namespace Cpp2ILAdapter.PseudoC;

public abstract record Value : IEmit
{
    public abstract void Write(IDecompilerOutput output, bool end = false);
}

public sealed record Register : Value
{
    public Register(string name)
    {
        if (name == "X31")
            name = "Stack";
        Name = name;
    }
    
    public string Name { get; set; }
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write(Name, BoxedTextColor.Local);
    }
}

public sealed record Variable(string Name) : Value
{
    public string Name { get; set; } = Name;
    /// <summary>
    /// IL2CppType or IL2CppDefinition
    /// </summary>
    public object? Type { get; set; }
    public bool IsKeyword = false;
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write(Name, new Cpp2IlVariableReference(this), DecompilerReferenceFlags.None, IsKeyword ? BoxedTextColor.Keyword : BoxedTextColor.Local);
    }
}

public sealed record Immediate(IConvertible Value) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write(Value.ToString(CultureInfo.InvariantCulture), BoxedTextColor.Number);
    }
}

public sealed record InstructionReference(uint Index) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write($"ISIL_{Index}", BoxedTextColor.Label);
    }
}

public sealed record LoadString(string Text) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write($"\"{Regex.Escape(Text)}\"", BoxedTextColor.String);
    }
}

public sealed record ManagedFunctionReference(MethodAnalysisContext Method) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        if (Method.IsStatic)
        {
            output.Write(Method.DeclaringType?.FullName ?? "unknownType", 
                new Cpp2ILTypeDefReference(Method.DeclaringType?.Definition), DecompilerReferenceFlags.None, BoxedTextColor.Type);
            output.Write(".", BoxedTextColor.Punctuation);
        }
        output.Write(Method.Name, new Cpp2ILMethodReference(Method), DecompilerReferenceFlags.None, BoxedTextColor.InstanceMethod);
    }
}

public sealed record UnmanagedFunctionReference(ulong Addr) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write("((", BoxedTextColor.Punctuation);
        output.Write("delegate", BoxedTextColor.Keyword);
        output.Write("* <...>", BoxedTextColor.Punctuation);
        output.Write(")", BoxedTextColor.Punctuation);
        output.Write($"{Addr:X2}", BoxedTextColor.Number);
        output.Write(")", BoxedTextColor.Punctuation);
    }
}

public sealed record AccessField(FieldAnalysisContext Field) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write(Field.Name, new Cpp2ILFieldReference(Field), DecompilerReferenceFlags.None, BoxedTextColor.InstanceField);
    }
}

public sealed record MetadataReference(MetadataUsage Metadata) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write($"/* TODO: MetadataReference: type - {Metadata.Type}; value - {Metadata.Type} */", BoxedTextColor.Comment);
    }
}