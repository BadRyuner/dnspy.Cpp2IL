using System.Globalization;
using System.Linq;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter;

public static class IsilLifter
{
    public abstract record Lifted
    {
        public abstract void Write(IDecompilerOutput output);
    }

    private record Nop() : Lifted
    {
        internal static readonly Nop Shared = new();
        public override void Write(IDecompilerOutput output)
        {
        }
    }
    
    private record Variable(string Name) : Lifted
    {
        public override void Write(IDecompilerOutput output)
        {
            output.Write(Name, BoxedTextColor.Local);
        }
    }
    
    private record Constant(string Value, object Color) : Lifted
    {
        public override void Write(IDecompilerOutput output)
        {
            output.Write(Value, Color);
        }
    }
    
    private record Return(Lifted What) : Lifted
    {
        internal static readonly Return NoReturn = new Return(new Variable(string.Empty)); // haha, eat it NRT
        public override void Write(IDecompilerOutput output)
        {
            output.Write("return", BoxedTextColor.Keyword);
            if (this != NoReturn)
            {
                output.Write(" ", BoxedTextColor.Local);
                What.Write(output);
            }
            output.WriteLine(";", BoxedTextColor.Local);
        }
    } 
    
    private record Set(Lifted Left, Lifted Right) : Lifted
    {
        public override void Write(IDecompilerOutput output)
        {
            Left.Write(output);
            output.Write(" = ", BoxedTextColor.Local);
            Right.Write(output);
            output.WriteLine(";", BoxedTextColor.Local);
        }

        public Lifted Left { get; set; } = Left;
        public Lifted Right { get; set; } = Right;
    }

    private record MathOp(Lifted Left, string Op, Lifted Right) : Lifted
    {
        public override void Write(IDecompilerOutput output)
        {
            Left.Write(output);
            output.Write(" ", BoxedTextColor.Local);
            output.Write(Op, BoxedTextColor.Local);
            output.Write(" ", BoxedTextColor.Local);
            Right.Write(output);
        }
    }

    private record Call(string Func, Lifted[] Args, bool Ret) : Lifted
    {
        public override void Write(IDecompilerOutput output)
        {
            output.Write(Func, BoxedTextColor.StaticMethod);
            output.Write("(", BoxedTextColor.Local);
            if (Args.Length > 0)
            {
                bool first = true;
                foreach (var arg in Args)
                {
                    if (!first)
                        output.Write(", ", BoxedTextColor.Local);
                    arg.Write(output);
                    first = false;
                }
            }
            output.Write(")", BoxedTextColor.Local);
            if (!Ret)
                output.WriteLine(";", BoxedTextColor.Local);
        }
    }

    private record Label(string Name) : Lifted
    {
        public override void Write(IDecompilerOutput output)
        {
            output.DecreaseIndent();
            output.Write(Name, BoxedTextColor.Label);
            output.WriteLine(":", BoxedTextColor.Local);
            output.IncreaseIndent();
        }
    }

    private record Goto(Lifted Label) : Lifted
    {
        private static readonly string Kw = "goto ";
        
        public override void Write(IDecompilerOutput output)
        {
            output.Write(Kw, BoxedTextColor.Keyword);
            Label.Write(output);
            output.WriteLine(";", BoxedTextColor.Local);
        }
    }

    private record If(Lifted Condition, Lifted Body) : Lifted
    {
        private static readonly string Kw = "if ";
        public Lifted Condition { get; set; } = Condition;

        public override void Write(IDecompilerOutput output)
        {
            output.Write(Kw, BoxedTextColor.Keyword);
            output.Write("(", BoxedTextColor.Local);
            Condition.Write(output);
            output.Write(") ", BoxedTextColor.Local);
            Body.Write(output);
        }
    }

    private record Deref(Lifted What) : Lifted
    {
        private static readonly string Kw = "*";
        public override void Write(IDecompilerOutput output)
        {
            output.Write(Kw, BoxedTextColor.Local);
            output.Write("(", BoxedTextColor.Local);
            What.Write(output);
            output.Write(")", BoxedTextColor.Local);
        }
    }

    private record AccessField(Lifted Object, string Field) : Lifted
    {
        private static readonly string Kw = ".";
        public override void Write(IDecompilerOutput output)
        {
            Object.Write(output);
            output.Write(Kw, BoxedTextColor.Local);
            output.Write(Field, BoxedTextColor.InstanceField);
        }
    }
    
    private static readonly string Add = "+";
    private static readonly string Sub = "-";
    private static readonly string Div = "/";
    private static readonly string Mul = "*";
    private static readonly string Shl = "<<";
    private static readonly string Shr = ">>";
    private static readonly string And = "&";
    private static readonly string Or = "|";
    private static readonly string Xor = "^";
    private static readonly string Cmp = "==";

