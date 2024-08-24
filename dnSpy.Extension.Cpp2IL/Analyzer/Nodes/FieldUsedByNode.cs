using System.Linq;
using System.Threading.Tasks;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.PseudoC.Passes;
using Cpp2ILAdapter.TreeView;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class FieldUsedByNode(TreeView.FieldNode field) : SearchNode
{
    public static readonly Guid GUID = new Guid("6EA8F293-8579-4EC7-BE73-C1A272A15F16");
    public override Guid Guid => GUID;
    public override object? Text => "Used by";

    public override IEnumerable<TreeNodeData> CreateChildren()
    {
        var allTypes = Cpp2ILDocumentNode.CurrentInstance?.AllTypes;
        if (allTypes == null)
            return Array.Empty<TreeNodeData>();
        
        Cpp2ILDocumentNode.CurrentInstance!.CheckIsAnalyzed();
        var result = new List<TreeNodeData>(4);
        var key = new object();
        Parallel.ForEach(allTypes, (typeNode) =>
        {
            var pass = new AnalyzerPass(field);
            foreach (var methodNode in typeNode.GetTreeNodeData.OfType<TreeView.MethodNode>())
            {
                pass.Found = false;
                foreach (var emitBlock in methodNode.Lifted.Value)
                {
                    if (pass.Found) break;
                    emitBlock.AcceptPass(pass);
                }

                if (pass.Found)
                    lock(key)
                        result.Add(new MethodNode(methodNode));
            }
        });
        return result;
    }
    
    sealed class AnalyzerPass(TreeView.FieldNode field) : BasePass
    {
        public bool Found = false;
        public override void AcceptExpression(ref Expression expression)
        {
            if (Found) return;
            if (expression is Expression { Right: AccessField reference }
                && reference.Field == field.Context)
                Found = true;
            else if (expression is Expression { Left: AccessField reference2 }
                     && reference2.Field == field.Context)
                Found = true;
        }

        public override void AcceptBlock(Block block)
        {
        }
    }
}