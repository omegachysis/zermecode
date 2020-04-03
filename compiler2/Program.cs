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

                try
                {
                    ast = parser.Parse(tokens);
                }
                catch (ParseError)
                {
                    Console.WriteLine("Error parsing the program:");
                    throw;
                }

                sw.Stop();

                Console.WriteLine($"Parsed: {sw.ElapsedMilliseconds} ms");
            }

            ast.Show();
            Console.WriteLine();

            Console.WriteLine("Transpiling...");
            var compiler = new Compiler();
            var sw1 = Stopwatch.StartNew();
            using (var outStream = new StreamWriter("bin/ir.cpp"))
            {
                try
                {
                    compiler.Write(ast, outStream);
                }
                catch (CompileError)
                {
                    outStream.Close();
                    File.Delete("bin/ir.cpp");
                    Console.WriteLine("Error compiling the program:");
                    throw;
                }
            }
            sw1.Stop();
            Console.WriteLine($"Transpiled: {sw1.ElapsedMilliseconds} ms");
        }
    }
}
