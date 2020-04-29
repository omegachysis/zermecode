#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace compiler3
{
    public class LexException : Exception
    {
        public LexException(string message, int line, int col) : 
        base($"Line {line}, Column {col}: {message}")
        {}
    }

    public class Lexer
    {
        private readonly StreamReader _input;

        public int Line { get; private set; }
        public int Col { get; private set; }

        public Lexer(StreamReader input)
        {
            _input = input;
        }

        private Token Symbol(TokenId symbol, int size)
        {
            Col += size;
            return new Token(symbol, Line, Col);
        }

        private char? _read()
        {
            var raw = _input.Read();
            if (raw == -1)
                return null;
            else
            {
                var c = (char)raw;
                if (c == '\n')
                {
                    Line++;
                    Col = 0;
                }
                else
                    Col++;

                return c;
            }
        }

        private char _readChecked()
        {
            var res = _read();
            if (res.HasValue)
                return res.Value;
            else
                throw new LexException("Unexpected EOF", Line, Col);
        }

        /// <summary>
        /// Return an enumerable which processes all tokens until EOF.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Token> Lex()
        {
            while (true)
            {
                var maybeC = _read();
                if (!maybeC.HasValue)
                {
                    yield return Symbol(TokenId.Eof, 0);
                    break;
                }
                var c = maybeC.Value;

                // Try to read numbers or identifiers first.
                if (char.IsDigit(c))
                {
                    // Start reading a number literal.
                    var num = new StringBuilder();
                    num.Append(c);
                    while (true)
                    {
                        c = _readChecked();
                        if (char.IsDigit(c))
                            num.Append(c);
                        else
                            break;
                    }

                    yield return new Token(TokenId.NumLit,
                        num.ToString(), Line, Col);
                }
                else if (char.IsLetter(c))
                {
                    // Start reading an identifier.
                    var id = new StringBuilder();
                    id.Append(c);
                    while (true)
                    {
                        c = _readChecked();
                        if (char.IsLetterOrDigit(c) || c == '_')
                            id.Append(c);
                        else
                            break;
                    }

                    yield return new Token(id.ToString(), Line, Col);
                }

                // Not an else here because we may have broken out of the above two 
                // when we saw a symbol that didn't match. Continuing gracefully 
                // here eliminates backtracking or the need for gotos.

                if (c == '\n' || c == '\r' || c == ' ' || c == '\t')
                    // Ignore whitespace.
                    continue;
                if (c == '(')
                    yield return Symbol(TokenId.LParen, 1);
                else if (c == ')')
                    yield return Symbol(TokenId.RParen, 1);
                else if (c == '{')
                    yield return Symbol(TokenId.LBrace, 1);
                else if (c == '}')
                    yield return Symbol(TokenId.RBrace, 1);
                else if (c == '"')
                {
                    yield return Symbol(TokenId.DoubleQuote, 1);
                    var str = new StringBuilder();

                    // Starting a string literal.
                    while (true)
                    {
                        c = _readChecked();
                        if (c == '"')
                            break;
                        else if (c == '\\')
                        {
                            // Read a blackslash, see it as an escaped character.
                            c = _readChecked();

                            if (c == 'n')
                                str.Append('\n');
                            else if (c == '\\')
                                str.Append('\\');
                            else if (c == 't')
                                str.Append('\t');
                            else if (c == 'r')
                                str.Append('\r');
                            else if (c == '"')
                                str.Append('"');
                            else
                                throw new LexException($"Unrecognized escape character '\\{c}'",
                                    Line, Col);

                            str.Append(c);
                        }
                        else
                            str.Append(c);
                    }

                    yield return new Token(TokenId.StringLit,
                        str.ToString(), Line, Col);
                    yield return Symbol(TokenId.DoubleQuote, 1);
                }
                else
                    throw new LexException($"Unexpected token '{Regex.Escape(c.ToString())}'",
                        Line, Col);
            }
        }
    }
}