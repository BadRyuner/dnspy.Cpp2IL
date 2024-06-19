using System.Linq;
using Cpp2IL.Core.ISIL;
using Echo.Code;
using Echo.ControlFlow;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.DataFlow.Emulation;

namespace Cpp2ILAdapter.IsilEcho;

public sealed class IsilStateTransitioner : StateTransitionerBase<InstructionSetIndependentInstruction>
{
    public IsilStateTransitioner(IArchitecture<InstructionSetIndependentInstruction> architecture, List<InstructionSetIndependentInstruction> instructions) : base(architecture)
    {
        _instructions = instructions;
    }

    private readonly List<InstructionSetIndependentInstruction> _instructions;

    public override int GetTransitionCount(in SymbolicProgramState<InstructionSetIndependentInstruction> currentState, in InstructionSetIndependentInstruction instruction)
    {
        switch (instruction.FlowControl)
        {
            case IsilFlowControl.ConditionalJump:
                return 2;
            case IsilFlowControl.MethodCall:
                var next = instruction.InstructionIndex + 1;
                if (_instructions.Any(i => i.InstructionIndex == next))
                    goto case IsilFlowControl.Continue;
                return 0;
            case IsilFlowControl.UnconditionalJump:
            case IsilFlowControl.Continue:
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

    public override int GetTransitions(in SymbolicProgramState<InstructionSetIndependentInstruction> currentState, in InstructionSetIndependentInstruction instruction,
        Span<StateTransition<InstructionSetIndependentInstruction>> transitionBuffer)
    {
        switch (instruction.FlowControl)
        {
            case IsilFlowControl.ConditionalJump:
                var nextState = ApplyDefaultBehaviour(currentState, instruction);
                var conditional = nextState.WithProgramCounter(((InstructionSetIndependentInstruction)instruction.Operands[0].Data).InstructionIndex);
                transitionBuffer[0] = new StateTransition<InstructionSetIndependentInstruction>(conditional, ControlFlowEdgeType.Conditional);
                transitionBuffer[1] = new StateTransition<InstructionSetIndependentInstruction>(nextState, ControlFlowEdgeType.FallThrough);
                return 2;
            case IsilFlowControl.UnconditionalJump:
                nextState = ApplyDefaultBehaviour(currentState, instruction);
                var unconditional = nextState.WithProgramCounter(((InstructionSetIndependentInstruction)instruction.Operands[0].Data).InstructionIndex);
                transitionBuffer[0] = new StateTransition<InstructionSetIndependentInstruction>(unconditional, ControlFlowEdgeType.Unconditional);
                return 1;
            case IsilFlowControl.MethodCall:
                var next = instruction.InstructionIndex + 1;
                if (_instructions.Any(i => i.InstructionIndex == next))
                    goto case IsilFlowControl.Continue;
                return 0;
            case IsilFlowControl.Continue:
                nextState = ApplyDefaultBehaviour(currentState, instruction);
                transitionBuffer[0] = new StateTransition<InstructionSetIndependentInstruction>(nextState, ControlFlowEdgeType.FallThrough);
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