    private static readonly string IncSet = "+=";

    private static readonly Variable CmpResult = new Variable("__cmp_res"); 
    private static readonly Constant ResEq = new("equals", BoxedTextColor.Local); 
    private static readonly Constant ResNeq = new("not equals", BoxedTextColor.Local); 
    private static readonly Constant ResGt = new("greater", BoxedTextColor.Local); 
    private static readonly Constant ResGte = new("greater or equals", BoxedTextColor.Local); 
    private static readonly Constant ResLt = new("less", BoxedTextColor.Local); 
    private static readonly Constant ResLte = new("less or equals", BoxedTextColor.Local); 
    private static readonly Variable Stack = new("__stack");
    private static readonly Variable Result = new("__result");
    private static readonly Variable This = new("this");
    
    public static List<Lifted> Lift(MethodAnalysisContext context)
    {
        var list = new List<Lifted>(8);
        var mappedRegs = MapArgs(context);
        var jumpDudes = new List<InstructionSetIndependentInstruction>();

        foreach (var instruction in context.ConvertedIsil!)
        {
            if (instruction.Operands.Length == 1 && instruction.Operands[0].Data is InstructionSetIndependentInstruction instr)
                jumpDudes.Add(instr);
        }
        
        foreach (var instruction in context.ConvertedIsil!)
        {
            if (jumpDudes.Contains(instruction))
                list.Add(new Label($"label_0x{instruction.InstructionIndex:X2}"));
            
            switch (instruction.OpCode.Mnemonic)
            {
                case IsilMnemonic.Move:
                    list.Add(new Set(ConvertOperand(instruction.Operands[0]), ConvertOperand(instruction.Operands[1])));
                    break;
                case IsilMnemonic.LoadAddress:
                    list.Add(new Set(ConvertOperand(instruction.Operands[0]), new Call("__address_of", new[] { ConvertOperand(instruction.Operands[1]) }, true)));
                    break;
                case IsilMnemonic.Call:
                {
                    if (instruction.Operands[0].Data is IsilImmediateOperand imm)
                    {
                        Call call;
                        if (context.AppContext.MethodsByAddress.TryGetValue((ulong)imm.Value, out var methods))
                        {
                            var method = methods[0];
                            var args = new Lifted[instruction.Operands.Length - 1];
                            for (var i = 1; i < instruction.Operands.Length; i++)
                            {
                                var op = instruction.Operands[i];
                                if (mappedRegs.TryGetValue(op.Data.ToString() ?? string.Empty, out var mapped))
                                    args[i - 1] = mapped;
                                else
                                    args[i - 1] = new Variable(op.Data.ToString() ?? string.Empty);
                            }
                            call = new Call($"{method.DeclaringType?.FullName}::{method.Name}", args, !method.IsVoid);
                            if (call.Ret)
                            {
                                list.Add(new Set(Result, call));
                                break;
                            }
                        }
                        else
                            call = new Call(imm.ToString(), Array.Empty<Lifted>(), false);
                        list.Add(call);
                    }
                    break;
                }
                case IsilMnemonic.CallNoReturn:
                {
                    if (instruction.Operands[0].Data is IsilImmediateOperand imm)
                    {
                        Call call;
                        if (context.AppContext.MethodsByAddress.TryGetValue((ulong)imm.Value, out var methods))
                        {
                            var method = methods[0];
                            var args = new Lifted[instruction.Operands.Length - 1];
                            for (var i = 1; i < instruction.Operands.Length; i++)
                            {
                                var op = instruction.Operands[i];
                                if (mappedRegs.TryGetValue(op.Data.ToString() ?? string.Empty, out var mapped))
                                    args[i - 1] = mapped;
                                else
                                    args[i - 1] = new Variable(op.Data.ToString() ?? string.Empty);
                            }
                            call = new Call($"{method.DeclaringType?.FullName}::{method.Name}", args, !method.IsVoid);
                        }
                        else
                            call = new Call(imm.ToString(), Array.Empty<Lifted>(), false);
                        list.Add(new Return(call));
                    }
                    break;
                }
                case IsilMnemonic.Exchange:
                    break;
                
                case IsilMnemonic.Add:
                case IsilMnemonic.Multiply:
                {
                    var op = instruction.OpCode.Mnemonic == IsilMnemonic.Add ? Add : Mul;
                    var math = new MathOp(ConvertOperand(instruction.Operands[1]), op, ConvertOperand(instruction.Operands[2]));
                    var set = new Set(ConvertOperand(instruction.Operands[0]), math);
                    list.Add(set);
                    break;
                }
                case IsilMnemonic.Subtract:
                case IsilMnemonic.Divide:
                case IsilMnemonic.ShiftLeft:
                case IsilMnemonic.ShiftRight:
                case IsilMnemonic.And:
                case IsilMnemonic.Or:
                case IsilMnemonic.Xor:
                {
                    var op = instruction.OpCode.Mnemonic switch
                    {
                        IsilMnemonic.Subtract => Sub,
                        IsilMnemonic.Divide => Div,
                        IsilMnemonic.ShiftLeft => Shl,
                        IsilMnemonic.ShiftRight => Shr,
                        IsilMnemonic.And => And,
                        IsilMnemonic.Or => Or,
                        IsilMnemonic.Xor => Xor,
                        _ => "err"
                    };
                    var math = new MathOp(ConvertOperand(instruction.Operands[0]), op, ConvertOperand(instruction.Operands[1]));
                    var set = new Set(ConvertOperand(instruction.Operands[0]), math);
                    list.Add(set);
                    break;
                }
                case IsilMnemonic.Not:
                    break;
                case IsilMnemonic.Compare:
                {
                    var left = ConvertOperand(instruction.Operands[1]);
                    var right = ConvertOperand(instruction.Operands[1]);
                    list.Add(new Set(CmpResult, new Call("compare", new[] { left, right }, true)));
                    break;
                }
                case IsilMnemonic.ShiftStack:
                {
                    list.Add(new MathOp(Stack, IncSet, ConvertOperand(instruction.Operands[0])));
                    break;
                }
                case IsilMnemonic.Push:
                {
                    list.Add(new Call("push", new[] { ConvertOperand(instruction.Operands[0]) }, false));
                    break;
                }
                case IsilMnemonic.Pop:
                {
                    list.Add(new Set(ConvertOperand(instruction.Operands[0]), new Call("pop", Array.Empty<Lifted>(), true)));
                    break;
                }
                case IsilMnemonic.Return:
                    if (context.IsVoid)
                        list.Add(Return.NoReturn);
                    else
                        list.Add(new Return(ConvertOperand(instruction.Operands[0])));
                    break;
                case IsilMnemonic.Goto:
                    list.Add(new Goto(ConvertOperand(instruction.Operands[0])));
                    break;
                case IsilMnemonic.JumpIfEqual:
                case IsilMnemonic.JumpIfNotEqual:
                case IsilMnemonic.JumpIfGreater:
                case IsilMnemonic.JumpIfGreaterOrEqual:
                case IsilMnemonic.JumpIfLess:
                case IsilMnemonic.JumpIfLessOrEqual:
                {
                    Lifted op = instruction.OpCode.Mnemonic switch
                    {
                        IsilMnemonic.JumpIfEqual => ResEq,
                        IsilMnemonic.JumpIfNotEqual => ResNeq,
                        IsilMnemonic.JumpIfGreater => ResGt,
                        IsilMnemonic.JumpIfGreaterOrEqual => ResGte,
                        IsilMnemonic.JumpIfLess => ResLt,
                        IsilMnemonic.JumpIfLessOrEqual => ResLte,
                        _ => new Variable("err")
                    };
                    list.Add(new If(new MathOp(CmpResult, "is", op), new Goto(ConvertOperand(instruction.Operands[0]))));
                    break;
                }
                case IsilMnemonic.SignExtend:
                {
                    var op = ConvertOperand(instruction.Operands[0]);
                    list.Add(new Set(op, new Call("sext", new[] { op }, true)));
                    break;
                }
                case IsilMnemonic.Interrupt:
                    break;
                case IsilMnemonic.NotImplemented:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        Optimize(list, context);
        return list;

        Lifted ConvertOperand(in InstructionSetIndependentOperand operand)
        {
            switch (operand.Data)
            {
                case InstructionSetIndependentInstruction label:
                    return new Variable($"label_{label.InstructionIndex:X2}");
                    break;
                case IsilImmediateOperand isilImmediateOperand:
                    return new Constant(isilImmediateOperand.Value.ToString(CultureInfo.InvariantCulture), BoxedTextColor.Number);
                    break;
                case IsilMemoryOperand isilMemoryOperand:
                {
                    Lifted? what = null;
                    if (isilMemoryOperand.Base != null)
                        what = ConvertOperand(isilMemoryOperand.Base.Value);
                    if (isilMemoryOperand.Addend != 0)
                    {
                        var c = new Constant(isilMemoryOperand.Addend.ToString(), BoxedTextColor.Number);
                        if (what != null)
                            what = new MathOp(what, Add, c);
                        else
                            what = c;
                    }
                    if (isilMemoryOperand.Index != null)
                    {
                        var index = ConvertOperand(isilMemoryOperand.Index.Value);
                        if (what == null)
                            what = index;
                        else
                            what = new MathOp(what, Add, index);

                        if (isilMemoryOperand.Scale > 1)
                            what = new MathOp(what, Mul, new Constant(isilMemoryOperand.Scale.ToString(), BoxedTextColor.Number));
                    }
                    return new Deref(what!);
                }
                case IsilRegisterOperand isilRegisterOperand:
                    if (mappedRegs!.TryGetValue(isilRegisterOperand.RegisterName, out var res))
                        return res;
                    return new Variable(isilRegisterOperand.RegisterName);
                case IsilStackOperand isilStackOperand:
                    return new Variable($"stack_0x{isilStackOperand.Offset:X2}");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static void Optimize(List<Lifted> list, MethodAnalysisContext context)
    {
        var thisType = context.DeclaringType;
        for (var i = 0; i < list.Count; i++)
        {
            var instr = list[i];
            if (i == list.Count - 1) continue;

            var next = list[i + 1];

            if (instr is Set { Right: Call { Func: "compare", Args: { } args } })
            {
                var left = args[0];
                var right = args[1];
                if (next is If nextIf)
                {
                    list[i] = Nop.Shared;
                    switch (((Constant)((MathOp)nextIf.Condition).Right).Value)
                    {
                        case "equals": nextIf.Condition = new MathOp(left, "==", right); break;
                        case "not equals": nextIf.Condition = new MathOp(left, "!=", right); break;
                        case "greater": nextIf.Condition = new MathOp(left, ">", right); break;
                        case "greater or equals": nextIf.Condition = new MathOp(left, ">=", right); break;
                        case "less": nextIf.Condition = new MathOp(left, "<=", right); break;
                        case "less or equals": nextIf.Condition = new MathOp(left, "<", right); break;
                    }
                    i++;
                }
            }

            {
                if (instr is Set { Left: Deref { What: MathOp { Left: Variable {} variable, Right: Constant {} offsetStr } } } setField)
                {
                    if (variable == This && Int32.TryParse(offsetStr.Value, out var offset))
                    {
                        var field = thisType!.Fields.FirstOrDefault(f => f.Offset == offset);
                        if (field != null)
                        {
                            setField.Left = new AccessField(variable, field.FieldName);
                        }
                    }
                }
            }
            {
                if (instr is Set { Right: Deref { What: MathOp { Left: Variable {} variable, Right: Constant {} offsetStr } } } setField)
                {
                    if (variable == This && Int32.TryParse(offsetStr.Value, out var offset))
                    {
                        var field = thisType!.Fields.FirstOrDefault(f => f.Offset == offset);
                        if (field != null)
                        {
                            setField.Right = new AccessField(variable, field.FieldName);
                        }
                    }
                }
            }
        }
    }
    
    private static Dictionary<string, Variable> MapArgs(MethodAnalysisContext context)
    {
        if (context.ParameterCount == 0 & context.IsStatic)
            return EmptyMap;
        
        var dict = new Dictionary<string, Variable>();

        var args = context.Parameters;

        var start = context.IsStatic ? 0 : 1;
        
        var argsCount = context.ParameterCount;

        var set = context.AppContext.InstructionSet;
        if (set is NewArmV8InstructionSet)
        {
            if (!context.IsStatic)
                dict.Add($"X0", This);
            
            for (var i = 0; i < argsCount && i < 8; i++)
            {
                dict.Add($"X{i + start}", new Variable(args[i].Name));
                dict.Add($"W{i + start}", new Variable(args[i].Name));
                dict.Add($"D{i}", new Variable(args[i].Name));
                dict.Add($"S{i}", new Variable(args[i].Name));
                dict.Add($"H{i}", new Variable(args[i].Name));
                dict.Add($"V{i}", new Variable(args[i].Name));
            }
        }
        else if (set is X86InstructionSet)
        {
            if (argsCount > 0)
                dict.Add("rcx", new Variable(args[0].Name));
            if (argsCount > 1)
                dict.Add("rdx", new Variable(args[1].Name));
            if (argsCount > 2)
                dict.Add("r8", new Variable(args[2].Name));
            if (argsCount > 3)
                dict.Add("r9", new Variable(args[3].Name));
            dict.Add("eax", Result);
            dict.Add("rax", Result);
        }
        
        return dict;
    }

    private static readonly Dictionary<string, Variable> EmptyMap = new()
    {
        { "eax", Result },
        { "rax", Result },
    };
}