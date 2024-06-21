using Cpp2IL.Core.Model.Contexts;

namespace Cpp2ILAdapter.PseudoC.Passes;

public sealed class EmitBlockLinker : BasePass
{
    private int _phase;
    private List<EmitBlock> _blocks = new(2);
    
    public override void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        _phase = 0;
        base.Start(blocks, context);
        _phase = 1;
        base.Start(blocks, context);
        _phase = 2;
        base.Start(blocks, context);
    }

    public override void AcceptExpression(ref Expression expression)
    {
        if (_phase == 0) return;
        if (expression.Kind != ExpressionKind.Goto) return;
        
        var reference = (InstructionReference)expression.First!;
        EmitBlock? block = null;
        
        for (var i = 0; i < _blocks.Count; i++)
        {
            block = _blocks[i];
            if (block.Index == reference.Index)
                break;
        }

        if (_phase == 1)
        {
            block!.ReferencesCount++;
        }
        else if (_phase == 2)
        {
            //if (block!.ReferencesCount <= 1)
            //    expression = Unsafe.As<EmitBlock, Expression>(ref block);
        }
    }

    public override void AcceptBlock(Block block)
    {
        if (_phase == 0 && block is EmitBlock emitBlock)
            _blocks.Add(emitBlock);
    }
}