using System.Linq;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.IsilEcho;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.PseudoC.Passes;
using Cpp2ILAdapter.TreeView;
using Echo.ControlFlow.Construction.Static;
using Echo.ControlFlow.Serialization.Blocks;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter;

public static class IsilLifter
{
    public static List<EmitBlock> Lift(MethodAnalysisContext context, Cpp2ILDocument document)
    {
        try
        {
            if (context.ConvertedIsil == null)
                context.Analyze();

            var arch = new IsilArchitecture(document);
            var transitioner = new IsilStaticSuccessorResolver(context.ConvertedIsil!);
            var builder =
                new StaticFlowGraphBuilder<InstructionSetIndependentInstruction>(arch, context.ConvertedIsil,
                    transitioner);
            var cfg = builder.ConstructFlowGraph(1, Array.Empty<long>());

            var blocks = cfg.ConstructBlocks().GetAllBlocks();
            var result = new List<EmitBlock>();
            IEmit? previous = new Expression(ExpressionKind.Nop);
            foreach (var basicBlock in blocks)
            {
                var emitBlock = new EmitBlock(basicBlock.Header.InstructionIndex);
                for (var i = 0; i < basicBlock.Instructions.Count; i++)
                {
                    var instruction = basicBlock.Instructions[i];
                    var transformed = Transform(ref previous, instruction, context);
                    if (previous != null)
                        emitBlock.Add(transformed);
                    else
                        emitBlock.Items[^1] = transformed;
                    previous = transformed;
                }

                result.Add(emitBlock);
            }

            //new EmitBlockLinker().Start(result, context);
            new RenameRegisters(context.AppContext.InstructionSet is X86InstructionSet).Start(result, context);
            new CreateVariables().Start(result, context);
            var dataFlowAnalysis = new DataFlowAnalysis();
            dataFlowAnalysis.Start(result, context);
            new MetadataInliner().Start(result, context);
            new StringAnalysis().Start(result, context);
            new ExpressionInliner().Start(result, context);
            
            return result.OrderBy(_ => _.Index).ToList();
        }
        catch(Exception e)
        {
            return new List<EmitBlock>()
            {
                new EmitBlock(0)
                {
                    Items =
                    {
                        new Unsupported(e.ToString())
                    }
                }
            };
        }
    }

    private static IEmit Transform(ref IEmit? previous, in InstructionSetIndependentInstruction instruction, MethodAnalysisContext context)
    {
        var operands = instruction.Operands;
        switch (instruction.OpCode.Mnemonic)
        {
            case IsilMnemonic.Move:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), TransformOperand(operands[1]), instruction.InstructionIndex);
            case IsilMnemonic.And:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.And, TransformOperand(operands[1]), TransformOperand(operands[2]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.Or:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Or, TransformOperand(operands[1]), TransformOperand(operands[2]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.Xor:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Xor, TransformOperand(operands[1]), TransformOperand(operands[2]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.Compare:
                return new Expression(ExpressionKind.Compare, TransformOperand(operands[0]), TransformOperand(operands[1]), instruction.InstructionIndex);
            case IsilMnemonic.Add:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Add, TransformOperand(operands[1]), TransformOperand(operands[2]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.Subtract:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Sub, TransformOperand(operands[1]), TransformOperand(operands[2]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.Multiply:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Mul, TransformOperand(operands[1]), TransformOperand(operands[2]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.Divide:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Div, TransformOperand(operands[1]), TransformOperand(operands[2]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.ShiftLeft:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Shl, TransformOperand(operands[0]), TransformOperand(operands[1]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.ShiftRight:
                return new Expression(ExpressionKind.Assign, TransformOperand(operands[0]), new Expression(ExpressionKind.Shr, TransformOperand(operands[0]), TransformOperand(operands[1]), instruction.InstructionIndex), instruction.InstructionIndex);
            case IsilMnemonic.Return:
                if (context.Definition?.RawReturnType?.Type == Il2CppTypeEnum.IL2CPP_TYPE_VOID)
                    return new Expression(ExpressionKind.Return);
                return new Expression(ExpressionKind.Return, TransformOperand(operands[0]), Index: instruction.InstructionIndex);
            case IsilMnemonic.Goto:
                return new Expression(ExpressionKind.Goto, new InstructionReference(((InstructionSetIndependentInstruction)operands[0].Data).InstructionIndex), Index: instruction.InstructionIndex);

            case IsilMnemonic.JumpIfEqual:
            case IsilMnemonic.JumpIfNotEqual:
            case IsilMnemonic.JumpIfGreater:
            case IsilMnemonic.JumpIfGreaterOrEqual:
            case IsilMnemonic.JumpIfLessOrEqual:
            case IsilMnemonic.JumpIfLess:
            {
                var result = new Expression(ExpressionKind.If, previous, new Expression(ExpressionKind.Goto, TransformOperand(operands[0]), Index: instruction.InstructionIndex), Index: instruction.InstructionIndex)
                    .FixIf(instruction.OpCode.Mnemonic);
                previous = null;
                return result;
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
                    returnsFast = false;
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

                IEmit call = new Expression(ExpressionKind.Call, function, new InlineEmitBlock(", ") { Items = args.ToList() }, instruction.InstructionIndex);
                if (returnsFast)
                    call = new Expression(ExpressionKind.Return, call, Index: instruction.InstructionIndex);
                else if (returns)
                    call = new Expression(ExpressionKind.Assign, GetReturnRegister(context), call, instruction.InstructionIndex);
                return call;
            }

            case IsilMnemonic.Not:
                return new Expression(ExpressionKind.Assign, TransformOperand(instruction.Operands[0]), new Expression(ExpressionKind.Not, TransformOperand(instruction.Operands[0]), Index: instruction.InstructionIndex), instruction.InstructionIndex);

            case IsilMnemonic.LoadAddress:
            {
                var expr = TransformOperand(instruction.Operands[1]);
                if (expr is Expression { Kind: ExpressionKind.Deref, Left: var e })
                    expr = e;
                return new Expression(ExpressionKind.Assign, TransformOperand(instruction.Operands[0]), expr);
            }
            
            case IsilMnemonic.Nop:
                return Expression.NopShared;
                
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
                if (operand.Data is IsilRegisterOperand reg)
                    return new Register(reg.RegisterName);
                else if (operand.Data is IsilVectorRegisterElementOperand vector)
                    return new VectorAccess(new Register(vector.RegisterName), new Immediate(vector.Index));
                throw new NotImplementedException(operand.Data.GetType().ToString());
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
                        what = new Expression(ExpressionKind.Add, what, c);
                    else
                        what = c;
                }
                if (isilMemoryOperand.Index != null)
                {
                    var index = TransformOperand(isilMemoryOperand.Index.Value);
                    if (what == null)
                        what = index;
                    else
                        what = new Expression(ExpressionKind.Add,what, index);

                    if (isilMemoryOperand.Scale > 1)
                        what = new Expression(ExpressionKind.Mul, what, new Immediate(isilMemoryOperand.Scale));
                }
                return new Expression(ExpressionKind.Deref, what);
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
        return new KnownFunctionReference(IL2CppKeyFunction.Error);
    }
}