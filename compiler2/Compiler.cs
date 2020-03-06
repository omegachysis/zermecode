#nullable enable
using System.IO;

namespace compiler2
{
    public class Compiler
    {
        public void Write(ast.Program ast, StreamWriter stream)
        {
            stream.WriteLine("#include <iostream>");
            stream.WriteLine("int main() { std::cout << \"Hello\"; return 0; }");
        }
    }
}