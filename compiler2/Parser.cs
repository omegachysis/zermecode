#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace compiler2
{
    public class Parser
    {
        private IEnumerator<Token>? _tokens;
        private ast.Program ast = new ast.Program();

        public ast.Program Parse(IEnumerator<Token> tokens)
        {
            _tokens = tokens;

            try
            {
                var t = Next();
                if (t.Id != TokenId.Begin)
                    throw new InvalidOperationException();

                ParseBlock();

                t = Next();
                if (t.Id != TokenId.Eof)
                    throw new ParseError(t, "Expected end of file");
            }
            catch (ParseError err)
            {
                Console.WriteLine(err.ToString());
                ast.Success = false;
                ast.ErrorMessage = err.Message;
                return ast;
            }

            ast.Success = true;
            return ast;
        }

        private void ParseBlock()
        {
            while (true)
            {
                var t = Next();

                // Function declaration:
                if (t.Id == TokenId.Fn)
                {
                    var decl = new ast.FnDecl();

                    // Function ID:
                    t = Next();
                    if (t.Id != TokenId.Id)
                        throw new ParseError(t, "Expected identifier");
                    decl.Id = t.Text;
                    var tId = t;

                    t = Next();
                    if (t.Id != TokenId.LParen)
                        throw new ParseError(t, "Expected '('");

                    // Function arguments:
                    var argIds = new HashSet<string>();
                    while (true)
                    {
                        var typeSpec = ParseTypeSpec();
                        var argId = Next();
                        if (argId.Id != TokenId.Id)
                            throw new ParseError(argId, "Expected identifier");
                        if (!argIds.Add(argId.Text))
                            throw new ParseError(argId, "Argument already defined");

                        decl.Params.Add(new compiler2.ast.Param()
                        {
                            Type = typeSpec,
                            Id = argId.Text,
                        });

                        t = Next();
                        if (t.Id == TokenId.RParen)
                            break;
                        else if (t.Id == TokenId.Comma)
                            continue;
                        else
                            new ParseError(t, "Expected ',' or ')'");
                    }

                    t = Next();
                    if (t.Id == TokenId.RArrow)
                    {
                        // Function return:
                        decl.ReturnType = ParseTypeSpec();

                        t = Next();
                    }

                    // Check uniqueness:
                    if (ast.Decls.Contains(decl))
                    {
                        throw new ParseError(tId, 
                            $"{decl} already declared in this scope");
                    }

                    if (t.Id != TokenId.Begin)
                        throw new ParseError(t, "Expected '{'");

                    // Function block:
                    ParseBlock();

                    ast.Decls.Add(decl);
                }
                else if (t.Id == TokenId.End)
                {
                    return;
                }
                else
                    throw new ParseError(t, "Expected function declaration, statement, or '}'");
            }
        }

        private ast.TypeSpec ParseTypeSpec()
        {
            var t = Next();
            if (t.Id != TokenId.Id)
                throw new ParseError(t, "Expected identifier");
            return new ast.SimpleTypeSpec(t.Text);
        }

        private Token Next()
        {
            if (_tokens is null) throw new InvalidOperationException();
            var success = _tokens.MoveNext();
            if (!success)
                return new Token(TokenId.Eof, 0, 0);
            Console.WriteLine(" >> " + _tokens.Current);
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
        public List<Stmt> Stmts = new List<Stmt>();

        public void Show()
        {
            if (Success)
            {
                Printer.Print("Program");
                Printer.Promote();
                foreach (var decl in Decls)
                    decl.Show();
                foreach (var stmt in Stmts)
                    stmt.Show();

                Printer.Demote();
            }
            else
            {
                Console.WriteLine("ERROR COMPILING THE PROGRAM");
            }
        }
    }

    public abstract class Decl 
    {
        abstract public void Show();
    }

    public class FnDecl : Decl, IShowable
    {
        public string Id = string.Empty;
        public TypeSpec? ReturnType = null;
        public List<Param> Params = new List<Param>();
        public List<Stmt> Body = new List<Stmt>();

        public override bool Equals(object? obj)
        {
            return obj is FnDecl decl &&
                Id == decl.Id && ReturnType == decl.ReturnType && 
                Params.Select(x => x.Type).SequenceEqual(decl.Params.Select(x => x.Type));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, ReturnType);
        }

        public override void Show()
        {
            Printer.Print(ToString());
            Printer.Promote();
            foreach (var stmt in Body)
                stmt.Show();
            Printer.Demote();
        }

        public override string ToString()
        {
            var args = string.Join(',', Params.Select(x => x.ToString()));
            return $"FnDecl({Id}({args}) -> {ReturnType})";
        }
    }

    public class Param : IShowable
    {
        public TypeSpec? Type = null;
        public string Id = string.Empty;

        public void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"Param({Type} {Id})";
        }
    }

    public abstract class TypeSpec : IShowable
    {
        abstract public void Show();
    }

    public class SimpleTypeSpec : TypeSpec
    {
        public string Id = string.Empty;

        public SimpleTypeSpec(string id)
        {
            Id = id;
        }

        public override bool Equals(object? obj)
        {
            return obj is SimpleTypeSpec other && 
                Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"SimpleTypeSpec({Id})";
        }
    }

    public abstract class Stmt : IShowable
    {
        abstract public void Show();
    }

    public class FnCall : Stmt
    {
        public string Id = string.Empty;
        public List<string> Args = new List<string>();

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            var args = string.Join(',', Args);
            return $"FnCall({Id}({args}))";
        }
    }
}