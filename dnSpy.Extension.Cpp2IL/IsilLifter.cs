using Cpp2IL.Core.Model.Contexts;

namespace Cpp2ILAdapter;

public static class IsilLifter
{
    private static readonly string Add = "+";
    private static readonly string Sub = "-";
    private static readonly string Div = "/";
    private static readonly string Mul = "*";
    private static readonly string Shl = "<<";
    private static readonly string Shr = ">>";
    private static readonly string And = "&";
    private static readonly string Or = "|";
    private static readonly string Xor = "^";
    private static readonly string Cmp = "==";

    private static readonly string IncSet = "+=";
    
    public static List<object> Lift(MethodAnalysisContext context)
    {
        var list = new List<object>(8);
        return list;
    }
}