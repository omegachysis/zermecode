#nullable enable
using System;
using System.IO;
using System.Text;

namespace compiler3
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var input = new StreamReader(File.OpenRead("input.zrm")))
            {
                var lexer = new Lexer(input);
                foreach (var token in lexer.Lex())
                {
                    Console.WriteLine(token);
                }
            }
        }
    }
}
