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
            if (next == -1)
                throw new LexerFinishException();

            var c = (char)next;
            if (c == '\n')
            {
                line += 1;
                col = 0;
            }
            else
                col += 1;

            return c;
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
                else if (c == ',')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Comma, line, col);
                }
                else if (c == '.')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Dot, line, col);
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
                    {
                        yield return new Token(TokenId.Op, "-", line, col - 1);
                        goto Backtrack;
                    }
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
                        yield return new Token(TokenId.Op, "/", line, col - 1);
                        goto Backtrack;
                    }
                }
                else if (c == ':')
                {
                    yield return Finish();
                    c = Next();
                    if (c == '=')
                        yield return new Token(TokenId.Assign, line, col - 2);
                    else
                    {
                        yield return new Token(TokenId.Colon, line, col - 1);
                        goto Backtrack;
                    }
                }
                else if (c == '=')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Eq, line, col);
                }
                else if (c == '*' || c == '+' || c == '-' || c == '^')
                {
                    yield return Finish();
                    yield return new Token(TokenId.Op, c.ToString(), line, col);
                }
                else if (c == '"')
                {
                    yield return Finish();
                    builder.Append(c);
                    // Read until we run into the end of the string.
                    while (true)
                    {
                        c = Next();
                        if (c == '"')
                        {
                            builder.Append(c);
                            break;
                        }
                        else if (c == '\\')
                        {
                            builder.Append('\\');
                            // Escaped character.
                            builder.Append(Next());
                        }
                        else
                            builder.Append(c);
                    }

                    yield return Finish();
                }
                else if (char.IsDigit(c))
                {
                    // Read digits until there are no more digits.
                    builder.Append(c);
                    while (true)
                    {
                        c = Next();
                        if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
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

            // Keywords:

            Token? res = null;
            if (IsKeyword(t, "fn", TokenId.Fn, out res)) return res;
            else if (IsKeyword(t, "return", TokenId.Return, out res)) return res;
            else if (IsKeyword(t, "type", TokenId.Type, out res)) return res;
            else if (IsKeyword(t, "if", TokenId.If, out res)) return res;
            else if (IsKeyword(t, "else", TokenId.Else, out res)) return res;

            // Literals:

            // Boolean (true/false):
            else if (t == "True")
                return new Token(TokenId.Bool, t, line, col - t.Length);
            else if (t == "False")
                return new Token(TokenId.Bool, t, line, col - t.Length);

            // Number:
            else if (char.IsDigit(t[0]))
                return new Token(TokenId.Num, t, line, col - t.Length);
                
            // String:
            else if (t[0] == '"')
                return new Token(TokenId.Str, t, line, col - t.Length);

            // Identifier:
            else
                return new Token(TokenId.Id, t, line, col - t.Length);
        }

        private bool IsKeyword(string t, string keyword, TokenId id, out Token? token)
        {
            if (t == keyword)
            {
                token = new Token(id, string.Empty, line, col - t.Length);
                return true;
            }
            else
            {
                token = null;
                return false;
            }
        }
    }
}