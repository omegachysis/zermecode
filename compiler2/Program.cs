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
            ast.Program ast;
            using (var inStream = new StreamReader("input.txt"))
            {
                // var lex = new Lexer(inStream);
                // var tokens = lex.Tokens().GetEnumerator();
                // while (tokens.MoveNext())
                //     Console.WriteLine(tokens.Current);
                // Console.WriteLine();

                // inStream.BaseStream.Seek(0, SeekOrigin.Begin);
                var lex = new Lexer(inStream);
                var tokens = lex.Tokens().GetEnumerator();
                var parser = new Parser();
                ast = parser.Parse(tokens);
            }

            ast.Show();
            Console.WriteLine();

            var compiler = new Compiler();

            using (var outStream = new StreamWriter("bin/ir.cpp"))
                compiler.Write(ast, outStream);
        }
    }
}
