using Cpp2IL.Core.Model.Contexts;

namespace Cpp2ILAdapter.PseudoC.Passes;

public abstract class BasePass
{
    public virtual void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            blocks[i].AcceptPass(this);
        }
    }
    
    public abstract void AcceptExpression(ref Expression expression);
    
    public abstract void AcceptBlock(Block block);
}