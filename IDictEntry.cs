namespace ForthCompiler
{
    public interface IDictEntry
    {
        void Process(Compiler compiler);

        TokenType TokenType { get; }
    }
}