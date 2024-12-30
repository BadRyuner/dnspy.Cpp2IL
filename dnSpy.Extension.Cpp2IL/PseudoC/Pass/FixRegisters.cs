using System.Text.RegularExpressions;

namespace Cpp2ILAdapter.PseudoC.Pass;

public sealed class FixRegisters : BasePass
{
    private static readonly Regex ArmRegs = new Regex(@"[W|X|w|x]\d+", RegexOptions.Compiled);
    
    protected override void AcceptSingleIEmit(IEmit emit)
    {
        if (emit is Register register)
        {
            register.Name = register.Name switch
            {
                "al" or "ax" or "eax" or "rax" => "rax",
                "bl" or "bx" or "ebx" or "rbx" => "rbx",
                "cl" or "cx" or "ecx" or "rcx" => "rcx",
                "dl" or "dx" or "edx" or "rdx" => "rdx",
                "si" or "sil" or "esi" or "rsi" => "rsi",
                "di" or "dil" or "edi" or "rdi" => "rdi",
                "sp" or "spl" or "esp" or "rsp" => "rsp",
                "r8b" or "r8w" or "r8d" or "r8" => "r8",
                "r9b" or "r9w" or "r9d" or "r9" => "r9",
                "r10b" or "r10w" or "r10d" or "r10" => "r10",
                "r11b" or "r11w" or "r11d" or "r11" => "r11",
                "r12b" or "r12w" or "r12d" or "r12" => "r12",
                "r13b" or "r13w" or "r13d" or "r13" => "r13",
                "r14b" or "r14w" or "r14d" or "r14" => "r14",
                "r15b" or "r15w" or "r15d" or "r15" => "r15",
                // todo add for other regs
                { } armReg when ArmRegs.IsMatch(register.Name) => $"X{armReg[1..]}",
                _ => register.Name // well ok
            };
        }
        base.AcceptSingleIEmit(emit);
    }
}