using System.CodeDom;
using System.Globalization;
using System.Linq;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using Cpp2ILAdapter.References;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;
using LibCpp2IL.BinaryStructures;
using LibCpp2IL.Metadata;

namespace Cpp2ILAdapter;

public static class IL2CppHelper
{
    public static string GetName(this Il2CppType? type)
    {
        if (type == null)
            return string.Empty;

        return type.Type switch
        {
            Il2CppTypeEnum.IL2CPP_TYPE_I => "nint",
            Il2CppTypeEnum.IL2CPP_TYPE_CLASS or Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE => type.AsClass().Name!,
            Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST => $"{type.CoerceToUnderlyingTypeDefinition().Name}",
            Il2CppTypeEnum.IL2CPP_TYPE_STRING => "string",
            Il2CppTypeEnum.IL2CPP_TYPE_OBJECT => "object",
            Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN => "bool",
            Il2CppTypeEnum.IL2CPP_TYPE_CHAR => "char",
            Il2CppTypeEnum.IL2CPP_TYPE_I1 => "sbyte",
            Il2CppTypeEnum.IL2CPP_TYPE_I2 => "short",
            Il2CppTypeEnum.IL2CPP_TYPE_I4 => "int",
            Il2CppTypeEnum.IL2CPP_TYPE_I8 => "long",
            Il2CppTypeEnum.IL2CPP_TYPE_U => "nuint",
            Il2CppTypeEnum.IL2CPP_TYPE_U1 => "byte",
            Il2CppTypeEnum.IL2CPP_TYPE_U2 => "ushort",
            Il2CppTypeEnum.IL2CPP_TYPE_U4 => "uint",
            Il2CppTypeEnum.IL2CPP_TYPE_U8 => "ulong",
            Il2CppTypeEnum.IL2CPP_TYPE_R4 => "float",
            Il2CppTypeEnum.IL2CPP_TYPE_R8 => "double",
            Il2CppTypeEnum.IL2CPP_TYPE_VOID => "void",
            Il2CppTypeEnum.IL2CPP_TYPE_PTR => $"{type.GetEncapsulatedType().GetName()}*",
            Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY => $"{type.GetEncapsulatedType().GetName()}[]",
            Il2CppTypeEnum.IL2CPP_TYPE_ARRAY => $"idk_how_to_display_array[{new string(',', type.GetArrayRank())}]",
            _ => "UnknownType"
        };
    }

    public static Il2CppTypeDefinition? ToTypeDefinition(this Il2CppType? type)
    {
        if (type == null)
            return null;

        Il2CppTypeDefinition? result;
        
        if (type.Type is Il2CppTypeEnum.IL2CPP_TYPE_CLASS or Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE)
            result = type.AsClass();
        else if (type.Type is Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST or Il2CppTypeEnum.IL2CPP_TYPE_ARRAY or Il2CppTypeEnum.IL2CPP_TYPE_PTR)
            result = type.CoerceToUnderlyingTypeDefinition();
        else if (type.Type is Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY)
            result = type.GetEncapsulatedType().ToTypeDefinition();
        else
            return null;

        return result;
    }

    public static FieldAnalysisContext? TryGetFieldAtOffset(ApplicationAnalysisContext context, object? obj, int offset)
    {
        if (obj == null) return null;
        
        if (obj is Il2CppType type)
        {
            if (type.Type is Il2CppTypeEnum.IL2CPP_TYPE_CLASS or Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE)
            {
                var resolved = type.AsClass();
                return GetFieldAtOffset(context, resolved, offset);
            }
        }
        else if (obj is Il2CppTypeDefinition typeDef)
        {
            return GetFieldAtOffset(context, typeDef, offset);
        }

        return null;
    }

    public static FieldAnalysisContext? GetFieldAtOffset(ApplicationAnalysisContext context, Il2CppTypeDefinition? type, int offset)
    {
        if (type == null)
            return null;
        
        var result = context.ResolveContextForType(type)?.Fields.FirstOrDefault(f => f.Offset == offset);
        if (result != null)
            return result;

        return GetFieldAtOffset(context, type.BaseType?.baseType, offset);
    }

    public static long ToLong(this IConvertible convertible)
    {
        return convertible.GetTypeCode() switch
        {
            TypeCode.Int32 => (int)convertible,
            _ => convertible.ToInt64(CultureInfo.InvariantCulture)
        };
    }

