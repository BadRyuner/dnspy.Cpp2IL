using System.Globalization;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.IsilEcho;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.PseudoC.Passes;
using Cpp2ILAdapter.TreeView;
using Echo.ControlFlow.Construction.Static;
using Echo.ControlFlow.Serialization.Blocks;
using LibCpp2IL;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter;

public static class IsilLifter
{
    public static List<EmitBlock> Lift(MethodAnalysisContext context, Cpp2ILDocument document)
    {
        var arch = new IsilArchitecture(document);
        var transitioner = new IsilStaticSuccessorResolver(context.ConvertedIsil!);
        var builder = new StaticFlowGraphBuilder<InstructionSetIndependentInstruction>(arch, context.ConvertedIsil, transitioner);
        var cfg = builder.ConstructFlowGraph(1, Array.Empty<long>());

        var blocks = cfg.ConstructBlocks().GetAllBlocks();
        var result = new List<EmitBlock>();
        IEmit? previous = new Expression(ExpressionKind.Nop);
        foreach (var basicBlock in blocks)
        {
            var emitBlock = new EmitBlock(basicBlock.Header.InstructionIndex);
            for (var i = 0; i < basicBlock.Instructions.Count; i++)
            {
                var instruction = basicBlock.Instructions[i];
                var transformed = Transform(ref previous, instruction, context);
                if (previous != null)
                    emitBlock.Add(transformed);
                else
                    emitBlock.Items[^1] = transformed;
                previous = transformed;
            }
            result.Add(emitBlock);
        }
        
        new EmitBlockLinker().Start(result, context);
        new CreateVariables().Start(result, context);
        new DataFlowAnalysis().Start(result, context);
        
        return result;
    }

    private static IEmit Transform(ref IEmit? previous, in InstructionSetIndependentInstruction instruction, MethodAnalysisContext context)
    {
        var operands = instruction.Operands;
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.Move:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), TransformOperand(operands[1]));
            case IsilMnemonic.And:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.And, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Or:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Or, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.Xor:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Xor, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.Compare:
                return new Expression(ExpressionKind.Compare, TransformOperand(operands[0]), TransformOperand(operands[1]));
            case IsilMnemonic.Add:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Add, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Subtract:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Sub, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.Multiply:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Mul, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Divide:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Div, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.ShiftLeft:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Shl, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.ShiftRight:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Shr, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.Return:
                if (context.Definition?.RawReturnType?.Type == Il2CppTypeEnum.IL2CPP_TYPE_VOID)
                    return new Expression(ExpressionKind.Return);
                return new Expression(ExpressionKind.Return, TransformOperand(operands[0]));
            case IsilMnemonic.Goto:
                return new Expression(ExpressionKind.Goto, new InstructionReference(((InstructionSetIndependentInstruction)operands[0].Data).InstructionIndex));

            case IsilMnemonic.JumpIfEqual:
            case IsilMnemonic.JumpIfNotEqual:
            case IsilMnemonic.JumpIfGreater:
            case IsilMnemonic.JumpIfGreaterOrEqual:
            case IsilMnemonic.JumpIfLessOrEqual:
            case IsilMnemonic.JumpIfLess:
            {
                var result = new Expression(ExpressionKind.If, previous, new Expression(ExpressionKind.Goto, TransformOperand(operands[0])))
                    .FixIf(instruction.OpCode.Mnemonic);
                previous = null;
                return result;
            }
            
            case IsilMnemonic.Call:
            {
                if (instruction.Operands[0].Data is IsilImmediateOperand imm)
                {
                    var args = new InlineEmitBlock(", ");
                    for (var i = 1; i < instruction.Operands.Length; i++)
                    {
                        var op = instruction.Operands[i];
                        args.Add(TransformOperand(op));
                    }

                    bool returns;
                    IEmit function;
                    if (context.AppContext.MethodsByAddress.TryGetValue((ulong)imm.Value, out var methods))
                    {
                        var method = methods[0];
                        function = new ManagedFunctionReference(method);
                        returns = method.Definition?.RawReturnType?.Type is not Il2CppTypeEnum.IL2CPP_TYPE_VOID;
                    }
                    else
                    {
                        function = new UnmanagedFunctionReference(imm.Value.ToUInt64(CultureInfo.InvariantCulture));
                        returns = false; // cursed
                    }
                    
                    var call = new Expression(ExpressionKind.Call, function, args);
                    if (returns)
                        call = new Expression(ExpressionKind.Assign, GetReturnRegister(context), call);
                    return call;
                }

                throw new NotImplementedException("instruction.Operands[0].Data is NOT IsilImmediateOperand");
            }
            
            case IsilMnemonic.LoadAddress:
            case IsilMnemonic.CallNoReturn:
            case IsilMnemonic.Exchange:
            case IsilMnemonic.Not:
            case IsilMnemonic.ShiftStack:
            case IsilMnemonic.Push:
            case IsilMnemonic.Pop:
            case IsilMnemonic.SignExtend:
            case IsilMnemonic.Interrupt:
            case IsilMnemonic.NotImplemented:
            default:
                return new Unsupported(instruction.ToString());
        }
    }

    private static IEmit TransformOperand(in InstructionSetIndependentOperand operand)
    {
        switch (operand.Type)
        {
            case InstructionSetIndependentOperand.OperandType.Immediate:
            {
                var value = ((IsilImmediateOperand)operand.Data).Value;
                var ptr = value.ToUInt64(CultureInfo.InvariantCulture);
                var reference = LibCpp2IlMain.GetAnyGlobalByAddress(ptr);
                if (reference is { IsValid: true })
                    return new MetadataReference(reference);
                return new Immediate(value);
            }
            case InstructionSetIndependentOperand.OperandType.Register:
                return new Register(((IsilRegisterOperand)operand.Data).RegisterName);
            case InstructionSetIndependentOperand.OperandType.Instruction:
                return new InstructionReference(((InstructionSetIndependentInstruction)operand.Data).InstructionIndex);
            case InstructionSetIndependentOperand.OperandType.Memory:
            {
                var isilMemoryOperand = (IsilMemoryOperand)operand.Data;
                IEmit? what = null;
                if (isilMemoryOperand.Base != null)
                    what = TransformOperand(isilMemoryOperand.Base.Value);
                if (isilMemoryOperand.Addend != 0)
                {
                    var c = new Immediate(isilMemoryOperand.Addend);
                    if (what != null)
                        what = new Expression(ExpressionKind.Add, what, c);
                    else
                        what = c;
                }
                if (isilMemoryOperand.Index != null)
                {
                    var index = TransformOperand(isilMemoryOperand.Index.Value);
                    if (what == null)
                        what = index;
                    else
                        what = new Expression(ExpressionKind.Add,what, index);

                    if (isilMemoryOperand.Scale > 1)
                        what = new Expression(ExpressionKind.Mul, what, new Immediate(isilMemoryOperand.Scale));
                }
                return new Expression(ExpressionKind.Deref, what);
            }
            case InstructionSetIndependentOperand.OperandType.StackOffset:
            default:
                return new Unsupported(operand.ToString() ?? string.Empty);
        }
    }

    private static Register GetReturnRegister(MethodAnalysisContext context)
    {
        var isFloat =
            context.Definition?.RawReturnType?.Type is Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8;

        var set = context.AppContext.InstructionSet;

        if (set is NewArmV8InstructionSet)
            return new Register(isFloat ? "V0" : "X0");
        return new Register(isFloat ? "XMM0" : "RAX");
    }
}