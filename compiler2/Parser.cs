#nullable enable
using System;
using System.Collections.Generic;

namespace compiler2
{
    public class Parser
    {
        private IEnumerator<Token>? _tokens;

        public ast.Program Parse(IEnumerator<Token> tokens)
        {
            _tokens = tokens;

            var res = new ast.Program();

            try
            {
                while (true)
                {
                    var t = Next();
                    if (t.Id == TokenId.Eof)
                        break;

                    // Function declaration:
                    if (t.Id == TokenId.Fn)
                    {
                        var decl = new ast.FnDecl();

                        // Function ID:
                        t = Next();
                        if (t.Id != TokenId.Id)
                            throw new ParseError(t);
                        decl.Id = t.Text;
                        var tId = t;

                        t = Next();
                        if (t.Id != TokenId.LParen)
                            throw new ParseError(t);

                        // Function arguments:
                        // TODO

                        t = Next();
                        if (t.Id != TokenId.RParen)
                            throw new ParseError(t);

                        t = Next();
                        if (t.Id == TokenId.RArrow)
                        {
                            // Function return:
                            var t1 = Next();
                            if (t1.Id != TokenId.Id)
                                throw new ParseError(t1);
                            decl.Return = t1.Text;

                            t = Next();
                        }

                        // Check uniqueness:
                        if (res.Decls.Contains(decl))
                        {
                            throw new ParseError(tId, 
                                $"{decl} already declared in this scope.");
                        }

                        if (t.Id != TokenId.Begin)
                            throw new ParseError(t);

                        // Function block:
                        // TODO

                        t = Next();
                        if (t.Id != TokenId.End)
                            throw new ParseError(t);

                        res.Decls.Add(decl);
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
                res.Success = false;
                res.ErrorMessage = err.Message;
                return res;
            }

            res.Success = true;
            return res;
        }

        private Token Next()
        {
            if (_tokens is null) throw new InvalidOperationException();
            var success = _tokens.MoveNext();
            if (!success) throw new NotImplementedException();
            return _tokens.Current;
        }
    }

    public class ParseError : Exception 
    {
        public ParseError(Token t, string? message = null) : base(
            $"Line {t.Line}, Col {t.Col}:\n{message}") { }
    }
}

namespace compiler2.ast
{
    public static class Printer
    {
        private static int _indent = 0;

        public static void Promote()
        {
            _indent += 1;
        }

        public static void Demote()
        {
            _indent -= 1;
        }

        public static void Print(string text)
        {
            Console.WriteLine(new String(' ', _indent) + text);
        }
    }

    public interface IShowable
    {
        void Show();
    }

    public class Program : IShowable
    {
        public bool Success = false;
        public string ErrorMessage = string.Empty;

        public HashSet<Decl> Decls = new HashSet<Decl>();

        public void Show()
        {
            if (Success)
            {
                Printer.Print("Program");
                Printer.Promote();
                foreach (var decl in Decls)
                    decl.Show();
            }
            else
            {
                Console.WriteLine("ERROR COMPILING THE PROGRAM");
            }
        }
    }

    public abstract class Decl : IShowable
    {
        abstract public void Show();
    }

    public class FnDecl : Decl, IShowable
    {
        public string Id = string.Empty;
        public string Return = string.Empty;

        public override bool Equals(object? obj)
        {
            return obj is FnDecl decl &&
                Id == decl.Id && Return == decl.Return;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Return);
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"FnDecl({Id} -> {Return})";
        }
    }
}