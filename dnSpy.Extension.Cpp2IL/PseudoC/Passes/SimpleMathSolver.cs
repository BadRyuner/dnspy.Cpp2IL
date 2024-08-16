using System.Runtime.CompilerServices;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL;

namespace Cpp2ILAdapter.PseudoC.Passes;

public class SimpleMathSolver : BasePass
{
    private Il2CppBinary _binary = null!;
    
    public override void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        _binary = context.AppContext.Binary;
        base.Start(blocks, context);
    }

    public override void AcceptExpression(ref Expression expression)
    {
        if (expression is { Kind: ExpressionKind.Add, Left: Immediate immLeft1, Right: Immediate immRight1 })
        {
            var hax = new Immediate(immLeft1.Value.ToLong() + immRight1.Value.ToLong());
            expression = Unsafe.As<Immediate, Expression>(ref hax);
        }
        else if (expression is { Kind: ExpressionKind.Deref, Left: Immediate ptr })
        {
            var hax = new Immediate(_binary.ReadPointerAtVirtualAddress((ulong)ptr.Value.ToLong()));
            expression = Unsafe.As<Immediate, Expression>(ref hax);
        }
    }

    public override void AcceptBlock(Block block)
    {
    }
}