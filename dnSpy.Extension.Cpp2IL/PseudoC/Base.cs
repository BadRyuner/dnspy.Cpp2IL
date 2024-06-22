using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cpp2ILAdapter.PseudoC.Passes;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter.PseudoC;

public interface IEmit
{
    public uint Index { get; set; }
    
    void Write(IDecompilerOutput output, bool end = false);
}

public abstract class Block : IEmit
{
    public readonly List<IEmit> Items = new(2);
    public void Add(IEmit item) => Items.Add(item);

    public uint Index { get; set; }
    
    public abstract void Write(IDecompilerOutput output, bool end);
    
    public void AcceptPass(BasePass pass)
    {
        pass.AcceptBlock(this);
        var items = CollectionsMarshal.AsSpan(Items);
        for (var i = 0; i < Items.Count; i++)
        {
            var item = items[i];
            if (item is Expression expr)
            {
                expr.AcceptPass(pass);
                pass.AcceptExpression(ref Unsafe.As<IEmit, Expression>(ref items[i]));
            }
        }
    }
}

public sealed class EmitBlock : Block
{
    public EmitBlock(uint index)
    {
        BlockIndex = index;
        LabelStart = $"ISIL_{index}";
    }
    
    public readonly uint BlockIndex;
    public readonly string LabelStart;
    public bool ShouldEmitLabel => ReferencesCount > 1;
    //private bool _emitted = false;
    public ushort ReferencesCount = 0;
    
    public override void Write(IDecompilerOutput output, bool end)
    {
        //if (_emitted) return;
        //_emitted = true;
        if (ShouldEmitLabel | true)
        {
            output.DecreaseIndent();
            output.WriteLine(LabelStart + ':', BoxedTextColor.Label);
            output.IncreaseIndent();
        }
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].Write(output, true);
        }
    }
}

public sealed class InlineEmitBlock : Block
{
    public InlineEmitBlock(string delimiter)
    {
        Delimiter = delimiter;
    }

    public int StartIndex = 0;
    public readonly string Delimiter;
    public override void Write(IDecompilerOutput output, bool end)
    {
        var len = Items.Count - 1;
        for (var i = StartIndex; i <= len; i++)
        {
            Items[i].Write(output);
            if (i != len)
                output.Write(Delimiter, BoxedTextColor.Punctuation);
        }
    }
}

public sealed record Unsupported(string What) : IEmit
{
    public uint Index { get; set; }

    public void Write(IDecompilerOutput output, bool end = false)
    {
        if (end)
            output.WriteLine($"/* Unsupported {What} */", BoxedTextColor.Comment);
        else
            output.Write($"/* Unsupported {What} */", BoxedTextColor.Comment);
    }
}