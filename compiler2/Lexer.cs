#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace compiler2
{
    public class Lexer
    {
        private StreamReader _stream;

        public Lexer(StreamReader stream)
        {
            _stream = stream;
        }

        public IEnumerable<Token> Tokens()
        {
            foreach (var token in RawTokens())
            {
                if (token.HasValue)
                    yield return token.Value;
            }
        }

        private IEnumerable<Token?> RawTokens()
        {
            var builder = new StringBuilder();

            while (true) {
                var next = _stream.Read();
                if (next == -1)
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.Eof);
                    break;
                }
                
                var c = (char)next;

                if (c == '\t' || c == '\n' || c == ' ' || c == '\r')
                    yield return Finish(builder);
                else if (c == '(')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.LParen);
                }
                else if (c == ')')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.RParen);
                }
                else if (c == '{')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.Begin);
                }
                else if (c == '}')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.End);
                }
                else
                {
                    builder.Append(c);
                    continue;
                }
            }
        }

        private Token? Finish(StringBuilder builder)
        {
            var t = builder.ToString();
            if (t.Length == 0) return null;
            builder.Clear();
            if (t == "fn")
                return new Token(TokenId.Fn, string.Empty);
            else
                return new Token(TokenId.Id, t);
        }
    }
}