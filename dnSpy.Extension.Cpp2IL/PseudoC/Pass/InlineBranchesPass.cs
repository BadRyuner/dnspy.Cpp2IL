using System.Linq;

namespace Cpp2ILAdapter.PseudoC.Pass;

public class InlineBranchesPass
{
    public List<Block> Process(List<Block> blocks)
    {
        List<Block> eaten = [];

        var branchBlocks = blocks.Where(b => b.Successors.Count == 2).ToArray();
        foreach (var branchBlock in branchBlocks)
        {
            var branch = branchBlock.ToEmit[^1];
            if (branch is IfExpression ifExpression)
            {
                var gotoExpression = ifExpression.Body as GotoExpression;
                if (gotoExpression == null) continue;
                var instructionReference = gotoExpression.Value as InstructionReference;
                if (instructionReference == null) continue;
                var targetBlock = blocks.FirstOrDefault(b => b.StartIsilIndex == instructionReference.InstructionIndex);
                if (targetBlock == null) continue;
                var alternative = branchBlock.Successors.First(b => b.Id != targetBlock.Id);
                if (targetBlock.Predecessors.Count == 1)
                {
                    ifExpression.Body = targetBlock;
                    eaten.Add(targetBlock);
                }
                else if (alternative.Predecessors.Count == 1)
                {
                    ifExpression.Condition = new NotExpression(ifExpression.Condition);
                    ifExpression.Body = alternative;
                    eaten.Add(alternative);
                }
            }
        }
        
        //AcceptBlocks(blocks);
        
        return blocks.Where(b => !eaten.Contains(b)).ToList();
    }
}