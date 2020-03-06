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

            while (true)
            {
                var t = Next();
                if (t.Id == TokenId.Eof)
                    break;

                // Function declaration:
                if (t.Id == TokenId.Fn)
                {
                    var decl = new ast.FnDecl();

                    t = Next();
                    if (t.Id == TokenId.Id)
                        decl.Id = t.Text;

                    t = Next();
                    if (t.Id != TokenId.LParen)
                        return res;

                    // Function arguments:
                    // TODO

                    t = Next();
                    if (t.Id != TokenId.RParen)
                        return res;

                    t = Next();
                    if (t.Id != TokenId.Begin)
                        return res;

                    // Function block:
                    // TODO

                    t = Next();
                    if (t.Id != TokenId.End)
                        return res;

                    res.Decls.Add(decl);
                }
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

        public List<Decl> Decls = new List<Decl>();

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
                Console.WriteLine("ERROR COMPILING THE PROGRAM:");
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

        public override void Show()
        {
            Printer.Print($"FnDecl({Id})");
        }
    }
}