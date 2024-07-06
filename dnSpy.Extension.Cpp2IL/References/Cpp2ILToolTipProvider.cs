using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using Cpp2ILAdapter.PseudoC;
using dnSpy.Contracts.Documents.Tabs.DocViewer.ToolTips;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Text;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter.References;

[ExportDocumentViewerToolTipProvider]
sealed class Cpp2ILToolTipProvider : IDocumentViewerToolTipProvider
{
    public object? Create(IDocumentViewerToolTipProviderContext context, object? @ref)
    {
        switch (@ref)
        {
            case Cpp2ILMethodReference methodReference:
            {
                var method = methodReference.Method;
                var toolTipProvider = context.Create();
                toolTipProvider.Image = DsImages.MethodPublic;
                DisplayMethod(method, toolTipProvider.Output);
                return toolTipProvider.Create();
            }
            case Cpp2ILFieldReference fieldReference:
            {
                var field = fieldReference.Field;
                var toolTipProvider = context.Create();
                toolTipProvider.Image = DsImages.MethodPublic;
                DisplayField(field, toolTipProvider.Output);
                return toolTipProvider.Create();
            }
            case Cpp2IlVariableReference variableReference:
            {
                var variable = variableReference.Variable;
                var toolTipProvider = context.Create();
                toolTipProvider.Image = DsImages.MethodPublic;
                DisplayVariable(variable, toolTipProvider.Output);
                return toolTipProvider.Create();
            }
            default:
                return null;
        }
    }

    private static void DisplayMethod(MethodAnalysisContext context, ICodeToolTipWriter write)
    {
        var def = context.Definition;
        if (def == null)
            return;
        write.Write(TextColor.Keyword, def.Attributes.HasFlag(MethodAttributes.Public) ? "public " : "private ");
        write.Write(TextColor.Keyword, def.IsStatic ? "static " : string.Empty);
        if (def.RawReturnType?.Type != Il2CppTypeEnum.IL2CPP_TYPE_VOID)
            write.Write(TextColor.Type, def.ReturnType?.ToString() ?? string.Empty);
        else
            write.Write(TextColor.Keyword, "void");
        write.Write(TextColor.Local, " ");
        write.Write(def.IsStatic ? TextColor.StaticMethod : TextColor.InstanceMethod, def.Name ?? string.Empty);
        write.Write(TextColor.Local, "(");
        if (def.Parameters != null)
        {
            bool first = true;
            foreach (var parameter in def.Parameters)
            {
                if (!first)
                    write.Write(TextColor.Local, ", ");
                write.Write(TextColor.Type, parameter.Type.ToString());
                write.Write(TextColor.Local, " ");
                write.Write(TextColor.Local, parameter.ParameterName);
                first = false;
            }
        }
        write.Write(TextColor.Local, ") ");
        write.Write(TextColor.White, "\nFrom ");
        write.Write(TextColor.Type, context.DeclaringType?.FullName);
    }

    private static void DisplayField(FieldAnalysisContext context, ICodeToolTipWriter write)
    {
        write.Write(TextColor.Type, context.FieldType.GetName());
        write.Write(TextColor.Punctuation, " ");
        write.Write(TextColor.Type, context.DeclaringType.FullName);
        write.Write(TextColor.Punctuation, "::");
        write.Write(TextColor.InstanceField, context.Name);
    }
    
    private static void DisplayVariable(Variable variable, ICodeToolTipWriter write)
    {
        write.Write(TextColor.Type, variable.Type switch
        {
            Il2CppType type => type.GetName(),
            Il2CppTypeDefinition typeDef => typeDef.Name,
            TypeAnalysisContext context => context.Name,
            _ => "Unknown"
        });
        write.Write(TextColor.Punctuation, " ");
        write.Write(TextColor.Local, variable.Name);
    }
}