using System.Linq;
using Cpp2IL.Core.ISIL;
using Echo.Code;
using Echo.ControlFlow;
using Echo.ControlFlow.Construction;
using Echo.ControlFlow.Construction.Static;

namespace Cpp2ILAdapter.IsilEcho;

public sealed class IsilStaticSuccessorResolver : IStaticSuccessorResolver<InstructionSetIndependentInstruction>
{
    public IsilStaticSuccessorResolver(List<InstructionSetIndependentInstruction> instructions)
    {
        _instructions = instructions;
    }

    private readonly List<InstructionSetIndependentInstruction> _instructions;

    public int GetSuccessorsCount(in InstructionSetIndependentInstruction instruction)
    {
        switch (instruction.FlowControl)
        {
            case IsilFlowControl.ConditionalJump:
                return 2;
            case IsilFlowControl.UnconditionalJump:
                return 1;
            case IsilFlowControl.MethodCall:
            case IsilFlowControl.Continue:
                var next = instruction.InstructionIndex + 1;
                if (_instructions.Any(i => i.InstructionIndex == next) == false)
                    return 0;
                return 1;
            case IsilFlowControl.MethodReturn:
            case IsilFlowControl.Interrupt:
                return 0;
            case IsilFlowControl.IndexedJump:
                throw new NotImplementedException("IndexedJump");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public int GetSuccessors(in InstructionSetIndependentInstruction instruction, Span<SuccessorInfo> successorsBuffer)
    {
        switch (instruction.FlowControl)
        {
            case IsilFlowControl.ConditionalJump:
                var conditional = ((InstructionSetIndependentInstruction)instruction.Operands[0].Data).InstructionIndex;
                successorsBuffer[0] = new(conditional, ControlFlowEdgeType.Conditional);
                successorsBuffer[1] = new(instruction.InstructionIndex + 1, ControlFlowEdgeType.FallThrough);
                return 2;
            case IsilFlowControl.UnconditionalJump:
                var unconditional = ((InstructionSetIndependentInstruction)instruction.Operands[0].Data).InstructionIndex;
                successorsBuffer[0] = new(unconditional, ControlFlowEdgeType.Unconditional);
                return 1;
            case IsilFlowControl.MethodCall:
            case IsilFlowControl.Continue:
                var next = instruction.InstructionIndex + 1;
                if (_instructions.Any(i => i.InstructionIndex == next) == false)
                    return 0;
                successorsBuffer[0] = new(instruction.InstructionIndex + 1, ControlFlowEdgeType.FallThrough);
                return 1;
            case IsilFlowControl.MethodReturn:
            case IsilFlowControl.Interrupt:
                return 0;
            case IsilFlowControl.IndexedJump:
                throw new NotImplementedException("IndexedJump");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}