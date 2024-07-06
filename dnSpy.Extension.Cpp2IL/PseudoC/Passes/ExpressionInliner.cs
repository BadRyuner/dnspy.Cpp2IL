using Cpp2IL.Core.Model.Contexts;

namespace Cpp2ILAdapter.PseudoC.Passes;

public sealed class ExpressionInliner : BasePass
{
    private Dictionary<Variable, VariableInfo> _infos = new(1);
    
    public override void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        base.Start(blocks, context);

        foreach (var (variable, info) in _infos)
        {
            if (info.OnceInitOnceRead /* || (info.IsOnceInitialized && info.Write[0].Right is Immediate) */)
            {
                var data = info.Write[0].Right!;
                if (info.UsedInBlocks.Count == 1)
                {
                    var items = info.UsedInBlocks[0].Items;
                    for (var i = 0; i < items.Count; i++)
                    {
                        if (items[i] == variable)
                        {
                            items[i] = data;
                            break;
                        }
                    }
                }
                else
                {
                    var read = info.Read[0];
                    if (read.Left == variable)
                        read.Left = data;
                    else
                        read.Right = data;
                }
                info.Write[0].NopSelf();
            }
        }
    }

    public override void AcceptExpression(ref Expression expression)
    {
        if (expression is { Kind: ExpressionKind.Assign, Left: Variable writeVariable })
            GetInfo(writeVariable).Write.Add(expression);
        else if (expression is { Left: Variable readVariable })
            GetInfo(readVariable).Read.Add(expression);
        
        if (expression is { Right: Variable readVariable2 })
            GetInfo(readVariable2).Read.Add(expression);
    }

    public override void AcceptBlock(Block block)
    {
        if (block is InlineEmitBlock inlineEmitBlock)
        {
            for (var i = 0; i < inlineEmitBlock.Items.Count; i++)
            {
                if (inlineEmitBlock.Items[i] is Variable variable)
                    GetInfo(variable).UsedInBlocks.Add(inlineEmitBlock);
            }
        }
    }

    private VariableInfo GetInfo(Variable variable)
    {
        if (_infos.TryGetValue(variable, out var result))
            return result;
        result = new VariableInfo();
        _infos.Add(variable, result);
        return result;
    }
    
    private sealed class VariableInfo
    {
        public readonly List<Expression> Write = new(1);
        public readonly List<Expression> Read = new();
        public readonly List<InlineEmitBlock> UsedInBlocks = new();
        
        public bool IsOnceInitialized => Write.Count == 1;
        
        public bool OnceInitOnceRead => Write.Count == 1 &&
                                        ((Read.Count == 1 && UsedInBlocks.Count == 0) ||
                                         (Read.Count == 0 && UsedInBlocks.Count == 1));
    }
}