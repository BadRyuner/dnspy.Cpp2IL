using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.TreeView;
using Echo.Code;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter.IsilEcho;

public sealed class IsilArchitecture : IArchitecture<InstructionSetIndependentInstruction>
{
    public IsilArchitecture(Cpp2ILDocument document)
    {
        Document = document;
    }

    public readonly Cpp2ILDocument Document;
    
    public long GetOffset(in InstructionSetIndependentInstruction instruction) => instruction.InstructionIndex;

    public int GetSize(in InstructionSetIndependentInstruction instruction) => 1;

    public InstructionFlowControl GetFlowControl(in InstructionSetIndependentInstruction instruction)
    {
        switch (instruction.FlowControl)
        {
            case IsilFlowControl.UnconditionalJump:
            case IsilFlowControl.ConditionalJump:
            case IsilFlowControl.IndexedJump:
                return InstructionFlowControl.CanBranch;
            case IsilFlowControl.MethodCall:
            case IsilFlowControl.Continue:
                return InstructionFlowControl.Fallthrough;
            case IsilFlowControl.MethodReturn:
            case IsilFlowControl.Interrupt:
                return InstructionFlowControl.IsTerminator;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public int GetStackPushCount(in InstructionSetIndependentInstruction instruction) => 0;

    public int GetStackPopCount(in InstructionSetIndependentInstruction instruction) => 0;

    public int GetReadVariablesCount(in InstructionSetIndependentInstruction instruction)
    {
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.SignExtend:
            case IsilMnemonic.LoadAddress:
                return 1;
            
            case IsilMnemonic.Call:
            case IsilMnemonic.CallNoReturn:
                return instruction.Operands.Length - 1;
            
            case IsilMnemonic.Move:
            case IsilMnemonic.And:
            case IsilMnemonic.Or:
            case IsilMnemonic.Xor:
            case IsilMnemonic.ShiftLeft:
            case IsilMnemonic.ShiftRight:
            case IsilMnemonic.Divide:
            case IsilMnemonic.Subtract:
            case IsilMnemonic.Exchange:
                return IsVariable(instruction.Operands[1]) ? 1 : 0;
            
            case IsilMnemonic.Multiply:
            case IsilMnemonic.Add:
                return (IsVariable(instruction.Operands[1]) ? 1 : 0) +
                       (IsVariable(instruction.Operands[2]) ? 1 : 0);
            
            case IsilMnemonic.Not:
                return IsVariable(instruction.Operands[0]) ? 1 : 0;
            
            case IsilMnemonic.Compare:
                return (IsVariable(instruction.Operands[0]) ? 1 : 0) +
                       (IsVariable(instruction.Operands[1]) ? 1 : 0);
            
            case IsilMnemonic.Push:
                return IsVariable(instruction.Operands[0]) ? 1 : 0;
            case IsilMnemonic.Pop:
                return IsVariable(instruction.Operands[1]) ? 1 : 0;
            
            case IsilMnemonic.Goto:
            case IsilMnemonic.Return:
            case IsilMnemonic.JumpIfEqual:
            case IsilMnemonic.JumpIfNotEqual:
            case IsilMnemonic.JumpIfGreater:
            case IsilMnemonic.JumpIfGreaterOrEqual:
            case IsilMnemonic.JumpIfLess:
            case IsilMnemonic.JumpIfLessOrEqual:
            case IsilMnemonic.Interrupt:
            case IsilMnemonic.ShiftStack:
            case IsilMnemonic.NotImplemented:
                return 0;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public int GetReadVariables(in InstructionSetIndependentInstruction instruction, Span<IVariable> variablesBuffer)
    {
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.SignExtend:
                variablesBuffer[0] = new IsilVariable(instruction.Operands[0].Data);
                return 1;
            
            case IsilMnemonic.LoadAddress:
                variablesBuffer[0] = new IsilVariable(instruction.Operands[1].Data);
                return 1;
            
            case IsilMnemonic.Call:
            case IsilMnemonic.CallNoReturn:
                for (var i = 1; i < instruction.Operands.Length; i++)
                {
                    variablesBuffer[i - 1] = new IsilVariable(instruction.Operands[i].Data);
                }
                return instruction.Operands.Length - 1;
            
            case IsilMnemonic.Pop:
            case IsilMnemonic.Move:
            case IsilMnemonic.And:
            case IsilMnemonic.Or:
            case IsilMnemonic.Xor:
            case IsilMnemonic.ShiftLeft:
            case IsilMnemonic.ShiftRight:
            case IsilMnemonic.Divide:
            case IsilMnemonic.Subtract:
            case IsilMnemonic.Exchange:
                if (IsVariable(instruction.Operands[1]))
                {
                    variablesBuffer[0] = new IsilVariable(instruction.Operands[1].Data);
                    return 1;
                }
                return 0;

            case IsilMnemonic.Multiply:
            case IsilMnemonic.Add:
            {
                int at = 0;
                if (IsVariable(instruction.Operands[1]))
                    variablesBuffer[at++] = new IsilVariable(instruction.Operands[1].Data);
                if (IsVariable(instruction.Operands[2]))
                    variablesBuffer[at] = new IsilVariable(instruction.Operands[2].Data);
                return (IsVariable(instruction.Operands[1]) ? 1 : 0) +
                       (IsVariable(instruction.Operands[2]) ? 1 : 0);
            }
            
            case IsilMnemonic.Push:
            case IsilMnemonic.Not:
                if (IsVariable(instruction.Operands[0]))
                {
                    variablesBuffer[0] = new IsilVariable(instruction.Operands[0].Data);
                    return 1;
                }
                return 0;

            case IsilMnemonic.Compare:
            {
                int at = 0;
                if (IsVariable(instruction.Operands[0]))
                    variablesBuffer[at++] = new IsilVariable(instruction.Operands[0].Data);
                if (IsVariable(instruction.Operands[1]))
                    variablesBuffer[at] = new IsilVariable(instruction.Operands[1].Data);
                return (IsVariable(instruction.Operands[0]) ? 1 : 0) +
                       (IsVariable(instruction.Operands[1]) ? 1 : 0);
            }
            
            case IsilMnemonic.Goto:
            case IsilMnemonic.Return:
            case IsilMnemonic.JumpIfEqual:
            case IsilMnemonic.JumpIfNotEqual:
            case IsilMnemonic.JumpIfGreater:
            case IsilMnemonic.JumpIfGreaterOrEqual:
            case IsilMnemonic.JumpIfLess:
            case IsilMnemonic.JumpIfLessOrEqual:
            case IsilMnemonic.Interrupt:
            case IsilMnemonic.ShiftStack:
            case IsilMnemonic.NotImplemented:
                return 0;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public int GetWrittenVariablesCount(in InstructionSetIndependentInstruction instruction)
    {
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.SignExtend:
            case IsilMnemonic.LoadAddress:
            case IsilMnemonic.Not:
            case IsilMnemonic.Move:
            case IsilMnemonic.And:
            case IsilMnemonic.Or:
            case IsilMnemonic.Xor:
            case IsilMnemonic.ShiftLeft:
            case IsilMnemonic.ShiftRight:
            case IsilMnemonic.Divide:
            case IsilMnemonic.Subtract:
            case IsilMnemonic.Exchange:
            case IsilMnemonic.Multiply:
            case IsilMnemonic.Add:
                return IsVariable(instruction.Operands[0]) ? 1 : 0;

            case IsilMnemonic.Call:
            {
                var methodPtr = (ulong)((IsilImmediateOperand)instruction.Operands[0].Data).Value;
                if (Document.Context.MethodsByAddress.TryGetValue(methodPtr, out var methods))
                {
                    var method = methods[0];
                    return method.Definition?.RawReturnType?.Type == Il2CppTypeEnum.IL2CPP_TYPE_VOID ? 0 : 1;
                }

                return 0;
            }
            
            case IsilMnemonic.CallNoReturn:
            case IsilMnemonic.Compare:
            case IsilMnemonic.Push:
            case IsilMnemonic.Pop:
            case IsilMnemonic.Goto:
            case IsilMnemonic.Return:
            case IsilMnemonic.JumpIfEqual:
            case IsilMnemonic.JumpIfNotEqual:
            case IsilMnemonic.JumpIfGreater:
            case IsilMnemonic.JumpIfGreaterOrEqual:
            case IsilMnemonic.JumpIfLess:
            case IsilMnemonic.JumpIfLessOrEqual:
            case IsilMnemonic.Interrupt:
            case IsilMnemonic.ShiftStack:
            case IsilMnemonic.NotImplemented:
                return 0;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public int GetWrittenVariables(in InstructionSetIndependentInstruction instruction, Span<IVariable> variablesBuffer)
    {
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.SignExtend:
            case IsilMnemonic.LoadAddress:
            case IsilMnemonic.Not:
            case IsilMnemonic.Move:
            case IsilMnemonic.And:
            case IsilMnemonic.Or:
            case IsilMnemonic.Xor:
            case IsilMnemonic.ShiftLeft:
            case IsilMnemonic.ShiftRight:
            case IsilMnemonic.Divide:
            case IsilMnemonic.Subtract:
            case IsilMnemonic.Exchange:
            case IsilMnemonic.Multiply:
            case IsilMnemonic.Add:
                if (IsVariable(instruction.Operands[0]))
                {
                    variablesBuffer[0] = new IsilVariable(instruction.Operands[0].Data);
                    return 1;
                }

                return 0;

            case IsilMnemonic.Call:
            {
                var methodPtr = (ulong)((IsilImmediateOperand)instruction.Operands[0].Data).Value;
                if (Document.Context.MethodsByAddress.TryGetValue(methodPtr, out var methods))
                {
                    var method = methods[0];
                    var returns = method.Definition?.RawReturnType?.Type != Il2CppTypeEnum.IL2CPP_TYPE_VOID;
                    if (returns)
                        variablesBuffer[0] = GetReturnRegister(method);
                    return returns ? 1 : 0;
                }

                return 0;
            }
            
            case IsilMnemonic.CallNoReturn:
            case IsilMnemonic.Compare:
            case IsilMnemonic.Push:
            case IsilMnemonic.Pop:
            case IsilMnemonic.Goto:
            case IsilMnemonic.Return:
            case IsilMnemonic.JumpIfEqual:
            case IsilMnemonic.JumpIfNotEqual:
            case IsilMnemonic.JumpIfGreater:
            case IsilMnemonic.JumpIfGreaterOrEqual:
            case IsilMnemonic.JumpIfLess:
            case IsilMnemonic.JumpIfLessOrEqual:
            case IsilMnemonic.Interrupt:
            case IsilMnemonic.ShiftStack:
            case IsilMnemonic.NotImplemented:
                return 0;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private bool IsVariable(in InstructionSetIndependentOperand operand)
    {
        switch (operand.Type)
        {
            case InstructionSetIndependentOperand.OperandType.StackOffset:
            case InstructionSetIndependentOperand.OperandType.Register:
            case InstructionSetIndependentOperand.OperandType.Memory:
                return true;
            default:
                return false;
        }
    }

    private IsilVariable GetReturnRegister(MethodAnalysisContext method)
    {
        if (Document.Context.InstructionSet is NewArmV8InstructionSet)
        {
            if (method.Definition!.RawReturnType!.Type is Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8)
                return V0;
            else
                return X0;
        }
        else // x64
        {
            if (method.Definition!.RawReturnType!.Type is Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8)
                return XMM0;
            else
                return RAX;
        }
    }

    private static readonly IsilVariable X0 = new(new IsilRegisterOperand("X0"));
    private static readonly IsilVariable V0 = new(new IsilRegisterOperand("V0"));
    private static readonly IsilVariable RAX = new(new IsilRegisterOperand("RAX"));
    private static readonly IsilVariable XMM0 = new(new IsilRegisterOperand("XMM0"));
}