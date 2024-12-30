using System.Linq;
using System.Runtime.InteropServices;
using Cpp2IL.Core.Extensions;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter.PseudoC.Pass;

public sealed class CreateVariablesPass() : BasePass
{
    private static readonly string[] VariablesNames = Enumerable.Range(0, 999).Select(s => $"var_{s}").ToArray();
    
    private static readonly string[] IntParametersQueryForX64 = [ "rcx", "rdx", "r8", "r9" ]; // next stack
    private static readonly string[] FloatOrVecParametersQueryForX64 = [ "xmm0", "xmm1", "xmm2", "xmm3" ]; // next stack
    
    private static readonly string[] IntParametersQueryForArm = [ "X0", "X1", "X2", "X3", "X4", "X5", "X6", "X7" ]; // next stack
    private static readonly string[] FloatOrVecParametersQueryForArm = [ "D0", "D1", "D2", "D3", "D4", "D5", "D6", "D7" ]; // next ???

    private List<Block> _alreadyVisited = [];
    
    private readonly Dictionary<int, Dictionary<string, Stack<Variable>>> _regToVarMap = new(2);
    
    private int _variableCount = 0;
    
    private int _currentBlock = 0;

    private Dictionary<string, Stack<Variable>> CurrentMap
    {
        get
        {
            if (_regToVarMap.TryGetValue(_currentBlock, out var result))
                return result;
            result = new(8);
            _regToVarMap.Add(_currentBlock, result);
            return result;
        }
    }

    private Variable GetVariable(Register register, bool assign = false, bool setNewName = true)
    {
        if (!CurrentMap.TryGetValue(register.Name, out var variables))
        {
            variables = new(1);
            CurrentMap.Add(register.Name, variables);
        }

        if (!assign)
        {
            if (variables.Count == 0)
            {
                // inject parameters
                var pseudo = new Variable(setNewName ? VariablesNames[_variableCount++] : register.Name);
                variables.Push(pseudo);
                return pseudo;
            }
            return variables.Peek();
        }
        
        var var = new Variable(setNewName ? VariablesNames[_variableCount++] : register.Name);
        variables.Push(var);
        return var;
    }

    public CreateVariablesPass FillRegistersWithParameters(int startBlockId, MethodAnalysisContext context)
    {
        var isX64 = context.AppContext.InstructionSet is Cpp2IL.Core.InstructionSets.X86InstructionSet;
        var ints = isX64 ? IntParametersQueryForX64 : IntParametersQueryForArm;
        var floats = isX64 ? FloatOrVecParametersQueryForX64 : FloatOrVecParametersQueryForArm;
        var max = ints.Length;
        var current = 0;

        _currentBlock = startBlockId;
        
        if (!context.IsStatic)
        {
            var thisVar = GetVariable(new Register(ints[current]), true, false);
            thisVar.Type = context.DeclaringType!.Definition;
            thisVar.Name = "this";
            thisVar.IsKeyword = true;
            current++;
        }
        
        var parameters = context.Parameters;
        
        for (var i = 0; i < parameters.Count; i++)
        {
            if (current == max)
                break;
            
            var param = parameters[i];
            var name = param.Definition?.RawType?.Type is Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8 ? floats[i] : ints[i];
            var var = GetVariable(new Register(name), true, false);
            var.Name = param.Name;
            var.Type = param.Definition?.RawType;
            
            current++;
        }
        
        return this;
    }
    
    public override void AcceptBlocks(List<Block> blocks)
    {
        var entry = blocks[0];
        Branch(entry);
    }

    private void Branch(Block block)
    {
        if (_alreadyVisited.Contains(block)) return;
        _alreadyVisited.Add(block);
        
        _currentBlock = block.Id;
        AcceptBlock(block);
        var pre = _currentBlock;
        for (var i = 0; i < block.Successors.Count; i++)
        {
            var suc = block.Successors[i];
            _currentBlock = suc.Id;
            var cloned = _regToVarMap[pre].Clone();
            for (var x = 0; x < cloned.Keys.Count; x++)
            {
                string key = cloned.Keys.ElementAt(x);
                cloned[key] = cloned[key].Clone();
            }
            _regToVarMap.TryAdd(_currentBlock, cloned);
            Branch(suc);
        }
    }

    protected override void AcceptBlock(Block block)
    {
        _regToVarMap.TryAdd(_currentBlock, []);
        base.AcceptBlock(block);
    }

    protected override void VisitAssignExpression(AssignExpression expression)
    {
        if (expression.Value is Register reg1)
            expression.Value = GetVariable(reg1);
        if (expression.Target is Register reg2)
            expression.Target = GetVariable(reg2, true);
        
        base.VisitAssignExpression(expression);
    }

    protected override void VisitCallExpression(CallExpression expression)
    {
        if (expression.Method is Register reg1)
            expression.Method = GetVariable(reg1);
        for (var i = 0; i < expression.Arguments.Length; i++)
        {
            if (expression.Arguments[i] is Register reg2)
                expression.Arguments[i] = GetVariable(reg2);
        }
        
        base.VisitCallExpression(expression);
    }

    protected override void VisitCompareExpression(CompareExpression expression)
    {
        if (expression.Left is Register reg1)
            expression.Left = GetVariable(reg1);
        if (expression.Right is Register reg2)
            expression.Right = GetVariable(reg2);
        
        base.VisitCompareExpression(expression);
    }

    protected override void VisitDerefExpression(DerefExpression expression)
    {
        if (expression.Value is Register reg1)
            expression.Value = GetVariable(reg1);
        
        base.VisitDerefExpression(expression);
    }

    protected override void VisitIfExpression(IfExpression expression)
    {
        if (expression.Condition is Register reg1)
            expression.Condition = GetVariable(reg1);
        
        base.VisitIfExpression(expression);
    }

    protected override void VisitMathExpression(MathExpression expression)
    {
        if (expression.Left is Register reg1)
            expression.Left = GetVariable(reg1);
        if (expression.Right is Register reg2)
            expression.Right = GetVariable(reg2);
        
        base.VisitMathExpression(expression);
    }

    protected override void VisitReturnExpression(ReturnExpression expression)
    {
        if (expression.Value is Register reg1)
            expression.Value = GetVariable(reg1);
        
        base.VisitReturnExpression(expression);
    }

    protected override void VisitWhileExpression(WhileExpression expression)
    {
        if (expression.Condition is Register reg1)
            expression.Condition = GetVariable(reg1);
        
        base.VisitWhileExpression(expression);
    }

    protected override void VisitIfElseExpression(IfElseExpression expression)
    {
        if (expression.Condition is Register reg1)
            expression.Condition = GetVariable(reg1);
        
        base.VisitIfElseExpression(expression);
    }

    protected override void VisitVectorAccessExpression(VectorAccessExpression expression)
    {
        if (expression.Vector is Register reg1)
            expression.Vector = GetVariable(reg1);
        if (expression.Index is Register reg2)
            expression.Index = GetVariable(reg2);
        
        base.VisitVectorAccessExpression(expression);
    }
}