#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace compiler2
{
    public class Lexer
    {
        private readonly StreamReader _stream;
        private int Line = 1;
        private int Col = 0;

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

        private char Next()
        {
            var next = _stream.Read();
            Col += 1;
            if (next == -1)
                throw new InvalidOperationException();

            return (char)next;
        }

        private IEnumerable<Token?> RawTokens()
        {
            var builder = new StringBuilder();

            while (true) {
                var next = _stream.Read();
                Col += 1;
                if (next == -1)
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.Eof, Line, Col);
                    break;
                }
                
                var c = (char)next;

                if (c == '\t')
                {
                    yield return Finish(builder);
                    Col += 3;
                }
                else if (c == '\n')
                {
                    yield return Finish(builder);
                    Line += 1;
                    Col = 0;
                }
                else if (c == ' ')
                {
                    yield return Finish(builder);
                }
                else if (c == '\r') 
                { 
                    Col -= 1; 
                }
                else if (c == '(')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.LParen, Line, Col);
                }
                else if (c == ')')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.RParen, Line, Col);
                }
                else if (c == '{')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.Begin, Line, Col);
                }
                else if (c == '}')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.End, Line, Col);
                }
                else if (c == '-')
                {
                    yield return Finish(builder);
                    c = Next();
                    if (c == '>')
                    {
                        yield return new Token(TokenId.RArrow, Line, Col);
                    }
                    else
                        throw new NotImplementedException();
                }
                else if (c == '/')
                {
                    yield return Finish(builder);
                    c = Next();
                    if (c == '/')
                    {
                        // Skip the rest of the line when we run into a comment.
                        while (Next() != '\n') {};
                    }
                    else
                        throw new NotImplementedException();
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
                return new Token(TokenId.Fn, string.Empty, Line, Col - t.Length);
            else
                return new Token(TokenId.Id, t, Line, Col - t.Length);
        }
    }
}