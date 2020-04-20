#nullable enable

using System;
using System.IO;

namespace compiler3
{
    public class Lexer
    {
        private readonly StreamReader _input;

        public Lexer(StreamReader input)
        {
            _input = input;
        }


    }
}