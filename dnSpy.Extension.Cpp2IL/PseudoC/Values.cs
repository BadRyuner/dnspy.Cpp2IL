using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.References;
using Cpp2ILAdapter.TreeView;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;
using LibCpp2IL;
using LibCpp2IL.Metadata;
using LibCpp2IL.Reflection;

namespace Cpp2ILAdapter.PseudoC;

public abstract record Value : IEmit
{
    public uint Index { get; set; }
    public abstract void Write(IDecompilerOutput output);
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
    public override void Write(IDecompilerOutput output)
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
    public override void Write(IDecompilerOutput output)
    {
        output.Write(Name, new Cpp2IlVariableReference(this), DecompilerReferenceFlags.None, IsKeyword ? BoxedTextColor.Keyword : BoxedTextColor.Local);
    }
}

public sealed record Immediate(IConvertible Value) : Value
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write(Value.ToString(CultureInfo.InvariantCulture), BoxedTextColor.Number);
    }
}

public sealed record InstructionReference(uint InstructionIndex) : Value
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write($"ISIL_{InstructionIndex}", BoxedTextColor.Label);
    }
}

public sealed record LoadString(string Text) : Value
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write($"\"{Regex.Escape(Text)}\"", BoxedTextColor.String);
    }
}

public sealed record ManagedFunctionReference(MethodAnalysisContext Method) : Value
{
    public override void Write(IDecompilerOutput output)
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
    public override void Write(IDecompilerOutput output)
    {
        output.Write("((", BoxedTextColor.Punctuation);
        output.Write("delegate", BoxedTextColor.Keyword);
        output.Write("* <...>", BoxedTextColor.Punctuation);
        output.Write(")", BoxedTextColor.Punctuation);
        output.Write($"{Addr:X2}", BoxedTextColor.Number);
        output.Write(")", BoxedTextColor.Punctuation);
    }
}

public sealed record KnownFunctionReference(IL2CppKeyFunction Function, bool ReturnsValue, bool LikeJmp) : Value
{
    private readonly string _name = Function.ToString();
    
    public override void Write(IDecompilerOutput output)
    {
        output.Write(_name, new Cpp2ILKeyFunction(Function), DecompilerReferenceFlags.None, BoxedTextColor.ExtensionMethod);
    }

    public int ArgsCount => Function switch
    {
        IL2CppKeyFunction.IL2CppValueBox => 2,
        _ => 1
    };
    
    public static readonly KnownFunctionReference IL2CppCodegenInitializeMethod = new(IL2CppKeyFunction.IL2CppCodegenInitializeMethod, false, false);
    public static readonly KnownFunctionReference IL2CppRuntimeClassInit = new(IL2CppKeyFunction.IL2CppRuntimeClassInit, false, false);
    public static readonly KnownFunctionReference IL2CppObjectNew = new(IL2CppKeyFunction.IL2CppObjectNew, true, false);
    public static readonly KnownFunctionReference IL2CppArrayNewSpecific = new(IL2CppKeyFunction.IL2CppArrayNewSpecific, true, false);
    public static readonly KnownFunctionReference IL2CppTypeGetObject = new(IL2CppKeyFunction.IL2CppTypeGetObject, true, false);
    public static readonly KnownFunctionReference IL2CppResolveIcall = new(IL2CppKeyFunction.IL2CppResolveIcall, true, false);
    public static readonly KnownFunctionReference IL2CppStringNew = new(IL2CppKeyFunction.IL2CppStringNew, true, false);
    public static readonly KnownFunctionReference IL2CppValueBox = new(IL2CppKeyFunction.IL2CppValueBox, true, false);
    public static readonly KnownFunctionReference IL2CppObjectUnbox = new(IL2CppKeyFunction.IL2CppObjectUnbox, true, false);
    public static readonly KnownFunctionReference IL2CppRaiseException = new(IL2CppKeyFunction.IL2CppRaiseException, true, true);
    public static readonly KnownFunctionReference IL2CppVmObjectIsInst = new(IL2CppKeyFunction.IL2CppVmObjectIsInst, true, false);
    public static readonly KnownFunctionReference AddrPInvokeLookup = new(IL2CppKeyFunction.AddrPInvokeLookup, true, false);
}

public sealed record VariableFunctionReference(Value Reference) : Value
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("((", BoxedTextColor.Punctuation);
        output.Write("delegate", BoxedTextColor.Keyword);
        output.Write("* <...>", BoxedTextColor.Punctuation);
        output.Write(")", BoxedTextColor.Punctuation);
        Reference.Write(output);
        output.Write(")", BoxedTextColor.Punctuation);
    }
}

public sealed record AccessField(FieldAnalysisContext Field) : Value
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write(Field.Name, new Cpp2ILFieldReference(Field), DecompilerReferenceFlags.None, BoxedTextColor.InstanceField);
    }
}

public sealed record MetadataReference(MetadataUsage Metadata) : Value
{
    public override void Write(IDecompilerOutput output)
    {
        if (Metadata.Type is MetadataUsageType.Type or MetadataUsageType.TypeInfo)
        {
            output.Write("typeof", BoxedTextColor.Keyword);
            output.Write("(", BoxedTextColor.Punctuation);
            var ty = (Il2CppTypeReflectionData)Metadata.Value;
            if (ty.baseType != null)
                output.Write(ty.ToString(), new Cpp2ILTypeDefReference(ty.baseType), DecompilerReferenceFlags.None, BoxedTextColor.Type);
            else 
                output.Write(ty.ToString(), BoxedTextColor.Type);
            output.Write(")", BoxedTextColor.Punctuation);
        }
        else if (Metadata.Type == MetadataUsageType.MethodRef)
        {
            output.Write("__methodref__", BoxedTextColor.Keyword);
            output.Write("(", BoxedTextColor.Punctuation);
            var ty = (Cpp2IlMethodRef)Metadata.Value;
            output.Write(ty.ToString(), new Cpp2ILMethodReferenceFromRef(ty), DecompilerReferenceFlags.None, BoxedTextColor.InstanceMethod);
            output.Write(")", BoxedTextColor.Punctuation);
        }
        else if (Metadata.Type == MetadataUsageType.MethodDef)
        {
            output.Write("__methoddef__", BoxedTextColor.Keyword);
            output.Write("(", BoxedTextColor.Punctuation);
            var ty = (Il2CppMethodDefinition)Metadata.Value;
            var tyy = Cpp2ILDocumentNode.CurrentInstance.AllTypes
                .SelectMany(static t => t.GetTreeNodeData.OfType<MethodNode>())
                .FirstOrDefault(m => m.Context.Definition == ty)?.Context;
            output.Write(ty.DeclaringType?.Name ?? string.Empty, new Cpp2ILTypeDefReference(ty.DeclaringType), DecompilerReferenceFlags.None, BoxedTextColor.Type);
            output.Write(".", BoxedTextColor.Punctuation);
            output.Write(ty.Name ?? string.Empty, new Cpp2ILMethodReference(tyy), DecompilerReferenceFlags.None, BoxedTextColor.InstanceMethod);
            output.Write(")", BoxedTextColor.Punctuation);
        }
        else if (Metadata.Type == MetadataUsageType.StringLiteral)
        {
            output.Write("\"", BoxedTextColor.String);
            output.Write((string)Metadata.Value, BoxedTextColor.String);
            output.Write("\"", BoxedTextColor.String);   
        }
        else
            output.Write($"/* TODO: MetadataReference: type - {Metadata.Type}; value - {Metadata.Value.GetType()} */", BoxedTextColor.Comment);
    }
}