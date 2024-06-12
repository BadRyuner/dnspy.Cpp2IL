using System.Linq;
using System.Reflection;
using System.Windows.Media.Animation;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;

namespace Cpp2ILAdapter.TreeView;

public class MethodNode : DsDocumentNode, IDecompileSelf
{
    public static readonly Guid MyGuid = new("c45cfdcc-f161-4b6c-876e-60688d64e594");
    
    public MethodNode(MethodAnalysisContext context, IDsDocument document) : base(document)
    {
        Context = context;
    }

    public readonly MethodAnalysisContext Context;
    
    public override Guid Guid => MyGuid;
    protected override ImageReference GetIcon(IDotNetImageService dnImgMgr) 
        => Context.Attributes.HasFlag(MethodAttributes.Public) ? DsImages.MethodPublic : DsImages.MethodPrivate;

    protected override void WriteCore(ITextColorWriter output, IDecompiler decompiler, DocumentNodeWriteOptions options)
    {
        output.Write(Context.MethodName);
    }

    public bool Decompile(IDecompileNodeContext context)
    {
        var write = context.Output;
        Context.Analyze();
        write.WriteLine(Context.Definition!.HumanReadableSignature!, BoxedTextColor.Blue);
        write.IncreaseIndent();
        if (Context.ConvertedIsil == null || Context.ConvertedIsil.Count == 0)
        {
            write.WriteLine("No ISIL was generated", BoxedTextColor.Red);
        }
        else
        {
            try
            {
                var white = BoxedTextColor.White;
                foreach (var instruction in Context.ConvertedIsil)
                {
                    write.Write(instruction.InstructionIndex.ToString(), BoxedTextColor.AsmAddress);
                    write.Write(" ", BoxedTextColor.White);
                    write.Write(instruction.OpCode.Mnemonic.ToString(), BoxedTextColor.AsmMnemonic);
                    write.Write(" ", BoxedTextColor.White);
                    var len = instruction.Operands.Length - 1;
                    for (var index = 0; index <= len ; index++)
                    {
                        var operand = instruction.Operands[index];

                        switch (operand.Type)
                        {
                            case InstructionSetIndependentOperand.OperandType.Memory:
                                var mem = (IsilMemoryOperand)operand.Data;
                                if (mem.Base == null && mem.Index == null)
                                {
                                    var ctx = (Cpp2ILDocument)Document;
                                    if (ctx.MethodByRva.TryGetValue(mem.Addend, out var method))
                                    {
                                        write.Write($"({method.DeclaringType?.FullName}::{method.Name})", BoxedTextColor.ExtensionMethod);
                                        break;
                                    }
                                }
                                write.Write(operand.Data.ToString(), BoxedTextColor.AsmNumber);
                                break;
                            case InstructionSetIndependentOperand.OperandType.Immediate:
                            case InstructionSetIndependentOperand.OperandType.StackOffset:
                                write.Write(operand.Data.ToString(), BoxedTextColor.AsmNumber);
                                break;
                            case InstructionSetIndependentOperand.OperandType.Register:
                                write.Write(operand.Data.ToString(), BoxedTextColor.AsmRegister);
                                break;
                            default:
                                write.Write(operand.Data.ToString(), BoxedTextColor.White);
                                break;
                        }
                        
                        if (index != len)
                            write.Write(", ", BoxedTextColor.White);
                    }
                    write.WriteLine();
                }
            }
            catch(Exception e)
            {
                write.WriteLine($"Exception!\n{e}", BoxedTextColor.DarkGreen);
            }
        }
        write.DecreaseIndent();
        return true;
    }
}