namespace Cpp2ILAdapter.PseudoC.Pass;

public abstract class BasePass
{
    private IEmit _currentEmit;
    
    public virtual void AcceptBlocks(List<Block> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            AcceptBlock(blocks[i]);
        }
    }

    protected virtual void AcceptBlock(Block block)
    {
        for (var index = 0; index < block.ToEmit.Count; index++)
        {
            AcceptSingleIEmit(block.ToEmit[index]);
        }
    }

    protected virtual void AcceptSingleIEmit(IEmit emit)
    {
        if (emit is Block block)
        {
            AcceptBlock(block);
            return;    
        }
        
        if (emit is not Expression and not Nop) 
            return; // what the fuck
            
        _currentEmit = emit;
            
        switch (emit)
        {
            case AssignExpression assignExpression:
                VisitAssignExpression(assignExpression);
                break;
            case CallExpression callExpression:
                VisitCallExpression(callExpression);
                break;
            case CompareExpression compareExpression:
                VisitCompareExpression(compareExpression);
                break;
            case DerefExpression derefExpression:
                VisitDerefExpression(derefExpression);
                break;
            case IfElseExpression ifElseExpression:
                VisitIfElseExpression(ifElseExpression);
                break;
            case IfExpression ifExpression:
                VisitIfExpression(ifExpression);
                break;
            case MathExpression mathExpression:
                VisitMathExpression(mathExpression);
                break;
            case NotExpression notExpression:
                VisitNotExpression(notExpression);
                break;
            case ReturnExpression returnExpression:
                VisitReturnExpression(returnExpression);
                break;
            case VectorAccessExpression vectorAccessExpression:
                VisitVectorAccessExpression(vectorAccessExpression);
                break;
            case WhileExpression whileExpression:
                VisitWhileExpression(whileExpression);
                break;
            case GotoExpression gotoExpression:
                VisitGotoExpression(gotoExpression);
                break;
            case Nop:
                break;
            default:
                throw new Exception($"Oh shit what about this -> {emit.GetType().Name}");
        }
    }
    
    protected virtual void VisitGotoExpression(GotoExpression gotoExpression)
    {
        
    }

    protected virtual void VisitAssignExpression(AssignExpression expression)
    {
        AcceptSingleIEmit(expression.Target);
        AcceptSingleIEmit(expression.Value);
    }
    
    protected virtual void VisitCallExpression(CallExpression expression)
    {
        AcceptSingleIEmit(expression.Method);
        for (var i = 0; i < expression.Arguments.Length; i++)
        {
            AcceptSingleIEmit(expression.Arguments[i]);
        }
    }
    
    protected virtual void VisitCompareExpression(CompareExpression expression)
    {
        AcceptSingleIEmit(expression.Left);
        AcceptSingleIEmit(expression.Right);
    }
    
    protected virtual void VisitDerefExpression(DerefExpression expression)
    {
        AcceptSingleIEmit(expression.Value);
    }
    
    protected virtual void VisitIfElseExpression(IfElseExpression expression)
    {
        AcceptSingleIEmit(expression.Condition);
        AcceptSingleIEmit(expression.If);
        AcceptSingleIEmit(expression.Else);
    }

    protected virtual void VisitIfExpression(IfExpression expression)
    {
        AcceptSingleIEmit(expression.Condition);
        AcceptSingleIEmit(expression.Body);
    }
    
    protected virtual void VisitMathExpression(MathExpression expression)
    {
        AcceptSingleIEmit(expression.Right);
        AcceptSingleIEmit(expression.Left);
    }
    
    protected virtual void VisitNotExpression(NotExpression expression)
    {
        AcceptSingleIEmit(expression.Value);
    }
    
    protected virtual void VisitReturnExpression(ReturnExpression expression)
    {
        if (expression.Value != null)
            AcceptSingleIEmit(expression.Value);
    }
    
    protected virtual void VisitVectorAccessExpression(VectorAccessExpression expression)
    {
        AcceptSingleIEmit(expression.Vector);
        AcceptSingleIEmit(expression.Index);
    }
    
    protected virtual void VisitWhileExpression(WhileExpression expression)
    {
        AcceptSingleIEmit(expression.Condition);
        AcceptSingleIEmit(expression.Body);
    }
}