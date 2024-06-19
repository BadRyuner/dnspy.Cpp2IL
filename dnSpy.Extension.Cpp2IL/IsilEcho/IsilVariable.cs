using Cpp2IL.Core.ISIL;
using Echo.Code;

namespace Cpp2ILAdapter.IsilEcho;

public sealed class IsilVariable : IVariable
{
    public IsilVariable(IsilOperandData variableData)
    {
        VariableData = variableData;
        Name = VariableData.ToString()!;
    }
    
    public readonly IsilOperandData VariableData;

    public string Name { get; }
}