using System.Runtime.CompilerServices;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils;
using LibCpp2IL;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter.PseudoC.Passes;

public sealed class StringAnalysis : BasePass
{
    private Il2CppBinary _binary = null!;

    public override void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        _binary = context.AppContext.Binary;
        base.Start(blocks, context);
    }

    public override void AcceptExpression(ref Expression expression)
    {
        if (expression is { Kind: ExpressionKind.Deref, Left: Immediate imm })
        {
            var ptr = (ulong)imm.Value.ToLong();
            if (ptr > 1000)
            {
                var str = MiscUtils.TryGetLiteralAt(_binary, ptr);
                if (str != null)
                {
                    expression = Unsafe.As<Expression>(new LoadString(str));
                    return;
                }
            }
        }
        if (expression is { Kind: ExpressionKind.Deref, First: LoadString strr })
            expression = Unsafe.As<Expression>(strr);
    }

    public override void AcceptBlock(Block block)
    {
    }
}