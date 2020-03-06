#nullable enable
using System;
using System.IO;
using System.Text;

namespace compiler2
{
    class Program
    {
        static void Main(string[] args)
        {
            // using (var file = new StreamWriter("bin/ir.c"))
            // {
            //     file.WriteLine("#include <stdio.h>");
            //     file.Write("int main() {");
            //     file.Write("printf(\"Hello, this is me!\");");
            //     file.Write("return 0;");
            //     file.Write("}");
            // }

            ast.Program ast;
            using (var inStream = new StreamReader("input.txt"))
            {
                var lex = new Lexer(inStream);
                var tokens = lex.Tokens().GetEnumerator();
                while (tokens.MoveNext())
                    Console.WriteLine(tokens.Current);
                Console.WriteLine();

                inStream.BaseStream.Seek(0, SeekOrigin.Begin);
                lex = new Lexer(inStream);
                tokens = lex.Tokens().GetEnumerator();
                var parser = new Parser();
                ast = parser.Parse(tokens);
            }

            ast.Show();
            Console.WriteLine();

            var compiler = new Compiler();

            using (var outStream = new StreamWriter("bin/ir.c"))
                compiler.Write(ast, outStream);
        }
    }
}
