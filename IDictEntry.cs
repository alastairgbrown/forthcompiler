using System.Reflection;

namespace ForthCompiler
{
    public interface IDictEntry
    {
        MethodInfo Method { get; }

        TokenType TokenType { get; }
    }
}