#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace compiler2
{
    public class Lexer
    {
        private readonly StreamReader stream;
        private int line = 1;
        private int col = 0;

        public Lexer(StreamReader stream)
        {
            this.stream = stream;
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
            var next = stream.Read();
            col += 1;
            if (next == -1)
                throw new InvalidOperationException();

            return (char)next;
        }

        private IEnumerable<Token?> RawTokens()
        {
            var builder = new StringBuilder();
            yield return new Token(TokenId.Begin, 0, 0);

            while (true) {
                var next = stream.Read();
                col += 1;
                if (next == -1)
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.End, line, col);
                    break;
                }
                
                var c = (char)next;

                if (c == '\t')
                {
                    yield return Finish(builder);
                    col += 3;
                }
                else if (c == '\n')
                {
                    yield return Finish(builder);
                    line += 1;
                    col = 0;
                }
                else if (c == ' ')
                {
                    yield return Finish(builder);
                }
                else if (c == '\r') 
                { 
                    col -= 1; 
                }
                else if (c == '(')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.LParen, line, col);
                }
                else if (c == ')')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.RParen, line, col);
                }
                else if (c == '{')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.Begin, line, col);
                }
                else if (c == '}')
                {
                    yield return Finish(builder);
                    yield return new Token(TokenId.End, line, col);
                }
                else if (c == ';')
                {
                    yield return new Token(TokenId.Semi, line, col);
                }
                else if (c == '-')
                {
                    yield return Finish(builder);
                    c = Next();
                    if (c == '>')
                    {
                        yield return new Token(TokenId.RArrow, line, col);
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
                return new Token(TokenId.Fn, string.Empty, line, col - t.Length);
            else
                return new Token(TokenId.Id, t, line, col - t.Length);
        }
    }
}