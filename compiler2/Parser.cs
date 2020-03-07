#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace compiler2
{
    public class Parser
    {
        private IEnumerator<Token>? tokens;

        public ast.Program Parse(IEnumerator<Token> tokens)
        {
            this.tokens = tokens;
            var ast = new ast.Program();

            try
            {
                var t = Next();
                if (t.Id != TokenId.Begin)
                    throw new InvalidOperationException();

                ast.Body = ParseBlock();

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

        private ast.Block ParseBlock()
        {
            var block = new ast.Block();
            while (true)
            {
                var t = Next();

                // Function declaration:
                if (t.Id == TokenId.Fn)
                {
                    var fn = new ast.FnDecl();

                    // Function ID:
                    t = Next();
                    if (t.Id != TokenId.Id)
                        throw new ParseError(t, "Expected identifier");
                    fn.Id = t.Text;
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

                        fn.Params.Add(new compiler2.ast.Param()
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
                        fn.ReturnType = ParseTypeSpec();

                        t = Next();
                    }

                    // Check uniqueness:
                    if (block.Decls.Contains(fn))
                    {
                        throw new ParseError(tId, 
                            $"{fn} already declared in this scope");
                    }

                    if (t.Id != TokenId.Begin)
                        throw new ParseError(t, "Expected '{'");

                    // Function block:
                    fn.Body = ParseBlock();

                    fn.Show();
                    block.Decls.Add(fn);
                }
                else if (t.Id == TokenId.End)
                {
                    return block;
                }
                else if (t.Id == TokenId.Id)
                {
                    // Could be an assignment or a function call.
                    var t1 = Next();
                    if (t1.Id == TokenId.LParen)
                    {
                        // Function call.
                        var fnCall = ParseFnCall(fnId: t);
                        var t2 = Next();
                        if (t2.Id != TokenId.Semi)
                            throw new ParseError(t2, "Expected ';'");

                        var stmt = new ast.ExprStmt(fnCall);
                        stmt.Show();
                        block.Stmts.Add(stmt);
                    }
                    else if (t1.Id == TokenId.Equal)
                    {
                        // Assignment.
                        Console.WriteLine("Assignment");

                        var rhs = ParseExpr(Next());
                        var t2 = Next();
                        if (t2.Id != TokenId.Semi)
                            throw new ParseError(t2, "Expected ';'");

                        var assn = new ast.Assn(t.Text, rhs);
                        assn.Show();
                        block.Stmts.Add(assn);
                    }
                    else
                        throw new NotImplementedException();
                }
                else
                    throw new ParseError(t, "Expected '}', assignment, or statement.");
            }
        }

        private ast.FnCall ParseFnCall(Token fnId)
        {
            Console.WriteLine("Function call");

            var args = new List<ast.Expr>();
            while (true)
            {
                var t = Next();
                if (t.Id == TokenId.RParen)
                    break;
                else
                    throw new NotImplementedException();
            }

            return new ast.FnCall()
            {
                Id = fnId.Text,
                Args = args,
            };
        }

        private ast.Expr ParseExpr(Token t)
        {
            Console.WriteLine("Expr");

            if (t.Id == TokenId.Id)
            {
                // Could be a function call.
                var t1 = Next();
                if (t1.Id == TokenId.LParen)
                {
                    // Function call.
                    var fnCall = ParseFnCall(fnId: t);
                    return fnCall;
                }
                else
                    throw new NotImplementedException();
            }
            else if (t.Id == TokenId.Number)
            {
                return new ast.Number(t.Text);
            }
            else
                throw new ParseError(t, "Expected identifier");
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
            if (tokens is null) throw new InvalidOperationException();
            var success = tokens.MoveNext();
            if (!success)
                return new Token(TokenId.Eof, 0, 0);
            Console.WriteLine(" >> " + tokens.Current);
            return tokens.Current;
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
            Console.WriteLine(new String(' ', _indent * 2) + text);
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
        public Block? Body = null;

        public void Show()
        {
            if (Success)
            {
                Printer.Print("Program");
                Printer.Print("");
                if (Body != null)
                {
                    foreach (var decl in Body.Decls)
                        decl.Show();
                    foreach (var stmt in Body.Stmts)
                        stmt.Show();
                }
            }
            else
            {
                Console.WriteLine("ERROR COMPILING THE PROGRAM");
            }
        }
    }

    public class Block
    {
        public HashSet<Decl> Decls = new HashSet<Decl>();
        public List<Stmt> Stmts = new List<Stmt>();
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
        public Block? Body = null;

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
            if (Body != null)
            {
                Printer.Promote();
                foreach (var decl in Body.Decls)
                    decl.Show();
                foreach (var stmt in Body.Stmts)
                    stmt.Show();
                Printer.Demote();
            }
        }

        public override string ToString()
        {
            var args = string.Join(',', Params.Select(x => x.ToString()));
            return $"[FnD:{Id}({args}) -> {ReturnType}]";
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
            return $"<P:{Type} {Id}>";
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
            return $"[ST:{Id}]";
        }
    }

    public abstract class Stmt : IShowable
    {
        abstract public void Show();
    }

    public class ExprStmt : Stmt
    {
        public Expr Expr;

        public ExprStmt(Expr expr)
        {
            Expr = expr;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[ExS:{Expr}]";
        }
    }

    public abstract class Expr : IShowable
    {
        public abstract void Show();
    }

    public class FnCall : Expr
    {
        public string Id = string.Empty;
        public List<Expr> Args = new List<Expr>();

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            var args = string.Join(',', Args.Select(x => x.ToString()));
            return $"[FnC:{Id}({args})]";
        }
    }

    public class Number : Expr
    {
        public string Value;

        public Number(string value)
        {
            Value = value;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[N:{Value}]";
        }
    }

    public class Assn : Stmt
    {
        public string Id = string.Empty;
        public Expr Value;

        public Assn(string id, Expr value)
        {
            Id = id;
            Value = value;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[A:{Id} = {Value})]";
        }
    }
}