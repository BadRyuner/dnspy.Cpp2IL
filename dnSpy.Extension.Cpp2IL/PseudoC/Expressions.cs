using System.Runtime.CompilerServices;
using Cpp2IL.Core.ISIL;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter.PseudoC;

public abstract record Expression(ExpressionKind Kind) : IEmit
{
    public bool Eliminated = false;
    
    public abstract void Write(IDecompilerOutput output);

    public abstract int ChildrenCount { get; }

    public abstract ref IEmit GetChildren(int id);
    
    public bool IsMathExpression => Kind switch
    {
        ExpressionKind.Add => true,
        ExpressionKind.Sub => true,
        ExpressionKind.Mul => true,
        ExpressionKind.Div => true,
        ExpressionKind.Rem => true,
        ExpressionKind.Or => true,
        ExpressionKind.Xor => true,
        ExpressionKind.And => true,
        _ => false
    };
    
    public string GetOperator()
    {
        return Kind switch
        {
            ExpressionKind.Assign => " = ",
            ExpressionKind.Add => " + ",
            ExpressionKind.Sub => " - ",
            ExpressionKind.Mul => " * ",
            ExpressionKind.Div => " / ",
            ExpressionKind.Rem => " % ",
            ExpressionKind.Shl => " << ",
            ExpressionKind.Shr => " >> ",
            ExpressionKind.Or => " | ",
            ExpressionKind.Xor => " ^ ",
            ExpressionKind.And => " & ",
            ExpressionKind.CompareEq => " == ",
            ExpressionKind.CompareNeq => " != ",
            ExpressionKind.CompareGt => " > ",
            ExpressionKind.CompareGe => " >= ",
            ExpressionKind.CompareLt => " < ",
            ExpressionKind.CompareLe => " <= ",
            ExpressionKind.MemberAccess => ".",
            _ => throw new NotImplementedException(Kind.ToString())
        };
    }
}

public enum ExpressionKind : byte
{
    Nop,
    Assign,
    Deref,
    Add, Sub, Mul, Div, Rem,
    Or, And, Xor,
    Not,
    Shl, Shr,
    Call,
    Return,
    
    If, IfElse, 
    While,
    
    Compare,
    CompareEq, CompareNeq, 
    CompareGt, CompareGe, CompareLt, CompareLe,
    Goto,
    
    VectorAccess,
    
    MemberAccess,
}

public sealed record Nop() : Expression(ExpressionKind.Nop)
{
    public static readonly Nop Shared = new();
    
    public override void Write(IDecompilerOutput output) { }

    public override int ChildrenCount => 0;
    public override ref IEmit GetChildren(int id) => throw new Exception("Unexpected shit");
}

public sealed record IfExpression(IEmit Condition, IEmit Body) : Expression(ExpressionKind.If)
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("if", BoxedTextColor.Keyword);
        output.Write("(", BoxedTextColor.Punctuation);
        Condition.Write(output);
        output.WriteLine(")", BoxedTextColor.Punctuation);
        output.WriteLine("{", BoxedTextColor.Punctuation);
        output.IncreaseIndent();
        Body.Write(output);
        output.WriteLine();
        output.DecreaseIndent();
        output.WriteLine("}", BoxedTextColor.Punctuation);
    }

    public IEmit Condition = Condition;
    public IEmit Body = Body;
    
    public override int ChildrenCount => 2;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Condition;
        if (id == 1)
            return ref Body;
        throw new Exception("Unexpected shit");
    }
}

public sealed record IfElseExpression(IEmit Condition, IEmit If, IEmit Else) : Expression(ExpressionKind.IfElse)
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("if ", BoxedTextColor.Keyword);
        output.Write("(", BoxedTextColor.Punctuation);
        Condition.Write(output);
        output.WriteLine(")", BoxedTextColor.Punctuation);
        output.Write("{", BoxedTextColor.Punctuation);
        output.IncreaseIndent();
        If.Write(output);
        output.DecreaseIndent();
        output.WriteLine("}", BoxedTextColor.Punctuation);
        output.Write("else ", BoxedTextColor.Keyword);
        var brackets = Else is not IfExpression and not IfElseExpression;
        if (brackets)
        {
            output.Write("{", BoxedTextColor.Punctuation);
            output.IncreaseIndent();
        }
        Else.Write(output);
        if (brackets)
        {
            output.DecreaseIndent();
            output.WriteLine("}", BoxedTextColor.Punctuation);
        }
    }
    
    public IEmit Condition = Condition;
    public IEmit If = If;
    public IEmit Else = Else;
    
    public override int ChildrenCount => 3;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Condition;
        if (id == 1)
            return ref If;
        if (id == 2)
            return ref Else;
        throw new Exception("Unexpected shit");
    }
}

public sealed record WhileExpression(IEmit Condition, IEmit Body) : Expression(ExpressionKind.While)
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("while", BoxedTextColor.Keyword);
        output.Write("(", BoxedTextColor.Punctuation);
        Condition.Write(output);
        output.WriteLine(")", BoxedTextColor.Punctuation);
        output.WriteLine("{", BoxedTextColor.Punctuation);
        output.IncreaseIndent();
        Body.Write(output);
        output.DecreaseIndent();
        output.WriteLine("}", BoxedTextColor.Punctuation);
    }
    
    public IEmit Condition = Condition;
    public IEmit Body = Body;
    
    public override int ChildrenCount => 2;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Condition;
        if (id == 1)
            return ref Body;
        throw new Exception("Unexpected shit");
    }
}

