using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL;

namespace Cpp2ILAdapter.PseudoC.Passes;

public class MetadataInliner : BasePass
{
    private Il2CppBinary _binary;
    
    public override void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        _binary = context.AppContext.Binary;
        base.Start(blocks, context);
    }
    
    public override void AcceptExpression(ref Expression expression)
    {
        if (expression is { Left: Immediate imm1 })
        {
            try
            {
                var ptr = (ulong)imm1.Value.ToLong();
                var global = LibCpp2IlMain.GetAnyGlobalByAddress(ptr);

                if (global is { IsValid: true })
                {
                    expression.Left = new MetadataReference(global);
                    goto exit;
                }
                
                global = LibCpp2IlMain.GetAnyGlobalByAddress(_binary.ReadPointerAtVirtualAddress(ptr));
                if (global is { IsValid: true })
                    expression.Left = new MetadataReference(global);
            }
            catch
            {
                // ignore
            }
        }
        exit:
        if (expression is { Right: Immediate imm2 })
        {
            try
            {
                var ptr = (ulong)imm2.Value.ToLong();
                var global = LibCpp2IlMain.GetAnyGlobalByAddress(ptr);
            
                if (global is { IsValid: true })
                {
                    expression.Right = new MetadataReference(global);
                    return;
                }
                
                global = LibCpp2IlMain.GetAnyGlobalByAddress(_binary.ReadPointerAtVirtualAddress(ptr));
                if (global is { IsValid: true })
                    expression.Right = new MetadataReference(global);
            }
            catch
            {
                // ignore
            }
        }
    }

    public override void AcceptBlock(Block block)
    {
    }
}