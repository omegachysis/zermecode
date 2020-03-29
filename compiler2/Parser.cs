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
        private Stack<Block> blocks = new Stack<Block>();
        private Block? block => blocks.Count == 0 ? null : blocks.Peek();

        public ast.Program Parse(IEnumerator<Token> tokens)
        {
            this.tokens = tokens;
            var ast = new ast.Program();

            try
            {
                var t = Next();
                if (t.Id != TokenId.Begin)
                    throw new InvalidOperationException();

                ast.Body = ParseBlock(Next(), oneStatement: false);

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

        private ast.Block ParseBlock(Token t, bool oneStatement)
        {
            blocks.Push(new ast.Block(tokens!.Current, block));
            while (true)
            {
                // Function declaration:
                if (t.Id == TokenId.Fn)
                {
                    var fn = ParseFnDecl();

                    // Check uniqueness:
                    if (block!.FnDecls.Contains(fn))
                    {
                        throw new ParseError(fn.Id,
                            $"{fn} already declared in this scope");
                    }

                    block.FnDecls.Add(fn);
                }
                // Type declaration:
                else if (t.Id == TokenId.Type)
                {
                    var type = ParseTypeDecl();

                    // Check uniqueness:
                    if (block!.TypeDecls.Contains(type))
                    {
                        throw new ParseError(type.Id,
                            $"{type} already declared in this scope");
                    }

                    block.TypeDecls.Add(type);
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

                        var stmt = new ast.ExprStmt(block!, fnCall);
                        block!.Stmts.Add(stmt);
                    }
                    else if (t1.Id == TokenId.Eq || t1.Id == TokenId.Assign)
                    {
                        // Assignment (mutable or immutable).
                        var rhs = ParseExpr(Next(), out var la);
                        if (la.Id != TokenId.Semi)
                            throw new ParseError(la, "Expected ';'");

                        var assn = new ast.Assn(block!, t, rhs,
                            mutable: t1.Id == TokenId.Assign);
                        block!.Stmts.Add(assn);
                    }
                    else
                        throw new ParseError(t, "Expected '=' or ':='");
                }
                else if (t.Id == TokenId.Return)
                {
                    // Return statement.
                    var t1 = Next();
                    if (t1.Id == TokenId.Semi)
                        block!.Stmts.Add(new ast.ReturnStmt(
                            block!, returnKeyword: t, value: null));
                    else
                    {
                        var expr = ParseExpr(t1, out var la);
                        block!.Stmts.Add(new ast.ReturnStmt(
                            block!, returnKeyword: t, value: expr));
                    }
                }
                else if (t.Id == TokenId.End && !oneStatement)
                {
                    return blocks.Pop();
                }
                else if (t.Id == TokenId.If)
                {
                    var ifToken = t;

                    // If statement.
                    t = Next();

                    if (t.Id != TokenId.LParen)
                        throw new ParseError(t, "Expected '('");
                    var cond = ParseExpr(t, out var la);

                    Block body;
                    if (la.Id == TokenId.Begin)
                        body = ParseBlock(Next(), oneStatement: false);
                    else
                        body = ParseBlock(la, oneStatement: true);

                    block!.Stmts.Add(new ast.IfStmt(
                        block!, ifToken, cond, body
                    ));
                }
                else
                    throw new ParseError(t, "Expected '}', assignment, or statement");

                if (oneStatement)
                    return blocks.Pop();
                else
                    t = Next();
            }
        }

        private ast.TypeDecl ParseTypeDecl()
        {
            // Type ID:
            Token tId = Next();
            if (tId.Id != TokenId.Id)
                throw new ParseError(tId, "Expected identifier");
                
            var t = Next();
            if (t.Id != TokenId.Begin)
                throw new ParseError(t, "Expected '{'");

            return new ast.TypeDecl(block!, tId, 
                ParseBlock(Next(), oneStatement: false));
        }

        private ast.FnDecl ParseFnDecl()
        {
            // Function ID:
            Token t = Next();
            if (t.Id != TokenId.Id)
                throw new ParseError(t, "Expected identifier");
            var tId = t;

            // Detect if this is an operator definition:
            if (t.Text == "op")
            {
                t = Next();
                if (t.Id != TokenId.Op)
                    throw new ParseError(t, "Expected operator");
                tId.Text += t.Text;
            }

            t = Next();
            if (t.Id != TokenId.LParen)
                throw new ParseError(t, "Expected '('");

            // Function arguments:
            var argIds = new HashSet<string>();
            var parameters = new List<compiler2.ast.Param>();
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

                parameters.Add(new compiler2.ast.Param(typeSpec, argId));

                t = Next();
                if (t.Id == TokenId.RParen)
                    break;
                else if (t.Id == TokenId.Comma)
                    continue;
                else
                    new ParseError(t, "Expected ',' or ')'");
            }

            t = Next();
            TypeSpec? returnType = null;
            if (t.Id == TokenId.RArrow)
            {
                // Function return:
                returnType = ParseTypeSpec(Next());

                t = Next();
            }

            if (t.Id != TokenId.Begin)
                throw new ParseError(t, "Expected '{'");

            // Function block:
            return new ast.FnDecl(block!, tId, parameters, returnType, 
                body: ParseBlock(Next(), oneStatement: false));
        }

        private ast.FnCall ParseFnCall(Token fnId)
        {
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

            return new ast.FnCall(block!, fnId)
            {
                Args = args,
            };
        }

        private ast.FnCall ConvertAlgExprToFunction(Expr l, Token op, Expr r)
        {
            var opFn = op;
            opFn.Text = "op" + opFn.Text;
            var term1 = new ast.FnCall(block!, opFn);
            term1.Args.Add(l);
            term1.Args.Add(r);
            return term1;
        }

        private ast.FnCall ConvertAlgExprToFunction(Token op, Expr expr)
        {
            var opFn = op;
            opFn.Text = "op" + opFn.Text;
            var term1 = new ast.FnCall(block!, opFn);
            term1.Args.Add(expr);
            return term1;
        }

        private ast.Expr ParseExpr(Token t, out Token la)
        {
            var l = ParseTerm(t, out var la1);

            while (la1.Id == TokenId.Op && (
                la1.Text == "+" || la1.Text == "-"))
            {
                // Interpret operators as functions:
                var r = ParseTerm(Next(), out var la2);
                l = ConvertAlgExprToFunction(l, la1, r);
                la1 = la2;
            }

            la = la1;
            return l;
        }

        private ast.Expr ParseTerm(Token t, out Token la)
        {
            var l = ParseFactor(t, out var la1);
            var term = l;

            while (la1.Id == TokenId.Op && (
                la1.Text == "/" || la1.Text == "*"))
            {
                // Interpret operators as functions:
                var r = ParseFactor(Next(), out var la2);
                term = ConvertAlgExprToFunction(term, la1, r);
                la1 = la2;
            }

            la = la1;
            return term;
        }

        private ast.Expr ParseFactor(Token t, out Token la)
        {
            var l = ParseExponential(t, out var la1);
            var term = l;

            while (la1.Id == TokenId.Op && la1.Text == "^")
            {
                // Interpret operators as functions:
                var r = ParseExponential(Next(), out var la2);
                term = ConvertAlgExprToFunction(term, la1, r);
                la1 = la2;
            }

            la = la1;
            return term;
        }

        private ast.Expr ParseExponential(Token t, out Token la)
        {
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
                    return new ast.VarExpr(block!, t);
                }
            }
            else if (t.Id == TokenId.Num)
            {
                // Number literals.
                la = Next();
                return new ast.NumExpr(block!, t);
            }
            else if (t.Id == TokenId.Str)
            {
                // String literals.
                la = Next();
                return new ast.StrExpr(block!, t);
            }
            else if (t.Id == TokenId.Op && t.Text == "-")
            {
                // Unary negation, interpret as a negation function.
                var toNegate = ParseFactor(Next(), out var la1);
                la = la1;
                return ConvertAlgExprToFunction(t, toNegate);
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
            else if (t.Id == TokenId.Bool)
            {
                // Boolean literal (true/false).
                la = Next();
                return new ast.BoolExpr(block!, t);
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
}