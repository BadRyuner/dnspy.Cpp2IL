﻿using System.Collections.Frozen;
using System.Linq;
using System.Reflection;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.References;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter.TreeView;

public class MethodNode : DsDocumentNode, IDecompileSelf
{
    public static readonly Guid MyGuid = new("c45cfdcc-f161-4b6c-876e-60688d64e594");
    
    public MethodNode(MethodAnalysisContext context, IDsDocument document) : base(document)
    {
        Context = context;
        Document = (Cpp2ILDocument)document;
        Lifted = new(() => IsilLifter.Lift(Context, Document));
    }

    public new readonly MethodAnalysisContext Context;
    public new readonly Cpp2ILDocument Document;
    public readonly Lazy<List<Block>> Lifted;
    
    public bool IsStatic => (Context.Attributes & MethodAttributes.Static) != 0;
    
    public string DisplayName => $"{Context.ReturnTypeContext.OriginalTypeName} {Context.DeclaringType?.FullName}::{Context.Name}({string.Join(',', Context.Parameters.Select(p => $"{p.ParameterType.GetName()} {p.ParameterName}"))})";
    
    public override Guid Guid => MyGuid;
    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr) 
        => Context.Attributes.HasFlag(MethodAttributes.Public) ? DsImages.MethodPublic : DsImages.MethodPrivate;
    
    protected override void WriteCore(ITextColorWriter output, IDecompiler decompiler, DocumentNodeWriteOptions options)
    {
        output.Write(IsStatic ? TextColor.StaticMethod 
            : TextColor.InstanceMethod, Context.Name);
        output.Write(TextColor.Punctuation, "(");
        var def = Context.Definition;
        if (def?.Parameters != null)
        {
            bool first = true;
            foreach (var parameter in def.Parameters)
            {
                if (!first)
                    output.Write(TextColor.Punctuation, ", ");
                output.Write(TextColor.Type, parameter.Type.ToString());
                output.Write(TextColor.Punctuation, " ");
                output.Write(TextColor.Local, parameter.ParameterName);
                first = false;
            }
        }
        output.Write(TextColor.Punctuation, ")");
    }

    public bool Decompile(IDecompileNodeContext context)
    {
        if (context.Decompiler.GenericNameUI == "IL")
            RenderIsil(context);
        else
            RenderPseudoSharp(context);
        
        return true;
    }

    private void RenderIsil(IDecompileNodeContext context)
    {
        var write = context.Output;
        
        if (Context.CustomAttributes == null)
            Context.AnalyzeCustomAttributeData();
        IL2CppHelper.DispayAttributes(Context.CustomAttributes, write);
        
        RenderHeader(context, Context.Definition!, true);
        write.IncreaseIndent();
        
        try
        {
            Context.Analyze();
            
            if (Context.ConvertedIsil == null || Context.ConvertedIsil.Count == 0)
            {
                write.WriteLine("No ISIL was generated", BoxedTextColor.Red);
            }
            else
            {
                foreach (var instruction in Context.ConvertedIsil)
                {
                    write.Write(instruction.InstructionIndex.ToString("000"), BoxedTextColor.AsmLabelAddress);
                    write.Write(" ", BoxedTextColor.Local);
                    write.Write(instruction.OpCode.Mnemonic.ToString(), BoxedTextColor.AsmMnemonic);
                    write.Write(" ", BoxedTextColor.Local);
                    var len = instruction.Operands.Length - 1;
                    for (var index = 0; index <= len ; index++)
                    {
                        var operand = instruction.Operands[index];
                        switch (operand.Type)
                        {
                            case InstructionSetIndependentOperand.OperandType.Immediate:
                                if (index == 0 && instruction.OpCode.Mnemonic == IsilMnemonic.Call)
                                {
                                    if (operand.Data is IsilMethodOperand { Method: { } resolvedMethod })
                                    {
                                        write.Write($"{resolvedMethod.DeclaringType?.FullName}::{resolvedMethod.Name}", new Cpp2ILMethodReference(resolvedMethod) , DecompilerReferenceFlags.None, BoxedTextColor.StaticMethod);
                                        break;
                                    }
                                    if (operand.Data is IsilImmediateOperand { Value: string funcName })
                                    {
                                        write.Write(funcName, BoxedTextColor.StaticMethod);
                                        break;
                                    }
                                    var methodPtr = (ulong)((IsilImmediateOperand)operand.Data).Value;
                                    if (Context.AppContext.MethodsByAddress.TryGetValue(methodPtr, out var methods))
                                    {
                                        var method = methods[0];
                                        write.Write($"{method.DeclaringType?.FullName}::{method.Name}", new Cpp2ILMethodReference(method) , DecompilerReferenceFlags.None, BoxedTextColor.StaticMethod);
                                        break;
                                    }
                                }
                                write.Write(operand.Data.ToString()!, BoxedTextColor.AsmNumber);
                                break;
                            case InstructionSetIndependentOperand.OperandType.Memory:
                            case InstructionSetIndependentOperand.OperandType.StackOffset:
                                write.Write(operand.Data.ToString()!, BoxedTextColor.AsmNumber);
                                break;
                            case InstructionSetIndependentOperand.OperandType.Register:
                                write.Write(operand.Data.ToString()!, BoxedTextColor.AsmRegister);
                                break;
                            default:
                                write.Write(operand.Data.ToString()!, BoxedTextColor.Local);
                                break;
                        }
                            
                        if (index != len)
                            write.Write(", ", BoxedTextColor.Local);
                    }
                    write.WriteLine();
                }
            }
        }
        catch(Exception e)
        {
            write.WriteLine($"Exception!\n{e}", BoxedTextColor.DarkGreen);
        }
        write.DecreaseIndent();
    }

    private void RenderPseudoSharp(IDecompileNodeContext context)
    {
        var write = context.Output;
        var def = Context.Definition!;
        RenderHeader(context, def, false);
        write.WriteLine("{", BoxedTextColor.Local);
        write.IncreaseIndent();
        
        try
        {
            Context.Analyze();
            
            if (Context.ConvertedIsil == null || Context.ConvertedIsil.Count == 0)
            {
                write.WriteLine("// No ISIL was generated", BoxedTextColor.DarkGreen);
            }
            else
            {
                var lifted = Lifted.Value;
                foreach (var emitBlock in lifted)
                {
                    emitBlock.Write(write);
                }
            }
        }
        catch(Exception e)
        {
            write.WriteLine($"{e}", BoxedTextColor.DarkGreen);
        }
        
        write.DecreaseIndent();
        write.WriteLine("}", BoxedTextColor.Local);
    }

    private void RenderHeader(IDecompileNodeContext context, Il2CppMethodDefinition def, bool isil)
    {
        var write = context.Output;
        if (!isil)
        {
            write.Write("[", BoxedTextColor.Local);
            write.Write("Rva", BoxedTextColor.DarkGreen);
            write.Write("(", BoxedTextColor.Local);
            write.Write($"0x{def.Rva:X2}", BoxedTextColor.Number);
            write.WriteLine(")]", BoxedTextColor.Local);
        }
        write.Write(def.Attributes.HasFlag(MethodAttributes.Public) ? "public " : "private ", BoxedTextColor.Keyword);
        write.Write(def.IsStatic ? "static " : string.Empty, BoxedTextColor.Keyword);
        if (def.RawReturnType?.Type != Il2CppTypeEnum.IL2CPP_TYPE_VOID)
            write.Write(def.ReturnType?.ToString() ?? string.Empty, new Cpp2ILTypeDefReference(Context.ReturnTypeContext.Definition), DecompilerReferenceFlags.None, BoxedTextColor.Type);
        else
            write.Write("void", BoxedTextColor.Keyword);
        write.Write(" ", BoxedTextColor.Local);
        write.Write(def.Name ?? string.Empty, this, DecompilerReferenceFlags.None, def.IsStatic ? BoxedTextColor.StaticMethod : BoxedTextColor.InstanceMethod);
        write.Write("(", BoxedTextColor.Local);
        if (Context.Parameters.Count != 0)
        {
            bool first = true;
            foreach (var parameter in Context.Parameters)
            {
                if (!first)
                    write.Write(", ", BoxedTextColor.Local);
                write.Write(parameter.ParameterTypeContext.Name, new Cpp2ILTypeDefReference(parameter.ParameterTypeContext.Definition), DecompilerReferenceFlags.None, BoxedTextColor.Type);
                write.Write(" ", BoxedTextColor.Local);
                write.Write(parameter.ParameterName, BoxedTextColor.Local);
                first = false;
            }
        }
        write.Write(") ", BoxedTextColor.Local);

        if (isil)
        {
            write.Write("at rva ", BoxedTextColor.White);
            write.Write($"0x{def.Rva:X2}", BoxedTextColor.Number);
        }
        
        write.WriteLine(string.Empty, BoxedTextColor.Local);
    }
}