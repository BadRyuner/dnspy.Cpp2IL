using System.Linq;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter.PseudoC.Passes;

public sealed class CreateVariables : BasePass
{
    //private int _phase;
    private readonly Dictionary<string, Variable> _registerToVariable = new(2);
    public readonly List<Variable> AllVariables = new(2);
    
    public override void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        //_phase = 0; // replace all registers with variables
        base.Start(blocks, context);
        RenamePhase(context); // rename all variables
    }

    public override void AcceptExpression(ref Expression expression)
    {
        if (expression is { Kind: ExpressionKind.Assign, Left: Register left, Right: Register right })
        {
            // share one var between two registers
            var copyReg = GetVariableForRegister(right);
            _registerToVariable[left.Name] = copyReg;
            expression = new Expression(ExpressionKind.Nop);
            return;
        }
        
        if (expression is { Left: Register leftRegister })
        {
            expression.Left = expression.Kind == ExpressionKind.Assign ? CreateVariable(leftRegister) : GetVariableForRegister(leftRegister);
        }
        
        if (expression is { Right: Register rightRegister })
            expression.Right = GetVariableForRegister(rightRegister);
    }

    public override void AcceptBlock(Block block)
    {
        if (block is InlineEmitBlock inlineEmitBlock)
        {
            for (var i = 0; i < inlineEmitBlock.Items.Count; i++)
            {
                if (inlineEmitBlock.Items[i] is Register register)
                    inlineEmitBlock.Items[i] = GetVariableForRegister(register);
            }
        }
    }

    private void RenamePhase(MethodAnalysisContext context)
    {
        var varDictionary = new Dictionary<string, uint>(AllVariables.Count);
        for (var i = 0; i < AllVariables.Count; i++)
        {
            var variable = AllVariables[i];
            if (variable.Name == "Stack") continue;
            if (!varDictionary.ContainsKey(variable.Name))
            {
                varDictionary[variable.Name] = 0;
                variable.Name = $"var_{variable.Name}";
            }
            else
            {
                varDictionary[variable.Name]++;
                variable.Name = $"var_{variable.Name}_{varDictionary[variable.Name]}";
            }
        }

        if (context.AppContext.InstructionSet is NewArmV8InstructionSet)
        {
            var vectorCount = 0;
            var nonVectorCount = 0;
            
            if (!context.IsStatic)
            {
                var thisVar = AllVariables.FirstOrDefault(static v => v.Name == "var_X0");
                if (thisVar != null)
                {
                    thisVar.Name = "this";
                    thisVar.Type = context.Definition?.DeclaringType;
                    thisVar.IsKeyword = true;
                }
                nonVectorCount++;
            }
            for (var i = 0; i < context.Parameters.Count; i++)
            {
                // arm64 call.conv: params X0-X7
                if (nonVectorCount > 7) 
                    break; // todo: handle stack param
                
                var param = context.Parameters[i];
                if (param.ParameterType.Type is Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8)
                {
                    var str = "var_V" + vectorCount;
                    vectorCount++;
                    var pVar = AllVariables.FirstOrDefault(v => v.Name == str);
                    if (pVar != null)
                    {
                        pVar.Name = param.ParameterName;
                        pVar.Type = param.ParameterType;
                    }
                }
                else
                {
                    if (param.ParameterType.Type is Il2CppTypeEnum.IL2CPP_TYPE_I1 or Il2CppTypeEnum.IL2CPP_TYPE_I2 or Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN)
                    {
                        var str1 = "var_W" + nonVectorCount;
                        var pVar = AllVariables.FirstOrDefault(v => v.Name == str1);
                        if (pVar != null)
                        {
                            pVar.Name = param.ParameterName;
                            pVar.Type = param.ParameterType;
                        }
                    }
                    else
                    {
                        var str = "var_X" + nonVectorCount;
                        var pVar = AllVariables.FirstOrDefault(v => v.Name == str);
                        if (pVar != null)
                        {
                            pVar.Name = param.ParameterName;
                            pVar.Type = param.ParameterType;
                        }
                    }
                    
                    nonVectorCount++;
                }
            }
        }
        else
        {
            if (!context.IsStatic)
            {
                var thisVar = AllVariables.FirstOrDefault(static v => v.Name == "var_rcx");
                if (thisVar != null)
                {
                    thisVar.Name = "this";
                    thisVar.Type = context.Definition?.DeclaringType;
                    thisVar.IsKeyword = true;
                }
            }
            
            for (var i = context.IsStatic ? 0 : 1; i < context.Parameters.Count; i++)
            {
                if (i > 3) 
                    break; // todo: handle stack param
                
                var param = context.Parameters[i];
                if (param.ParameterType.Type is Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8)
                {
                    var str = X64FloatArgs[i];
                    var pVar = AllVariables.FirstOrDefault(v => v.Name == str);
                    if (pVar != null)
                    {
                        pVar.Name = param.ParameterName;
                        pVar.Type = param.ParameterType;
                    }
                }
                else
                {
                    var str = X64IntArgs[i];
                    var pVar = AllVariables.FirstOrDefault(v => v.Name == str);
                    if (pVar != null)
                    {
                        pVar.Name = param.ParameterName;
                        pVar.Type = param.ParameterType;
                    }
                }
            }
        }
    }
    
    private static readonly string[] X64IntArgs = new[] { "var_rcx", "var_rdx", "var_r8", "var_r9" };
    private static readonly string[] X64FloatArgs = new[] { "var_xmm0", "var_xmm1", "var_xmm2", "var_xmm3" };
    
    private void ShadowVariable(Variable variable) => _registerToVariable[variable.Name] = variable;

    private Variable GetVariableForRegister(Register register)
    {
        if (_registerToVariable.TryGetValue(register.Name, out var result))
            return result;
        result = new Variable(register.Name);
        AllVariables.Add(result);
        ShadowVariable(result);
        return result;
    }

    private Variable CreateVariable(Register register)
    {
        var result = new Variable(register.Name);
        AllVariables.Add(result);
        ShadowVariable(result);
        return result;
    }
}