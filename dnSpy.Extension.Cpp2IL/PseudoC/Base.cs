using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cpp2ILAdapter.PseudoC.Passes;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter.PseudoC;

public interface IEmit
{
    void Write(IDecompilerOutput output, bool end = false);
}

public sealed class EmitBlock : IEmit
{
    public EmitBlock(uint index)
    {
        Index = index;
        LabelStart = $"ISIL_{index}";
    }
    
    public readonly uint Index;
    public readonly string LabelStart;
    public bool ShouldEmitLabel => ReferencesCount > 1;
    private bool _emitted = false;
    public ushort ReferencesCount = 0;
    public readonly List<IEmit> Items = new(2);
    public void Add(IEmit item) => Items.Add(item);
    
    public void Write(IDecompilerOutput output, bool end)
    {
        if (_emitted) return;
        _emitted = true;
        if (ShouldEmitLabel)
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

    public void AcceptPass(BasePass pass)
    {
        pass.AcceptEmitBlock(this);
        var items = CollectionsMarshal.AsSpan(Items);
        for (var i = 0; i < Items.Count; i++)
        {
            var item = items[i];
            if (item is Expression expr)
            {
                pass.AcceptExpression(ref Unsafe.As<IEmit, Expression>(ref items[i]));
                expr.AcceptPass(pass);
            }
        }
    }
}

public sealed class InlineEmitBlock : IEmit
{
    public InlineEmitBlock(string delimiter)
    {
        Delimiter = delimiter;
    }

    public int StartIndex = 0;
    public readonly string Delimiter;
    public readonly List<IEmit> Items = new(1);
    public void Add(IEmit item) => Items.Add(item);
    public void Write(IDecompilerOutput output, bool end)
    {
        var len = Items.Count - 1;
        for (var i = StartIndex; i <= len; i++)
        {
            Items[i].Write(output, false);
            if (i != len)
                output.Write(Delimiter, BoxedTextColor.Punctuation);
        }
    }
}

public sealed record Unsupported(string What) : IEmit
{
    public void Write(IDecompilerOutput output, bool end = false)
    {
        if (end)
            output.WriteLine($"/* Unsupported {What} */", BoxedTextColor.Comment);
        else
            output.Write($"/* Unsupported {What} */", BoxedTextColor.Comment);
    }
}