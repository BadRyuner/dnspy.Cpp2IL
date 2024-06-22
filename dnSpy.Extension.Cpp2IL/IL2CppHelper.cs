using System.CodeDom;
using System.Globalization;
using System.Linq;
using Cpp2IL.Core.Model.Contexts;
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
}