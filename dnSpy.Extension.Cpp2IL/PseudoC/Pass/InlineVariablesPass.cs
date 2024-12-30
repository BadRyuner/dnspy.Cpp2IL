using System.Linq;
using System.Runtime.CompilerServices;
using StableNameDotNet;

namespace Cpp2ILAdapter.PseudoC.Pass;

public class InlineVariablesPass : BasePass
{
    private unsafe class VariableInfo
    {
        public List<(Expression, IntPtr)> Used = [];
        public List<AssignExpression> Assigned = [];
    }

    private Dictionary<Variable, VariableInfo> _variableInfos = new(8);
    
    public override unsafe void AcceptBlocks(List<Block> blocks)
    {
        base.AcceptBlocks(blocks);

        for (var i = 0; i < _variableInfos.Count; i++)
        {
            var info = _variableInfos[_variableInfos.Keys.ElementAt(i)];
            var inlinable = info.Assigned.Count == 1;
            if (inlinable && info.Assigned[0].Value is Variable otherVariable)
            {
                info.Assigned[0].Eliminated = true;
                foreach (var (expression, ptr) in info.Used)
                {
                    ref var variable = ref Unsafe.AsRef<Variable>((void*)ptr);
                    variable = otherVariable;
                }
            }
            else if (inlinable && info.Assigned[0].Value is Immediate immediate)
            {
                info.Assigned[0].Eliminated = true;
                foreach (var (expression, ptr) in info.Used)
                {
                    ref var variable = ref Unsafe.AsRef<Value>((void*)ptr);
                    variable = immediate;
                }
            }
        }
    }

    protected override unsafe void AcceptSingleIEmit(IEmit emit)
    {
        base.AcceptSingleIEmit(emit);

        if (emit is Expression expr and not AssignExpression)
        {
            var count = expr.ChildrenCount;
            for (int i = 0; i < count; i++)
            {
                ref var child = ref expr.GetChildren(i);
                if (child is Variable var)
                {
                    var info = _variableInfos.GetOrCreate(var, () => new());
                    info.Used.Add((expr, (IntPtr)Unsafe.AsPointer(ref child)));
                }
            }
        }
    }

    protected override unsafe void VisitAssignExpression(AssignExpression expression)
    {
        if (expression.Target is Variable var1)
            _variableInfos.GetOrCreate(var1, () => new()).Assigned.Add(expression);
        if (expression.Value is Variable var2)
            _variableInfos.GetOrCreate(var2, () => new()).Used.Add((expression, (IntPtr)Unsafe.AsPointer(ref expression.Value)));
        
        base.VisitAssignExpression(expression);
    }
}