using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter.PseudoC;

public interface IEmit
{
    void Write(IDecompilerOutput output);
}

public sealed record Unsupported(string Why) : IEmit
{
    public void Write(IDecompilerOutput output)
    {
        if (Why.Length < 50)
        {
            output.Write("// Unsupported: ", BoxedTextColor.Comment);
            output.Write(Why, BoxedTextColor.Comment);
            return;
        }
        output.WriteLine("/* Unsupported: ", BoxedTextColor.Comment);
        output.WriteLine(Why, BoxedTextColor.Comment);
        output.Write("*/", BoxedTextColor.Comment);
    }
}