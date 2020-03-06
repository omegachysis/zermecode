#nullable enable
using System.IO;

namespace compiler2
{
    public class Compiler
    {
        private Lexer _lex;
        private StreamWriter _outStream;

        public Compiler(StreamReader inStream, StreamWriter outStream)
        {
            _lex = new Lexer(inStream);
            _outStream = outStream;
        }
    }
}