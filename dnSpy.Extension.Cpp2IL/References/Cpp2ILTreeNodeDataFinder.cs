using System.Linq;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.TreeView;
using dnSpy.Contracts.Documents.TreeView;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;
using FieldNode = Cpp2ILAdapter.TreeView.FieldNode;
using MethodNode = Cpp2ILAdapter.TreeView.MethodNode;
using NamespaceNode = Cpp2ILAdapter.TreeView.NamespaceNode;
using TypeNode = Cpp2ILAdapter.TreeView.TypeNode;

namespace Cpp2ILAdapter.References;

[ExportDocumentTreeNodeDataFinder]
sealed class Cpp2ILTreeNodeDataFinder : IDocumentTreeNodeDataFinder
{
    public DocumentTreeNodeData? FindNode(IDocumentTreeView documentTreeView, object? @ref)
    {
        if (@ref is not Cpp2ILReference)
            return null;
        
        var selectedItem = documentTreeView.TreeView.SelectedItem;

        if (selectedItem.GetTopNode() is not Cpp2ILDocumentNode documentNode)
            return null;
        
        documentNode.TreeNode.EnsureChildrenLoaded();
        
        switch (@ref)
        {
            case Cpp2ILTypeDefReference typeDefReference:
            {
                if (typeDefReference.Type == null)
                    return null;

                return TypeSearch(null, typeDefReference.Type, documentNode);
            }
            case Cpp2ILTypeReference typeReference:
            {
                if (typeReference.Type == null)
                    return null;
                
                return TypeSearch(typeReference.Type, null, documentNode);
            }
            case Cpp2ILMethodReference methodReference:
            {
                var method = methodReference.Method;
                var typeNode = documentNode.SearchType(method.DeclaringType!);
                if (typeNode == null)
                    return null;
                return typeNode.TreeNode.DataChildren
                    .FirstOrDefault(c => c is MethodNode node && node.Context.UnderlyingPointer == method.UnderlyingPointer) as MethodNode;
            }
            case Cpp2ILMethodReferenceFromRef methodReference2:
            {
                var method = methodReference2.Method;
                var decType = method.DeclaringType;
                var ty = documentNode.IlDocument.Context.AllTypes.First(_ => _.TypeNamespace == decType.Namespace && _.Name == decType.Name);
                var typeNode = documentNode.SearchType(ty);
                if (typeNode == null)
                    return null;
                return typeNode.TreeNode.DataChildren
                    .FirstOrDefault(c => c is MethodNode node && node.Context.UnderlyingPointer == method.BaseMethod.MethodPointer) as MethodNode;
            }
            case Cpp2ILFieldReference fieldReference:
            {
                var field = fieldReference.Field;
                var typeNode = documentNode.SearchType(field.DeclaringType);
                if (typeNode == null)
                    return null;
                return typeNode.TreeNode.DataChildren
                    .FirstOrDefault(c => c is FieldNode node && node.Context.Name == field.Name) as FieldNode;
            }
            default:
                return null;
        }
    }

    private static TypeNode? TypeSearch(Il2CppType? typeReference, Il2CppTypeDefinition? typeDef, Cpp2ILDocumentNode documentNode)
    {
        if (typeReference == null && typeDef == null)
            return null;
        
        Il2CppTypeDefinition? type = typeDef ?? typeReference.ToTypeDefinition();
        string? typeNamespace;
        string? typeName;

        if (type != null)
        {
            typeNamespace = type.Namespace!;
            typeName = type.Name!;
        }
        else
        {
            (typeNamespace, typeName) = typeReference?.Type switch
            {
                Il2CppTypeEnum.IL2CPP_TYPE_OBJECT => ("System", "Object"),
                Il2CppTypeEnum.IL2CPP_TYPE_STRING => ("System", "String"),
                Il2CppTypeEnum.IL2CPP_TYPE_CHAR => ("System", "Char"),
                Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN => ("System", "Boolean"),
                Il2CppTypeEnum.IL2CPP_TYPE_I => ("System", "IntPtr"),
                Il2CppTypeEnum.IL2CPP_TYPE_I1 => ("System", "SByte"),
                Il2CppTypeEnum.IL2CPP_TYPE_I2 => ("System", "Int16"),
                Il2CppTypeEnum.IL2CPP_TYPE_I4 => ("System", "Int32"),
                Il2CppTypeEnum.IL2CPP_TYPE_I8 => ("System", "Int64"),
                Il2CppTypeEnum.IL2CPP_TYPE_U => ("System", "UIntPtr"),
                Il2CppTypeEnum.IL2CPP_TYPE_U1 => ("System", "Byte"),
                Il2CppTypeEnum.IL2CPP_TYPE_U2 => ("System", "UInt16"),
                Il2CppTypeEnum.IL2CPP_TYPE_U4 => ("System", "UInt32"),
                Il2CppTypeEnum.IL2CPP_TYPE_U8 => ("System", "UInt64"),
                Il2CppTypeEnum.IL2CPP_TYPE_R4 => ("System", "Single"),
                Il2CppTypeEnum.IL2CPP_TYPE_R8 => ("System", "Double"),
                _ => (null, null)
            };
        }
                
        if (type != null || typeName != null)
        {
            var namespaceNodes = documentNode
                .TreeNode
                .DataChildren
                .Cast<AssemblyNode>()
                .SelectMany(static assembly =>
                {
                    assembly.TreeNode.EnsureChildrenLoaded();
                    return assembly.TreeNode.DataChildren.Cast<NamespaceNode>();
                })
                .Where(ns =>
                {
                    ns.TreeNode.EnsureChildrenLoaded();
                    return ns.Types[0].Namespace == typeNamespace;
                });
                    
            foreach (var namespaceNode in namespaceNodes)
            {
                var types = namespaceNode.TreeNode.DataChildren.Cast<TypeNode>();
                var result = RecursiveTypeSearch(types, type, typeName);
                if (result != null)
                    return result;
            }
        }

        return null;
    }

    private static TypeNode? RecursiveTypeSearch(IEnumerable<TypeNode> nodes, Il2CppTypeDefinition? type, string? name)
    {
        foreach (var typeNode in nodes)
        {
            var def = typeNode.Context.Definition;
            if (type != null ? def == type : def!.Name == name)
                return typeNode;
            typeNode.TreeNode.EnsureChildrenLoaded();
            var nested = typeNode.GetTreeNodeData.Where(treeNodeData => treeNodeData is TypeNode).Cast<TypeNode>();
            var nestedResult = RecursiveTypeSearch(nested, type, name);
            if (nestedResult != null)
                return nestedResult;
        }

        return null;
    }
}