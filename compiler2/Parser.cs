#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using compiler2.ast;

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
                    var fn = ParseFnDecl();

                    // Check uniqueness:
                    if (block.Decls.Contains(fn))
                    {
                        throw new ParseError(fn.Id,
                            $"{fn} already declared in this scope");
                    }

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
                    else if (t1.Id == TokenId.Eq)
                    {
                        // Assignment.
                        Console.WriteLine("Assignment");

                        var rhs = ParseExpr(Next(), out var la);
                        if (la.Id != TokenId.Semi)
                            throw new ParseError(la, "Expected ';'");

                        var assn = new ast.Assn(t, rhs);
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

        private ast.FnDecl ParseFnDecl()
        {
            Console.WriteLine("Function decl");

            // Function ID:
            Token t = Next();
            if (t.Id != TokenId.Id)
                throw new ParseError(t, "Expected identifier");
            var fn = new ast.FnDecl(t);
            var tId = t;

            t = Next();
            if (t.Id != TokenId.LParen)
                throw new ParseError(t, "Expected '('");

            // Function arguments:
            var argIds = new HashSet<string>();
            while (true)
            {
                t = Next();
                if (t.Id == TokenId.RParen)
                    break;

                var typeSpec = ParseTypeSpec(t);
                var argId = Next();
                if (argId.Id != TokenId.Id)
                    throw new ParseError(argId, "Expected identifier");
                if (!argIds.Add(argId.Text))
                    throw new ParseError(argId, "Argument already defined");

                fn.Params.Add(new compiler2.ast.Param(typeSpec, argId));

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
                fn.ReturnType = ParseTypeSpec(Next());

                t = Next();
            }

            if (t.Id != TokenId.Begin)
                throw new ParseError(t, "Expected '{'");

            // Function block:
            fn.Body = ParseBlock();

            return fn;
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
                {
                    args.Add(ParseExpr(t, out var la));
                    if (la.Id == TokenId.RParen)
                        break;
                    else if (la.Id == TokenId.Comma)
                        continue;
                    else
                        new ParseError(la, "Expected ',' or ')'");
                }
            }

            return new ast.FnCall(fnId)
            {
                Args = args,
            };
        }

        private ast.Expr ParseExpr(Token t, out Token la)
        {
            Console.WriteLine("Expr");

            var l = ParseTerm(t, out var la1);
            var term = l;

            while (la1.Id == TokenId.Op && (
                la1.Text == "+" || la1.Text == "-"))
            {
                var r = ParseTerm(Next(), out var la2);
                term = new ast.AlgExpr(term, la1, r);
                la1 = la2;
            }

            la = la1;
            return term;
        }

        private ast.Expr ParseTerm(Token t, out Token la)
        {
            Console.WriteLine("Term");

            var l = ParseFactor(t, out var la1);
            var term = l;

            while (la1.Id == TokenId.Op && (
                la1.Text == "/" || la1.Text == "*"))
            {
                var r = ParseFactor(Next(), out var la2);
                term = new ast.AlgExpr(term, la1, r);
                la1 = la2;
            }

            la = la1;
            return term;
        }

        private ast.Expr ParseFactor(Token t, out Token la)
        {
            Console.WriteLine("Factor");

            if (t.Id == TokenId.Id)
            {
                // Could be a function call.
                var t1 = Next();
                if (t1.Id == TokenId.LParen)
                {
                    // Function call.
                    var fnCall = ParseFnCall(fnId: t);
                    la = Next();
                    return fnCall;
                }
                else
                {
                    // Variable.
                    la = t1;
                    return new ast.VarExpr(t);
                }
            }
            else if (t.Id == TokenId.Num)
            {
                // Number literals.
                la = Next();
                return new ast.NumExpr(t);
            }
            else if (t.Id == TokenId.Str)
            {
                // String literals.
                la = Next();
                return new ast.StrExpr(t);
            }
            else if (t.Id == TokenId.Op && t.Text == "-")
            {
                // Unary negation.
                var negated = ParseFactor(Next(), out var la1);
                la = la1;
                var mult = t;
                mult.Text = "*";
                return new ast.AlgExpr(new ast.NumExpr(t), mult, negated);
            }
            else if (t.Id == TokenId.LParen)
            {
                // Parenthetical expression.
                var expr = ParseExpr(Next(), out var la1);

                if (la1.Id != TokenId.RParen)
                    throw new ParseError(la1, "Expected ')'");
                
                la = Next();
                return expr;
            }
            else
                throw new ParseError(t, "Expected identifier or literal");
        }

        private ast.TypeSpec ParseTypeSpec(Token t)
        {
            if (t.Id != TokenId.Id)
                throw new ParseError(t, "Expected identifier");
            return new ast.SimpleTypeSpec(t);
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
        public HashSet<Decl> Decls = new HashSet<global::compiler2.ast.Decl>();
        public List<Stmt> Stmts = new List<global::compiler2.ast.Stmt>();
    }

    public abstract class Decl 
    {
        abstract public void Show();
    }

    public class FnDecl : Decl, IShowable
    {
        public Token Id;
        public TypeSpec? ReturnType = null;
        public List<Param> Params = new List<global::compiler2.ast.Param>();
        public Block? Body = null;

        public FnDecl(Token id)
        {
            Id = id;
        }

        public override bool Equals(object? obj)
        {
            return obj is FnDecl decl &&
                Id.Text == decl.Id.Text && ReturnType == decl.ReturnType && 
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
            return $"[FnD:{Id.Text}({args}) -> {ReturnType}]";
        }
    }

    public class Param : IShowable
    {
        public TypeSpec Type;
        public Token Id;

        public Param(TypeSpec type, Token id)
        {
            Type = type;
            Id = id;
        }

        public void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[P:{Type} {Id.Text}]";
        }
    }

    public abstract class TypeSpec : IShowable
    {
        abstract public void Show();
    }

    public class SimpleTypeSpec : TypeSpec
    {
        public Token Id;

        public SimpleTypeSpec(Token id)
        {
            Id = id;
        }

        public override bool Equals(object? obj)
        {
            return obj is SimpleTypeSpec other && 
                Id.Text == other.Id.Text;
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
            return $"[ST:{Id.Text}]";
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
        public Token Id;
        public List<Expr> Args = new List<global::compiler2.ast.Expr>();

        public FnCall(Token id)
        {
            Id = id;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            var args = string.Join(',', Args.Select(x => x.ToString()));
            return $"[FnC:{Id.Text}({args})]";
        }
    }

    public class NumExpr : Expr
    {
        public Token Value;

        public NumExpr(Token value)
        {
            Value = value;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[N:{Value.Text}]";
        }
    }
    
    public class VarExpr : Expr
    {
        public Token Value;

        public VarExpr(Token value)
        {
            Value = value;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[V:{Value.Text}]";
        }
    }

    public class StrExpr : Expr
    {
        public Token Value;

        public StrExpr(Token value)
        {
            Value = value;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[S:{Value.Text}]";
        }
    }

    public class AlgExpr : Expr
    {
        public Expr Lhs;
        public Token Op;
        public Expr Rhs;

        public AlgExpr(Expr lhs, Token op, Expr rhs)
        {
            Lhs = lhs;
            Op = op;
            Rhs = rhs;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[{Lhs}{Op.Text}{Rhs}]";
        }
    }

    public class Assn : Stmt
    {
        public Token Id;
        public Expr Value;

        public Assn(Token id, Expr value)
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
            return $"[A:{Id.Text} = {Value}]";
        }
    }
}