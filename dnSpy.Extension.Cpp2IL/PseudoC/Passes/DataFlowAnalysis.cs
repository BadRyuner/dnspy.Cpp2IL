using System.Globalization;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.BinaryStructures;

namespace Cpp2ILAdapter.PseudoC.Passes;

public class DataFlowAnalysis : BasePass
{
    private bool _success;
    private ApplicationAnalysisContext _context = null!;
    
    public override void Start(List<EmitBlock> blocks, MethodAnalysisContext context)
    {
        _context = context.AppContext;
        _success = true;
        while (_success)
        {
            _success = false;
            base.Start(blocks, context);
        }
    }

    public override void AcceptExpression(ref Expression expression)
    {
        if (expression is
            {
                Kind: ExpressionKind.Deref,
                Left: Expression { Kind: ExpressionKind.Add, Left: Variable variable, Right: Immediate offset }
            })
        {
            if (variable.Name == "Stack") return;
            
            var field = IL2CppHelper.TryGetFieldAtOffset(_context, variable.Type, offset.Value.ToInt32(CultureInfo.InvariantCulture));
            if (field != null)
            {
                expression.Kind = ExpressionKind.MemberAccess;
                expression.Left = variable;
                expression.Right = new AccessField(field);
                _success = true;
            }

            return;
        }

        if (expression is
            {
                Kind: ExpressionKind.Assign, 
                Left: Variable assignVar,
                Right: Expression { Kind: ExpressionKind.MemberAccess, Right: AccessField { Field: var accessField } }
            })
        {
            if (assignVar.Type != accessField.FieldType)
            {
                assignVar.Type = accessField.FieldType;
                _success = true;
            }
        }

        if (expression is
            {
                Kind: ExpressionKind.Assign,
                Left: Variable retValue,
                Right: Expression
                {
                    Kind: ExpressionKind.Call,
                    Left: ManagedFunctionReference { } method
                }
            })
        {
            if (retValue.Type == null && method.Method.Definition is { RawReturnType: Il2CppType { } retType })
            {
                retValue.Type = retType;
                _success = true;
            }
        }
    }

    public override void AcceptBlock(Block block)
    {
        
    }
}