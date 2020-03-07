#nullable enable
using System;
using System.Diagnostics;
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
                Console.WriteLine("Loading...");
                var lex = new Lexer(inStream);
                var tokens = lex.Tokens().GetEnumerator();
                var parser = new Parser();

                Console.WriteLine("Parsing...");
                var sw = Stopwatch.StartNew();
                ast = parser.Parse(tokens);
                sw.Stop();

                Console.WriteLine($"Parsed: {sw.ElapsedMilliseconds} ms");
            }

            ast.Show();
            Console.WriteLine();

            var compiler = new Compiler();

            using (var outStream = new StreamWriter("bin/ir.cpp"))
                compiler.Write(ast, outStream);
        }
    }
}
