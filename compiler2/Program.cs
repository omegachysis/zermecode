#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace compiler2
{
    class Program
    {
        static void Main(string[] args)
        {
            ast.Program ast;
            using (var inStream = new StreamReader("input.zrm"))
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

            Console.WriteLine("Transpiling...");
            var compiler = new Compiler();
            bool success;
            var sw1 = Stopwatch.StartNew();
            using (var outStream = new StreamWriter("bin/ir.cpp"))
                success = compiler.Write(ast, outStream);
            sw1.Stop();

            Console.WriteLine($"Transpiled: {sw1.ElapsedMilliseconds} ms");

            if (!success)
                File.Delete("bin/ir.cpp");
        }
    }
}
