using System.Collections.Frozen;
using System.Linq;
using System.Threading.Tasks;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.PseudoC;
using Cpp2ILAdapter.PseudoC.Passes;
using Cpp2ILAdapter.TreeView;
using dnSpy.Contracts.TreeView;

namespace Cpp2ILAdapter.Analyzer.Nodes;

public class MethodUsedByNode(TreeView.MethodNode method) : SearchNode
{
    public static readonly Guid GUID = new Guid("D1E0CAD0-FB1E-4AD8-B45D-B032C2020DF3");
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
            var pass = new AnalyzerPass(method);
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
    
    sealed class AnalyzerPass(TreeView.MethodNode method) : BasePass
    {
        public bool Found = false;
        public override void AcceptExpression(ref Expression expression)
        {
            if (Found) return;
            if (expression is Expression { Kind: ExpressionKind.Call, Left: ManagedFunctionReference reference }
                && reference.Method == method.Context)
                Found = true;
        }

        public override void AcceptBlock(Block block)
        {
        }
    }
}