using System.Globalization;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.References;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter.PseudoC;

public abstract record Value : IEmit
{
    public abstract void Write(IDecompilerOutput output, bool end = false);
}

public sealed record Register : Value
{
    public Register(string name, Il2CppTypeEnum kind = Il2CppTypeEnum.IL2CPP_TYPE_I4)
    {
        if (name == "X31")
            name = "Stack";
        Name = name;
        Kind = kind;
    }
    
    public string Name { get; set; }
    public Il2CppTypeEnum Kind { get; set; }
    public Il2CppType? Type { get; set; }
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write(Name, BoxedTextColor.Local);
    }
}

public sealed record Variable(string Name, Il2CppTypeEnum Kind = Il2CppTypeEnum.IL2CPP_TYPE_I4) : Value
{
    public string Name { get; set; } = Name;
    public Il2CppTypeEnum Kind { get; set; } = Kind;
    public Il2CppType? Type { get; set; }
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write(Name, BoxedTextColor.Local);
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

public sealed record ManagedFunctionReference(MethodAnalysisContext method) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        if (method.IsStatic)
        {
            output.Write(method.DeclaringType?.FullName ?? "unknownType", 
                new Cpp2ILTypeDefReference(method.DeclaringType?.Definition), DecompilerReferenceFlags.None, BoxedTextColor.Type);
            output.Write(".", BoxedTextColor.Punctuation);
        }
        output.Write(method.Name, new Cpp2ILMethodReference(method), DecompilerReferenceFlags.None, BoxedTextColor.InstanceMethod);
    }
}

public sealed record UnmanagedFunctionReference(ulong addr) : Value
{
    public override void Write(IDecompilerOutput output, bool end = false)
    {
        output.Write("((", BoxedTextColor.Punctuation);
        output.Write("delegate", BoxedTextColor.Keyword);
        output.Write("* <...>", BoxedTextColor.Punctuation);
        output.Write(")", BoxedTextColor.Punctuation);
        output.Write($"{addr:X2}", BoxedTextColor.Number);
        output.Write(")", BoxedTextColor.Punctuation);
    }
}