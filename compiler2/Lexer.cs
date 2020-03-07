#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace compiler2
{
    public class LexerFinishException : Exception {}

    public class Lexer
    {
        private readonly StreamReader stream;
        private readonly StringBuilder builder = new StringBuilder();
        private int line = 1;
        private int col = 0;

        public Lexer(StreamReader stream)
        {
            this.stream = stream;
        }

        public IEnumerable<Token> Tokens()
        {
            var enumerator = RawTokens().GetEnumerator();
            while (true)
            {
                var ended = false;
                try
                {
                    enumerator.MoveNext();
                }
                catch (LexerFinishException)
                {
                    ended = true;
                }

                if (ended)
                {
                    yield return new Token(TokenId.End, line, col);
                    yield return new Token(TokenId.Eof, line, col);
                    break;
                }
                else
                {
                    if (enumerator.Current.HasValue)
                        yield return enumerator.Current.Value;
                }
            }
        }

        private char Next()
        {
            var next = stream.Read();
            col += 1;
            if (next == -1)
                throw new LexerFinishException();

            return (char)next;
        }

        private IEnumerable<Token?> RawTokens()
        {
            yield return new Token(TokenId.Begin, 0, 0);

            while (true) {
                var c = Next();
            Backtrack:

                if (c == '\t')
                {
                    yield return Finish();
                    col += 3;
                }
                else if (c == '\n')
                {
                    yield return Finish();
                    line += 1;
                    col = 0;
                }
                else if (c == ' ')
                {
                    yield return Finish();
                }
                else if (c == '\r') 
                { 
                    col -= 1; 
                }
                else if (c == '(')
                {
                    yield return Finish();
                    yield return new Token(TokenId.LParen, line, col);
                }
                else if (c == ')')
                {
                    yield return Finish();
                    yield return new Token(TokenId.RParen, line, col);
                }
                else if (c == '{')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Begin, line, col);
                }
                else if (c == '}')
                {
                    yield return Finish();
                    yield return new Token(TokenId.End, line, col);
                }
                else if (c == ';')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Semi, line, col);
                }
                else if (c == '-')
                {
                    yield return Finish();
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
                    yield return Finish();
                    c = Next();
                    if (c == '/')
                    {
                        // Skip the rest of the line when we run into a comment.
                        while (Next() != '\n') {};
                    }
                    else if (c == '*')
                    {
                        // Skip until we run into a closing multiline comment.
                        while (true)
                        {
                            c = Next();
                            if (c == '*')
                            {
                                if (Next() == '/')
                                    break;
                            }
                        }
                    }
                    else
                    {
                        yield return new Token(TokenId.Operator, "/", line, col - 1);
                        goto Backtrack;
                    }
                }
                else if (c == '=')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Equal, line, col);
                }
                else if (c == '*' || c == '+' || c == '-' || c == '^')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Operator, c.ToString(), line, col);
                }
                else if (char.IsDigit(c))
                {
                    // Read digits until there are no more digits.
                    builder.Append(c);
                    while (true)
                    {
                        c = Next();
                        if (char.IsDigit(c))
                        {
                            builder.Append(c);
                        }
                        else
                        {
                            yield return Finish();
                            goto Backtrack;
                        }
                    }
                    
                }
                else
                {
                    builder.Append(c);
                    continue;
                }
            }
        }

        private Token? Finish()
        {
            var t = builder.ToString();
            if (t.Length == 0) return null;
            builder.Clear();
            if (t == "fn")
                return new Token(TokenId.Fn, string.Empty, line, col - t.Length);
            else if (char.IsDigit(t[0]))
                return new Token(TokenId.Number, t, line, col - t.Length);
            else
                return new Token(TokenId.Id, t, line, col - t.Length);
        }
    }
}