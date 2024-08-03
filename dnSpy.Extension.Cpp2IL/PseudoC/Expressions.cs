using System.Runtime.CompilerServices;
using Cpp2IL.Core.ISIL;
using Cpp2ILAdapter.PseudoC.Passes;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter.PseudoC;

public sealed record Expression(ExpressionKind Kind, IEmit? Left = null, IEmit? Right = null, uint Index = 0) : IEmit
{
    public IEmit? Left = Left;
    public ExpressionKind Kind = Kind;
    public IEmit? Right = Right;

    public IEmit? First => Left;
    public IEmit? Second => Right;

    public uint Index { get; set; } = Index;

    public void Write(IDecompilerOutput output, bool end = false)
    {
        if (Kind == ExpressionKind.Nop) return;
        
        switch (Kind)
        {
            case ExpressionKind.Assign:
            case ExpressionKind.Add:
            case ExpressionKind.Sub:
            case ExpressionKind.Mul:
            case ExpressionKind.Div:
            case ExpressionKind.Rem:
            case ExpressionKind.Shl:
            case ExpressionKind.Shr:
            case ExpressionKind.Or:
            case ExpressionKind.Xor:
            case ExpressionKind.And:
            case ExpressionKind.CompareEq:
            case ExpressionKind.CompareNeq:
            case ExpressionKind.CompareGt:
            case ExpressionKind.CompareGe:
            case ExpressionKind.CompareLt:
            case ExpressionKind.CompareLe:
            case ExpressionKind.MemberAccess:
                Left?.Write(output);
                output.Write(GetOperator(Kind), BoxedTextColor.Operator);
                Right?.Write(output);
                break;
            case ExpressionKind.Not:
                output.Write("(", BoxedTextColor.Punctuation);
                output.Write("~", BoxedTextColor.Operator);
                First?.Write(output);
                output.Write(")", BoxedTextColor.Punctuation);
                break;
            case ExpressionKind.Compare:
                output.Write("__compare__", BoxedTextColor.ExtensionMethod);
                output.Write("(", BoxedTextColor.Punctuation);
                Left?.Write(output);
                output.Write(",", BoxedTextColor.Punctuation);
                Right?.Write(output);
                output.Write(")", BoxedTextColor.Punctuation);
                break;
            case ExpressionKind.Return:
                output.Write("return", BoxedTextColor.Keyword);
                if (First != null)
                {
                    output.Write(" ", BoxedTextColor.Punctuation);
                    First.Write(output);
                }
                break;
            case ExpressionKind.Goto:
                output.Write("goto ", BoxedTextColor.Keyword);
                First!.Write(output);
                break;
            case ExpressionKind.Deref:
                output.Write("*(", BoxedTextColor.Punctuation);
                First!.Write(output);
                output.Write(")", BoxedTextColor.Punctuation);
                break;
            case ExpressionKind.If:
                output.Write("if ", BoxedTextColor.Keyword);
                output.Write("(", BoxedTextColor.Punctuation);
                First!.Write(output);
                output.Write(")", BoxedTextColor.Punctuation);
                output.WriteLine();
                if (Second is EmitBlock)
                {
                    output.WriteLine("{", BoxedTextColor.Punctuation);
                    output.IncreaseIndent();
                    Second.Write(output);
                    output.DecreaseIndent();
                    output.WriteLine("}", BoxedTextColor.Punctuation);
                    end = false;
                }
                else
                {
                    output.IncreaseIndent();
                    Second!.Write(output);
                    output.DecreaseIndent();
                }
                break;
            case ExpressionKind.Call:
            {
                var args = (InlineEmitBlock)Second!;
                if (First is ManagedFunctionReference { Method.IsStatic: false })
                {
                    args.StartIndex = 1;
                    var thus = args.Items[0];
                    if (thus is Expression { IsMathExpression: true })
                    {
                        output.Write("(", BoxedTextColor.Punctuation);
                        thus.Write(output);
                        output.Write(")", BoxedTextColor.Punctuation);
                    }
                    else
                    {
                        thus.Write(output);
                    }
                    output.Write(".", BoxedTextColor.Punctuation);
                }
                First!.Write(output);
                output.Write("(", BoxedTextColor.Punctuation);
                Second!.Write(output);
                output.Write(")", BoxedTextColor.Punctuation);
                break;
            }
        }
        
        if (end)
            output.WriteLine(";", BoxedTextColor.Punctuation);
    }

    public void AcceptPass(BasePass pass)
    {
        if (Left is Expression leftExpr)
        {
            pass.AcceptExpression(ref Unsafe.As<IEmit, Expression>(ref Left));
            leftExpr.AcceptPass(pass);
        }
        
        if (Second is Block block)
        {
            block.AcceptPass(pass);
        }
        else if (Right is Expression rightExpr)
        {
            pass.AcceptExpression(ref Unsafe.As<IEmit, Expression>(ref Right));
            rightExpr.AcceptPass(pass);
        }
    }

    public Expression FixIf(IsilMnemonic ifType)
    {
        if (First is not Expression)
            return this;
        
        ((Expression)First!).Kind = ifType switch
        {
            IsilMnemonic.JumpIfEqual => ExpressionKind.CompareEq,
            IsilMnemonic.JumpIfNotEqual => ExpressionKind.CompareNeq,
            IsilMnemonic.JumpIfGreater => ExpressionKind.CompareGt,
            IsilMnemonic.JumpIfGreaterOrEqual => ExpressionKind.CompareGe,
            IsilMnemonic.JumpIfLess => ExpressionKind.CompareLt,
            IsilMnemonic.JumpIfLessOrEqual => ExpressionKind.CompareLe,
            _ => throw new ArgumentOutOfRangeException()
        };
        return this;
    }

    public void NopSelf()
    {
        Kind = ExpressionKind.Nop;
        Left = null;
        Right = null;
    }

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
    
    private static string GetOperator(ExpressionKind kind)
    {
        return kind switch
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
            _ => throw new NotImplementedException()
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
    If,
    Call,
    Return,
    
    Compare,
    CompareEq, CompareNeq, 
    CompareGt, CompareGe, CompareLt, CompareLe,
    Goto,
    
    MemberAccess,
}