using Cpp2IL.Core.ISIL;
using Echo;
using Echo.Code;

namespace Cpp2ILAdapter.IsilEcho;

public sealed class IsilPurityClassifier : IPurityClassifier<InstructionSetIndependentInstruction>
{
    public static readonly IsilPurityClassifier Shared = new();
    
    public Trilean IsPure(in InstructionSetIndependentInstruction instruction)
    {
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.Call:
            case IsilMnemonic.CallNoReturn:
                return false;
            default:
                return true;
        }
    }
}