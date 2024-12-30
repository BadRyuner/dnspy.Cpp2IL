using System.Linq;
using Cpp2IL.Core.Graphs;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.PseudoC.Pass;
using Cpp2ILAdapter.TreeView;
using LibCpp2IL.BinaryStructures;
using Block = Cpp2ILAdapter.PseudoC.Block;

namespace Cpp2ILAdapter;

public static class IsilLifter
{
    public static List<Block> Lift(MethodAnalysisContext context, Cpp2ILDocument document)
    {
        try
        {
            if (context.ConvertedIsil == null)
                context.Analyze();

            var isilBlocks = context.ControlFlowGraph!.Blocks;

            List<Block> blocks = new(isilBlocks.Count);

            CompareExpression? unresolvedCompare = null;
            for (var i = 0; i < isilBlocks.Count; i++)
            {
                var isilBlock = isilBlocks[i];
                
                var block = new Block(isilBlock.BlockType switch
                {
                    BlockType.OneWay => EBlockType.Jump,
                    BlockType.TwoWay => EBlockType.If,
                    BlockType.NWay => EBlockType.If,
                    BlockType.Call => EBlockType.Continue,
                    BlockType.Return => EBlockType.Interrupt,
                    BlockType.Fall => EBlockType.Continue,
                    BlockType.Unknown => EBlockType.Interrupt,
                    BlockType.Interrupt => EBlockType.Interrupt,
                    BlockType.Entry => EBlockType.Continue,
                    BlockType.Exit => EBlockType.Interrupt,
                    _ => throw new Exception($"NotImplemented BlockType: {isilBlock.BlockType}")
                }, isilBlock.ID)
                {
                    StartIsilIndex = isilBlock.isilInstructions.FirstOrDefault()?.InstructionIndex ?? 0,
                    IgnoreBlock = isilBlock.isilInstructions.Count == 0,
                };

                for (var i1 = 0; i1 < isilBlock.isilInstructions.Count; i1++)
                {
                    var transformed = Transform(unresolvedCompare, isilBlock.isilInstructions[i1], context);
                    if (transformed is CompareExpression compareExpression)
                        unresolvedCompare = compareExpression;
                    else
                        block.ToEmit.Add(transformed);
                }
                
                blocks.Add(block);
            }

            // link
            for (var i = 0; i < isilBlocks.Count; i++)
            {
                var isilBlock = isilBlocks[i];
                var block = blocks.First(b => b.Id == isilBlock.ID);
                for (int x = 0; x < isilBlock.Successors.Count; x++)
                {
                    var suc = isilBlock.Successors[x];
                    var sucBlock = blocks.First(b => b.Id == suc.ID);
                    block.Successors.Add(sucBlock);
                }
                for (int x = 0; x < isilBlock.Predecessors.Count; x++)
                {
                    var pre = isilBlock.Predecessors[x];
                    var preBlock = blocks.First(b => b.Id == pre.ID);
                    block.Predecessors.Add(preBlock);
                }
            }
            
            new FixRegisters().AcceptBlocks(blocks);
            new CreateVariablesPass().FillRegistersWithParameters(blocks[0].Id, context).AcceptBlocks(blocks);
            new InlineVariablesPass().AcceptBlocks(blocks);
            blocks = new InlineBranchesPass().Process(blocks);
            
            return blocks;
        }
        catch(Exception e)
        {
            return [new Block(EBlockType.Interrupt, 0) { ToEmit = [ new Unsupported(e.ToString()) ]}];
        }
    }