public sealed record AssignExpression(IEmit Target, IEmit Value) : Expression(ExpressionKind.Assign)
{
    public override void Write(IDecompilerOutput output)
    {
        Target.Write(output);
        output.Write(" = ", BoxedTextColor.Punctuation);
        Value.Write(output);
    }
    
    public IEmit Value = Value;
    public IEmit Target = Target;
    
    public override int ChildrenCount => 2;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Value;
        if (id == 1)
            return ref Target;
        throw new Exception("Unexpected shit");
    }
}

public sealed record ReturnExpression(IEmit? Value) : Expression(ExpressionKind.Return)
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("return ", BoxedTextColor.Keyword);
        Value?.Write(output);
    }

    public IEmit? Value = Value;
    
    public override int ChildrenCount => 1;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Value!;
        throw new Exception("Unexpected shit");
    }
}

public sealed record GotoExpression(IEmit? Value) : Expression(ExpressionKind.Goto)
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("goto ", BoxedTextColor.Keyword);
        Value?.Write(output);
    }
    
    public IEmit? Value = Value;
    
    public override int ChildrenCount => 1;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Value!;
        throw new Exception("Unexpected shit");
    }
}

public sealed record MathExpression(ExpressionKind Kind, IEmit Left, IEmit Right) : Expression(Kind)
{
    public override void Write(IDecompilerOutput output)
    {
        Left.Write(output);
        output.Write(GetOperator(), BoxedTextColor.Keyword);
        Right.Write(output);
    }

    public IEmit Left = Left;
    public IEmit Right = Right;
    
    public override int ChildrenCount => 2;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Left;
        if (id == 1)
            return ref Right;
        throw new Exception("Unexpected shit");
    }
}

public sealed record CompareExpression(ExpressionKind CompareKind, IEmit Left, IEmit Right) : Expression(CompareKind)
{
    public ExpressionKind CompareKind { get; set; } = CompareKind;
    public IEmit Right = Right;
    public IEmit Left = Left;

    public override void Write(IDecompilerOutput output)
    {
        Left.Write(output);
        output.Write(CompareKind switch
        {
            ExpressionKind.CompareEq => " == ",
            ExpressionKind.CompareNeq => " != ",
            ExpressionKind.CompareGt => " > ",
            ExpressionKind.CompareGe => " >= ",
            ExpressionKind.CompareLt => " < ",
            ExpressionKind.CompareLe => " <= ",
            ExpressionKind.Compare => " unresolvedShit ",
            _ => throw new Exception("What the fuck")
        }, BoxedTextColor.Keyword);
        Right.Write(output);
    }
    
    public override int ChildrenCount => 2;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Left;
        if (id == 1)
            return ref Right;
        throw new Exception("Unexpected shit");
    }
}

public sealed record NotExpression(IEmit Value) : Expression(ExpressionKind.Not)
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("!(", BoxedTextColor.Punctuation);
        Value.Write(output);
        output.Write(")", BoxedTextColor.Punctuation);
    }

    public IEmit Value = Value;
    
    public override int ChildrenCount => 1;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Value;
        throw new Exception("Unexpected shit");
    }
}

public sealed record DerefExpression(IEmit Value) : Expression(ExpressionKind.Deref)
{
    public override void Write(IDecompilerOutput output)
    {
        output.Write("*(", BoxedTextColor.Punctuation);
        Value.Write(output);
        output.Write(")", BoxedTextColor.Punctuation);
    }

    public IEmit Value = Value;
    
    public override int ChildrenCount => 1;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Value;
        throw new Exception("Unexpected shit");
    }
}

public sealed record VectorAccessExpression(IEmit Vector, IEmit Index) : Expression(ExpressionKind.VectorAccess)
{
    public override void Write(IDecompilerOutput output)
    {
        Vector.Write(output);
        output.Write("[", BoxedTextColor.Punctuation);
        Index.Write(output);
        output.Write("]", BoxedTextColor.Punctuation);
    }

    public IEmit Index = Index;
    public IEmit Vector = Vector;
    
    public override int ChildrenCount => 2;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Index;
        if (id == 1)
            return ref Vector;
        throw new Exception("Unexpected shit");
    }
}

public sealed record CallExpression(IEmit Method, IEmit[] Arguments) : Expression(ExpressionKind.Call)
{
    public override void Write(IDecompilerOutput output)
    {
        var args = 0;
        if (Method is ManagedFunctionReference { Method: { IsStatic: false } })
        {
            args = 1;
            Arguments[0].Write(output);
            output.Write(".", BoxedTextColor.Punctuation);
        }
        Method.Write(output);
        output.Write("(", BoxedTextColor.Punctuation);
        for (; args < Arguments.Length; args++)
        {
            Arguments[args].Write(output);
            if (args != Arguments.Length - 1)
                output.Write(", ", BoxedTextColor.Punctuation);
        }
        output.Write(")", BoxedTextColor.Punctuation);
    }

    public IEmit Method = Method;
    public IEmit[] Arguments = Arguments;
    
    public override int ChildrenCount => 1 + Arguments.Length;

    public override ref IEmit GetChildren(int id)
    {
        if (id == 0)
            return ref Method;
        if (id > 0)
            return ref Arguments[id - 1];
        throw new Exception("Unexpected shit");
    }
}