    public static void DispayAttributes(List<AnalyzedCustomAttribute>? customAttributes, IDecompilerOutput write)
    {
        if (customAttributes != null && customAttributes.Count != 0)
        {
            try
            {
                for (var i = 0; i < customAttributes.Count; i++)
                {
                    var attr = customAttributes[i];
                    write.Write("[", BoxedTextColor.Punctuation);
                    write.Write(attr.Constructor.DeclaringType?.Name ?? string.Empty,
                        new Cpp2ILMethodReference(attr.Constructor), DecompilerReferenceFlags.None,
                        BoxedTextColor.Type);
                    var namedArgs = attr.Fields.Count + attr.Properties.Count;
                    if ((attr.HasAnyParameters || namedArgs != 0) && attr.IsSuitableForEmission) // what the fuck
                    {
                        write.Write("(", BoxedTextColor.Punctuation);
                        var parameters = attr.Constructor.Parameters;
                        //write.Write($"/* Parameters: {parameters.Count}; ConstructorParameters: {attr.ConstructorParameters.Count}; Fields: {attr.Fields.Count} */", BoxedTextColor.Comment);
                        for (var index = 0; index < attr.ConstructorParameters.Count; index++)
                        {
                            var parameterInfo = parameters[index];
                            write.Write(parameterInfo.ParameterName, BoxedTextColor.Parameter);
                            write.Write(": ", BoxedTextColor.Punctuation);
                            var parameter = attr.ConstructorParameters[index];
                            DisplayParameter(parameter, write);

                            static void DisplayParameter(BaseCustomAttributeParameter? parameter,
                                IDecompilerOutput write)
                            {
                                if (parameter is CustomAttributeEnumParameter e)
                                    write.Write(e.ToString().Split(' ')[0].Split("::").Last(), BoxedTextColor.Local);
                                else if (parameter is CustomAttributePrimitiveParameter primitive)
                                    write.Write(primitive.ToString(),
                                        primitive.PrimitiveType != Il2CppTypeEnum.IL2CPP_TYPE_STRING
                                            ? BoxedTextColor.Number
                                            : BoxedTextColor.String);
                                else if (parameter is CustomAttributeNullParameter)
                                    write.Write("null", BoxedTextColor.Keyword);
                                else if (parameter is CustomAttributeArrayParameter arrayParameter)
                                {
                                    write.Write("[", BoxedTextColor.Punctuation);
                                    foreach (var baseCustomAttributeParameter in arrayParameter.ArrayElements)
                                    {
                                        DisplayParameter(baseCustomAttributeParameter, write);
                                    }

                                    write.Write("]", BoxedTextColor.Punctuation);
                                }
                                else if (parameter is CustomAttributeTypeParameter typeParameter)
                                {
                                    write.Write("typeof", BoxedTextColor.Keyword);
                                    write.Write("(", BoxedTextColor.Punctuation);
                                    write.Write(typeParameter.Type.GetName(), new Cpp2ILTypeDefReference(typeParameter.Type.ToTypeDefinition()), DecompilerReferenceFlags.None, BoxedTextColor.Type);
                                    write.Write(")", BoxedTextColor.Punctuation);
                                }
                                else
                                    write.Write(parameter?.ToString() ?? string.Empty, BoxedTextColor.Local);
                            }

                            if (index != attr.ConstructorParameters.Count - 1 || attr.Fields.Count != 0 || attr.Properties.Count != 0)
                                write.Write(", ", BoxedTextColor.Punctuation);
                        }

                        for (var index = 0; index < attr.Fields.Count; index++)
                        {
                            var customAttributeField = attr.Fields[index];
                            write.Write(customAttributeField.Field.Name,
                                new Cpp2ILFieldReference(customAttributeField.Field), DecompilerReferenceFlags.None,
                                BoxedTextColor.InstanceProperty);
                            write.Write(" = ", BoxedTextColor.Punctuation);
                            write.Write(customAttributeField.Value.ToString() ?? string.Empty, BoxedTextColor.Local);
                            if (index != attr.Fields.Count - 1 && attr.Properties.Count != 0)
                                write.Write(",", BoxedTextColor.Punctuation);
                        }

                        for (var index = 0; index < attr.Properties.Count; index++)
                        {
                            var customAttributeProperty = attr.Properties[index];
                            write.Write(customAttributeProperty.Property.Name,
                                new Cpp2ILMethodReference(customAttributeProperty.Property.Setter!),
                                DecompilerReferenceFlags.None, BoxedTextColor.InstanceProperty);
                            write.Write(" = ", BoxedTextColor.Punctuation);
                            write.Write(customAttributeProperty.Value.ToString() ?? string.Empty, BoxedTextColor.Local);
                            if (index != attr.Properties.Count - 1)
                                write.Write(",", BoxedTextColor.Punctuation);
                        }

                        write.Write(")", BoxedTextColor.Punctuation);
                    }

                    write.WriteLine("]", BoxedTextColor.Punctuation);
                }
            }
            catch (Exception e)
            {
                write.WriteLine($"/* Exception! {e} */", BoxedTextColor.Comment);
            }
        }
    }
}