    private static IEmit Transform(CompareExpression? unresolvedCompare, in InstructionSetIndependentInstruction instruction, MethodAnalysisContext context)
    {
        var operands = instruction.Operands;
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.Move:
                return new AssignExpression(TransformOperand(operands[0]), TransformOperand(operands[1]));
            case IsilMnemonic.And:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.And, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Or:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Or, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Xor:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Xor, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Compare:
                return new CompareExpression(ExpressionKind.Compare, TransformOperand(operands[0]), TransformOperand(operands[1]));
            case IsilMnemonic.Add:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Add, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Subtract:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Sub, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Multiply:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Mul, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.Divide:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Div, TransformOperand(operands[1]), TransformOperand(operands[2])));
            case IsilMnemonic.ShiftLeft:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Shl, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.ShiftRight:
                return new AssignExpression(TransformOperand(operands[0]), new MathExpression(ExpressionKind.Shr, TransformOperand(operands[0]), TransformOperand(operands[1])));
            case IsilMnemonic.Return:
                if (context.Definition?.RawReturnType?.Type == Il2CppTypeEnum.IL2CPP_TYPE_VOID)
                    return new ReturnExpression(null);
                return new ReturnExpression(TransformOperand(operands[0]));
            case IsilMnemonic.Goto:
                return new GotoExpression(new InstructionReference(((InstructionSetIndependentInstruction)operands[0].Data).InstructionIndex));

            case IsilMnemonic.JumpIfEqual:
            case IsilMnemonic.JumpIfNotEqual:
            case IsilMnemonic.JumpIfGreater:
            case IsilMnemonic.JumpIfGreaterOrEqual:
            case IsilMnemonic.JumpIfLessOrEqual:
            case IsilMnemonic.JumpIfLess:
            {
                unresolvedCompare!.CompareKind = instruction.OpCode.Mnemonic switch
                {
                    IsilMnemonic.JumpIfEqual => ExpressionKind.CompareEq,
                    IsilMnemonic.JumpIfNotEqual => ExpressionKind.CompareNeq,
                    IsilMnemonic.JumpIfGreater => ExpressionKind.CompareGt,
                    IsilMnemonic.JumpIfGreaterOrEqual => ExpressionKind.CompareGe,
                    IsilMnemonic.JumpIfLessOrEqual => ExpressionKind.CompareLe,
                    IsilMnemonic.JumpIfLess => ExpressionKind.CompareLt,
                    _ => throw new Exception("What the fuck")
                };
                return new IfExpression(unresolvedCompare, new GotoExpression(TransformOperand(operands[0])));
            }
            
            case IsilMnemonic.Call:
            case IsilMnemonic.CallNoReturn:
            {
                var args = new IEmit[instruction.Operands.Length - 1];
                for (var i = 1; i < instruction.Operands.Length; i++)
                {
                    var op = instruction.Operands[i];
                    args[i - 1] = TransformOperand(op);
                }
                
                IEmit function;
                bool returns = false;
                bool returnsFast = false;
                
                // call by rva
                if (instruction.Operands[0].Data is IsilImmediateOperand { Value: not string } imm)
                {
                    var funcPtr = (ulong)imm.Value;
                    function = new UnmanagedFunctionReference(funcPtr);
                    returns = true; // cursed
                }
                // call by reg
                else if (instruction.Operands[0].Data is IsilRegisterOperand reg)
                {
                    function = TransformOperand(instruction.Operands[0]);
                    returnsFast = true; // not always, but idk how to properly realise it
                }
                // call resolved icall
                else if (instruction.Operands[0].Data is IsilImmediateOperand { Value: string keyFunction })
                {
                    var resolvedFunc = ResolveKeyFunction(keyFunction);
                    returns = resolvedFunc.ReturnsValue;
                    returnsFast = resolvedFunc.LikeJmp;
                    function = resolvedFunc;
                }
                // call resolved method
                else if (instruction.Operands[0].Data is IsilMethodOperand methodOperand)
                {
                    function = new ManagedFunctionReference(methodOperand.Method);
                    returns = methodOperand.Method.Definition?.RawReturnType?.Type is not Il2CppTypeEnum.IL2CPP_TYPE_VOID;
                }
                // what the fuck
                else
                    throw new NotImplementedException($"Cant transform {{{instruction.Operands[0].Data}}} (type: {instruction.Operands[0].Data.GetType()}) to function");

                IEmit call = new CallExpression(function, args);
                if (returnsFast)
                    call = new ReturnExpression(call);
                else if (returns)
                    call = new AssignExpression(GetReturnRegister(context), call);
                return call;
            }

            case IsilMnemonic.Not:
                return new AssignExpression(TransformOperand(instruction.Operands[0]), new NotExpression(TransformOperand(instruction.Operands[0])));

            case IsilMnemonic.LoadAddress:
            {
                var expr = TransformOperand(instruction.Operands[1]);
                if (expr is DerefExpression { Kind: ExpressionKind.Deref, Value: var e })
                    expr = e;
                return new AssignExpression(TransformOperand(instruction.Operands[0]), expr);
            }
            
            case IsilMnemonic.Nop:
                return Nop.Shared;
                
            case IsilMnemonic.Exchange:
            case IsilMnemonic.ShiftStack:
            case IsilMnemonic.Push:
            case IsilMnemonic.Pop:
            case IsilMnemonic.SignExtend:
            case IsilMnemonic.Interrupt:
            case IsilMnemonic.NotImplemented:
            default:
                return new Unsupported(instruction.ToString());
        }
    }
    
    private static IEmit TransformOperand(in InstructionSetIndependentOperand operand)
    {
        switch (operand.Type)
        {
            case InstructionSetIndependentOperand.OperandType.Immediate:
            {
                var value = ((IsilImmediateOperand)operand.Data).Value;
                return new Immediate(value);
            }
            case InstructionSetIndependentOperand.OperandType.Register:
            {
                if (operand.Data is IsilRegisterOperand reg)
                    return new Register(reg.RegisterName);
                else if (operand.Data is IsilVectorRegisterElementOperand vector)
                    return new VectorAccessExpression(new Register(vector.RegisterName), new Immediate(vector.Index));
                throw new NotImplementedException(operand.Data.GetType().ToString());
            }
            case InstructionSetIndependentOperand.OperandType.Instruction:
                return new InstructionReference(((InstructionSetIndependentInstruction)operand.Data).InstructionIndex);
            case InstructionSetIndependentOperand.OperandType.Memory:
            {
                var isilMemoryOperand = (IsilMemoryOperand)operand.Data;
                IEmit? what = null;
                if (isilMemoryOperand.Base != null)
                    what = TransformOperand(isilMemoryOperand.Base.Value);
                if (isilMemoryOperand.Addend != 0)
                {
                    var c = new Immediate(isilMemoryOperand.Addend);
                    if (what != null)
                        what = new MathExpression(ExpressionKind.Add, what, c);
                    else
                        what = c;
                }
                if (isilMemoryOperand.Index != null)
                {
                    var index = TransformOperand(isilMemoryOperand.Index.Value);
                    if (what == null)
                        what = index;
                    else
                        what = new MathExpression(ExpressionKind.Add,what, index);

                    if (isilMemoryOperand.Scale > 1)
                        what = new MathExpression(ExpressionKind.Mul, what, new Immediate(isilMemoryOperand.Scale));
                }
                return new DerefExpression(what!);
            }
            case InstructionSetIndependentOperand.OperandType.StackOffset:
                return new Register("Stack:" + ((IsilStackOperand)operand.Data).Offset);
            default:
                return new Unsupported(operand.ToString() ?? string.Empty);
        }
    }

    private static Register GetReturnRegister(MethodAnalysisContext context)
    {
        var isFloat =
            context.Definition?.RawReturnType?.Type is Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8;

        var set = context.AppContext.InstructionSet;

        if (set is NewArmV8InstructionSet)
            return new Register(isFloat ? "V0" : "X0");
        return new Register(isFloat ? "xmm0" : "rax");
    }

    private static IEmit GetUnmanagedFunction(MethodAnalysisContext context, ulong funcPtr)
    {
        var kfa = context.AppContext.GetOrCreateKeyFunctionAddresses();
        funcPtr = context.AppContext.Binary.GetRva(funcPtr);
        
        if (funcPtr == kfa.il2cpp_codegen_initialize_method ||
            funcPtr == kfa.il2cpp_codegen_initialize_runtime_metadata ||
            funcPtr == kfa.il2cpp_vm_metadatacache_initializemethodmetadata)
            return KnownFunctionReference.IL2CppCodegenInitializeMethod;
            
        if (funcPtr == kfa.il2cpp_runtime_class_init_actual ||
            funcPtr == kfa.il2cpp_runtime_class_init_export)
            return KnownFunctionReference.IL2CppRuntimeClassInit;
            
        if (funcPtr == kfa.il2cpp_codegen_object_new ||
            funcPtr == kfa.il2cpp_object_new ||
            funcPtr == kfa.il2cpp_vm_object_new)
            return KnownFunctionReference.IL2CppObjectNew;
            
        if (funcPtr == kfa.il2cpp_array_new_specific ||
            funcPtr == kfa.il2cpp_vm_array_new_specific ||
            funcPtr == kfa.SzArrayNew)
            return KnownFunctionReference.IL2CppArrayNewSpecific;
            
        if (funcPtr == kfa.il2cpp_type_get_object ||
            funcPtr == kfa.il2cpp_vm_reflection_get_type_object)
            return KnownFunctionReference.IL2CppTypeGetObject;
            
        if (funcPtr == kfa.il2cpp_runtime_class_init_actual ||
            funcPtr == kfa.il2cpp_resolve_icall)
            return KnownFunctionReference.IL2CppResolveIcall;
            
        if (funcPtr == kfa.il2cpp_codegen_string_new_wrapper ||
            funcPtr == kfa.il2cpp_string_new ||
            funcPtr == kfa.il2cpp_string_new_wrapper ||
            funcPtr == kfa.il2cpp_vm_string_new ||
            funcPtr == kfa.il2cpp_vm_string_newWrapper)
            return KnownFunctionReference.IL2CppStringNew;
            
        if (funcPtr == kfa.il2cpp_value_box ||
            funcPtr == kfa.il2cpp_vm_object_box)
            return KnownFunctionReference.IL2CppValueBox;
            
        if (funcPtr == kfa.il2cpp_object_unbox ||
            funcPtr == kfa.il2cpp_vm_object_unbox)
            return KnownFunctionReference.IL2CppObjectUnbox;
            
        if (funcPtr == kfa.il2cpp_codegen_raise_exception ||
            funcPtr == kfa.il2cpp_raise_exception ||
            funcPtr == kfa.il2cpp_vm_exception_raise)
            return KnownFunctionReference.IL2CppRaiseException;
            
        if (funcPtr == kfa.il2cpp_vm_object_is_inst)
            return KnownFunctionReference.IL2CppVmObjectIsInst;
            
        if (funcPtr == kfa.il2cpp_vm_object_is_inst)
            return KnownFunctionReference.AddrPInvokeLookup;
        return new UnmanagedFunctionReference(funcPtr);
    }
    
    private static KnownFunctionReference ResolveKeyFunction(string function)
    {
        if (function == "il2cpp_codegen_initialize_method" ||
            function == "il2cpp_codegen_initialize_runtime_metadata" ||
            function == "il2cpp_vm_metadatacache_initializemethodmetadata")
            return KnownFunctionReference.IL2CppCodegenInitializeMethod;
            
        if (function == "il2cpp_runtime_class_init_actual" ||
            function == "il2cpp_runtime_class_init_export")
            return KnownFunctionReference.IL2CppRuntimeClassInit;
            
        if (function == "il2cpp_codegen_object_new" ||
            function == "il2cpp_object_new" ||
            function == "il2cpp_vm_object_new")
            return KnownFunctionReference.IL2CppObjectNew;
            
        if (function == "il2cpp_array_new_specific" ||
            function == "il2cpp_vm_array_new_specific" ||
            function == "SzArrayNew")
            return KnownFunctionReference.IL2CppArrayNewSpecific;
            
        if (function == "il2cpp_type_get_object" ||
            function == "il2cpp_vm_reflection_get_type_object")
            return KnownFunctionReference.IL2CppTypeGetObject;
            
        if (function == "il2cpp_runtime_class_init_actual" ||
            function == "il2cpp_resolve_icall")
            return KnownFunctionReference.IL2CppResolveIcall;
            
        if (function == "il2cpp_codegen_string_new_wrapper" ||
            function == "il2cpp_string_new" ||
            function == "il2cpp_string_new_wrapper" ||
            function == "il2cpp_vm_string_new" ||
            function == "il2cpp_vm_string_newWrapper")
            return KnownFunctionReference.IL2CppStringNew;
            
        if (function == "il2cpp_value_box" ||
            function == "il2cpp_vm_object_box")
            return KnownFunctionReference.IL2CppValueBox;
            
        if (function == "il2cpp_object_unbox" ||
            function == "il2cpp_vm_object_unbox")
            return KnownFunctionReference.IL2CppObjectUnbox;
            
        if (function == "il2cpp_codegen_raise_exception" ||
            function == "il2cpp_raise_exception" ||
            function == "il2cpp_vm_exception_raise")
            return KnownFunctionReference.IL2CppRaiseException;
            
        if (function == "il2cpp_vm_object_is_inst")
            return KnownFunctionReference.IL2CppVmObjectIsInst;
            
        if (function == "il2cpp_vm_object_is_inst")
            return KnownFunctionReference.AddrPInvokeLookup;
        return new KnownFunctionReference(IL2CppKeyFunction.ErrorLoL, false, false); // bad code
    }
}