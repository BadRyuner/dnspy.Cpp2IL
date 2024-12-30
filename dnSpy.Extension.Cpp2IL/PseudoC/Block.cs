using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter.PseudoC;

public sealed class Block(EBlockType type, int id) : IEmit
{
    public readonly int Id = id;
    public EBlockType Type = type;
    public uint StartIsilIndex = 0;
    public bool IgnoreBlock;
    
    public readonly List<Block> Successors = [];
    public readonly List<Block> Predecessors = [];

    public List<IEmit> ToEmit = new List<IEmit>(2);
    
    public void Write(IDecompilerOutput output)
    {
        if (IgnoreBlock) return;
        
        output.DecreaseIndent();
        output.WriteLine($"ISIL_{StartIsilIndex}:", BoxedTextColor.Label);
        output.IncreaseIndent();
        for (var i = 0; i < ToEmit.Count; i++)
        {
            var emit = ToEmit[i];
            
            if (emit is Expression { Eliminated: true })
                continue;
            
            emit.Write(output);
            if (emit is not IfExpression and not IfElseExpression and not WhileExpression and not Nop )
                output.WriteLine(";", BoxedTextColor.Punctuation);
        }
    }
}

public enum EBlockType : byte
{
    None, 
    
    Interrupt,
    Continue,
    Jump,
    If,
}