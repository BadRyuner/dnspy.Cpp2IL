namespace Cpp2ILAdapter.PseudoC.Passes;

public class RenameRegisters : BasePass
{
    private readonly bool _isX64;

    public RenameRegisters(bool isX64)
    {
        _isX64 = isX64;
    }
    public override void AcceptExpression(ref Expression expression)
    {
        if (expression is { Right: Register reg })
            Simplify(reg);
        if (expression is { Left: Register reg2 })
            Simplify(reg2);
    }

    public override void AcceptBlock(Block block)
    {
    }

    private void Simplify(Register reg)
    {
        if (!_isX64)
        {
            if (reg.Name.StartsWith("W"))
                reg.Name = reg.Name.Replace('W', 'X');
            return;
        }
        
        if (reg.Name[0] == 'r' && !char.IsDigit(reg.Name[1]))
            return;
        
        switch (reg.Name)
        {
            case "al":
            case "ax":
            case "eax":
                reg.Name = "rax";
                return;
            case "bl":
            case "bx":
            case "ebx":
                reg.Name = "rbx";
                return;
            case "cl":
            case "cx":
            case "ecx":
                reg.Name = "rcx";
                return;
            case "dl":
            case "dx":
            case "edx":
                reg.Name = "rdx";
                return;
            case "si":
            case "sil":
            case "esi":
                reg.Name = "rsi";
                return;
            case "di":
            case "dil":
            case "edi":
                reg.Name = "rdi";
                return;
            case "bp":
            case "bpl":
            case "ebp":
                reg.Name = "rbx";
                return;
            case "sp":
            case "spl":
            case "esp":
                reg.Name = "rsp";
                return;
            case "r8b":
            case "r8w":
            case "r8d":
                reg.Name = "r8";
                return;
            case "r9b":
            case "r9w":
            case "r9d":
                reg.Name = "r9";
                return;
            case "r10b":
            case "r10w":
            case "r10d":
                reg.Name = "r10";
                return;
            case "r11b":
            case "r11w":
            case "r11d":
                reg.Name = "r11";
                return;
            case "r12b":
            case "r12w":
            case "r12d":
                reg.Name = "r12";
                return;
            case "r13b":
            case "r13w":
            case "r13d":
                reg.Name = "r13";
                return;
            case "r14b":
            case "r14w":
            case "r14d":
                reg.Name = "r14";
                return;
            case "r15b":
            case "r15w":
            case "r15d":
                reg.Name = "r15";
                return;
        }
    }
}