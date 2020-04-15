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
        private bool AllowNamedArgs = false;

        public ast.Program Parse(IEnumerator<Token> tokens)
        {
            this.tokens = tokens;
            
            var t = Next();
            if (t.Id != TokenId.Begin)
                throw new InvalidOperationException();

            var body = ParseBlock(Next(), oneStatement: false);
            var ast = new ast.Program(body);

            t = Next();
            if (t.Id != TokenId.Eof)
                throw new ParseError(t, "Expected end of file");

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
                    // Could be an assignment, member accessor, or function call.
                    var expr = ParseMemberAccess(t, out var t1);

                    if (t1.Id == TokenId.Semi)
                    {
                        // Expression statement.
                        var stmt = new ast.ExprStmt(block!, expr);
                        block!.Stmts.Add(stmt);
                    }
                    else if (t1.Id == TokenId.Assign)
                    {
                        // Reassignment.
                        var rhs = ParseExpr(Next(), out var la);
                        if (la.Id != TokenId.Semi)
                            throw new ParseError(la, "Expected ';'");

                        var assn = new ast.Assn(block!, t, rhs);
                        block!.Stmts.Add(assn);
                    }
                    else
                        throw new ParseError(t, "Expected ':=' or '('");
                }
                else if (t.Id == TokenId.Let)
                {
                    // Declaration (let statement).
                    var tId = Next();
                    
                    if (tId.Id != TokenId.Id)
                        throw new ParseError(tId, "Expected identifier");

                    // Check mutable (:=) or immutable (=).
                    var tOp = Next();
                    var mutable = false;
                    if (tOp.Id == TokenId.Assign)
                        mutable = true;
                    else if (tOp.Id == TokenId.Eq)
                        mutable = false;
                    else
                        throw new ParseError(tOp, "Expected '=' or ':='");

                    var rhs = ParseExpr(Next(), out var la);
                    if (la.Id != TokenId.Semi)
                        throw new ParseError(la, "Expected ';'");

                    var let = new ast.LetStmt(block!, tId, rhs, mutable);
                    block!.Stmts.Add(let);
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

                    var cond = ParseExpr(t, out var la);

                    Block body;
                    if (la.Id == TokenId.Begin)
                        body = ParseBlock(Next(), oneStatement: false);
                    else if (la.Id == TokenId.Then)
                        body = ParseBlock(Next(), oneStatement: true);
                    else
                        throw new ParseError(la, "Expected 'then' or '{'");

                    block!.Stmts.Add(new ast.IfStmt(
                        block!, ifToken, cond, body
                    ));
                }
                else if (t.Id == TokenId.Unless)
                {
                    var unlessToken = t;

                    // Unless statement.
                    t = Next();

                    var cond = ParseExpr(t, out var la);

                    Block body;
                    if (la.Id == TokenId.Begin)
                        body = ParseBlock(Next(), oneStatement: false);
                    else if (la.Id == TokenId.Then)
                        body = ParseBlock(Next(), oneStatement: true);
                    else
                        throw new ParseError(la, "Expected 'then' or '{'");

                    block!.Stmts.Add(new ast.UnlessStmt(
                        block!, unlessToken, cond, body
                    ));
                }
                else if (t.Id == TokenId.Else)
                {
                    var elseToken = t;

                    // Else statement.
                    t = Next();

                    Block body;
                    if (t.Id == TokenId.Begin)
                        body = ParseBlock(Next(), oneStatement: false);
                    else
                        body = ParseBlock(t, oneStatement: true);

                    block!.Stmts.Add(new ast.ElseStmt(
                        block!, elseToken, body
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
                tId.Text += t.Id;
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

                // Look for a mutable borrow or a take indicator:
                var passBy = PassedBy.ImmutableBorrow;
                var postMod = Next();
                
                Token argId;
                // If the next symbol is an ID, this is an immutable borrow:
                if (postMod.Id != TokenId.Id)
                {
                    if (postMod.Id == TokenId.Amp)
                    {
                        // Mutable borrow:
                        passBy = PassedBy.MutableBorrow;
                    }
                    else
                        throw new ParseError(postMod, "Expected identifer or '&'");

                    argId = Next();
                }
                else
                    argId = postMod;

                if (argId.Id != TokenId.Id)
                    throw new ParseError(argId, "Expected identifier");
                if (!argIds.Add(argId.Text))
                    throw new ParseError(argId, "Argument already defined");

                parameters.Add(new compiler2.ast.Param(block!, typeSpec, passBy, argId));

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
            AllowNamedArgs = true;
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

            AllowNamedArgs = false;
            return new ast.FnCall(block!, fnId)
            {
                Args = args,
            };
        }

        private ast.FnCall ConvertOpExprToFunctionCall(Expr l, Token op, Expr r)
        {
            var opFn = op;
            opFn.Text = "op" + op.Id;
            var term1 = new ast.FnCall(block!, opFn);
            term1.Args.Add(l);
            term1.Args.Add(r);
            return term1;
        }

        private ast.FnCall ConvertOpExprToFunctionCall(Token op, Expr expr)
        {
            var opFn = op;
            opFn.Text = "op" + op.Id;
            var term1 = new ast.FnCall(block!, opFn);
            term1.Args.Add(expr);
            return term1;
        }

        private ast.Expr ParseExpr(Token t, out Token la)
        {
            var l = ParseConjunction(t, out var la1);

            while (la1.Id == TokenId.DoublePipe)
            {
                var r = ParseConjunction(Next(), out var la2);
                l = new ast.Disjunction(block!, l, r);
                la1 = la2;
            }

            la = la1;
            return l;
        }

        private ast.Expr ParseConjunction(Token t, out Token la)
        {
            var l = ParseComparisonOrIdentity(t, out var la1);

            while (la1.Id == TokenId.DoubleAmp)
            {
                var r = ParseComparisonOrIdentity(Next(), out var la2);
                l = new ast.Conjunction(block!, l, r);
                la1 = la2;
            }

            la = la1;
            return l;
        }

        private ast.Expr ParseComparisonOrIdentity(Token t, out Token la)
        {
            var l = ParseAlgebraicExpr(t, out var la1);

            if (la1.Id == TokenId.Eq || la1.Id == TokenId.NotEq || 
                la1.Id == TokenId.LAngle || la1.Id == TokenId.RAngle || 
                la1.Id == TokenId.LAngleEq || la1.Id == TokenId.RAngleEq)
            {
                var r = ParseAlgebraicExpr(Next(), out var la2);
                l = ConvertOpExprToFunctionCall(l, la1, r);
                la1 = la2;
            }

            la = la1;
            return l;
        }

        private ast.Expr ParseAlgebraicExpr(Token t, out Token la)
        {
            var l = ParseTerm(t, out var la1);

            while (la1.Id == TokenId.Plus || la1.Id == TokenId.Minus)
            {
                var r = ParseTerm(Next(), out var la2);
                l = ConvertOpExprToFunctionCall(l, la1, r);
                la1 = la2;
            }

            la = la1;
            return l;
        }

        private ast.Expr ParseTerm(Token t, out Token la)
        {
            var l = ParseFactor(t, out var la1);
            var term = l;

            while (la1.Id == TokenId.FSlash || la1.Id == TokenId.Star)
            {
                var r = ParseFactor(Next(), out var la2);
                term = ConvertOpExprToFunctionCall(term, la1, r);
                la1 = la2;
            }

            la = la1;
            return term;
        }

        private ast.Expr ParseFactor(Token t, out Token la)
        {
            var l = ParseExponential(t, out var la1);
            var term = l;

            while (la1.Id == TokenId.Caret)
            {
                var r = ParseExponential(Next(), out var la2);
                term = ConvertOpExprToFunctionCall(term, la1, r);
                la1 = la2;
            }

            la = la1;
            return term;
        }

        private ast.Expr ParseMemberAccess(Token t, out Token la)
        {
            var l = ParseExpr(t, out var la1);

            if (la1.Id == TokenId.Dot)
            {
                var r = ParseExpr(Next(), out var la2);
                la = la2;
                return new MemberAccessExpr(block!, l, la1, r);
            }
            else
            {
                la = la1;
                return l;
            }
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
                    VerifyExpressionFollow(la);
                    return fnCall;
                }
                else if (t1.Id == TokenId.Colon)
                {
                    if (!AllowNamedArgs)
                        throw new ParseError(t1, "Named arguments not allowed in this context");

                    // Named argument.
                    var val = ParseExpr(Next(), out la);
                    val.NamedArg = t;
                    return val;
                }
                else
                {
                    // Variable or member access of some kind.
                    la = t1;
                    VerifyExpressionFollow(la);
                    return new ast.VarExpr(block!, t);
                }
            }
            else if (t.Id == TokenId.Num)
            {
                // Number literals.
                la = Next();
                VerifyExpressionFollow(la);
                return new ast.NumExpr(block!, t);
            }
            else if (t.Id == TokenId.Str)
            {
                // String literals.
                la = Next();
                VerifyExpressionFollow(la);
                return new ast.StrExpr(block!, t);
            }
            else if (t.Id == TokenId.Minus)
            {
                // Unary negation, interpret as a negation function.
                var toNegate = ParseFactor(Next(), out var la1);
                la = la1;
                VerifyExpressionFollow(la);
                return ConvertOpExprToFunctionCall(t, toNegate);
            }
            else if (t.Id == TokenId.Exclam)
            {
                // Boolean negation.
                var toNegate = ParseFactor(Next(), out var la1);
                la = la1;
                VerifyExpressionFollow(la);
                return new ast.BooleanNegated(block!, t, toNegate);
            }
            else if (t.Id == TokenId.LParen)
            {
                // Parenthetical expression.
                var expr = ParseExpr(Next(), out var la1);

                if (la1.Id != TokenId.RParen)
                    throw new ParseError(la1, "Expected ')'");
                
                la = Next();
                VerifyExpressionFollow(la);
                return expr;
            }
            else if (t.Id == TokenId.Bool)
            {
                // Boolean literal (true/false).
                la = Next();
                VerifyExpressionFollow(la);
                return new ast.BoolExpr(block!, t);
            }
            else
                throw new ParseError(t, "Expected identifier or literal");
        }

        private void VerifyExpressionFollow(Token t1)
        {
            if (t1.Id == TokenId.Amp || 
                t1.Id == TokenId.DoubleAmp || 
                t1.Id == TokenId.Pipe || 
                t1.Id == TokenId.DoublePipe || 
                t1.Id == TokenId.Caret || 
                t1.Id == TokenId.Comma || 
                t1.Id == TokenId.Semi || 
                t1.Id == TokenId.Then || 
                t1.Id == TokenId.Star || 
                t1.Id == TokenId.Plus || 
                t1.Id == TokenId.Minus || 
                t1.Id == TokenId.FSlash || 
                t1.Id == TokenId.RParen || 
                t1.Id == TokenId.Dot) {}
            else
                throw new ParseError(t1, "Unexpected expression element");
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
            $"Line {t.Line}, Col {t.Col}, Token {t}:\n{message}") { }
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