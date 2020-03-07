#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace compiler2
{
    public class CompileError : Exception 
    {
        public CompileError(Token t, string? message = null) : base(
            $"Line {t.Line}, Col {t.Col}:\n{message}") { }
    }

    public class Compiler
    {
        public const string Prefix = "__ZERM__";

        public static ast.Program Ast = new ast.Program();

        public void Write(ast.Program ast, StreamWriter stream)
        {
            Ast = ast;

            try
            {
                ast.Emit(stream);
            }
            catch (CompileError err)
            {
                Console.WriteLine("Error compiling the program: ");
                Console.WriteLine(err.ToString());
            }
        }
    }
}

namespace compiler2.ast
{
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
                    foreach (var decl in Body.FnDecls)
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

        public void Emit(StreamWriter stream)
        {
            if (!Success) return;

            // Emit a preamble to wrap C++:
            stream.Write(
@"#include <iostream>
typedef int __ZERM__CInt32;
");
                
            Body?.Emit(stream);

            stream.Write(
                "\nint main(){__ZERM__main__();}");
        }
    }

    public class Block
    {
        public Token Token;

        public HashSet<FnDecl> FnDecls = new HashSet<ast.FnDecl>();
        public List<Stmt> Stmts = new List<ast.Stmt>();
        public Block? Parent;

        public Block(Token token, Block? parent)
        {
            Parent = parent;
        }

        public override string ToString()
        {
            return $"[Bl:{Token}]";
        }

        public void Emit(StreamWriter stream)
        {
            foreach (var decl in FnDecls)
                decl.EmitForwardDeclaration(stream);
            foreach (var decl in FnDecls)
                decl.EmitImplementation(stream);
            foreach (var stmt in Stmts)
                stmt.Emit(stream);
        }

        public FnDecl FindFn(FnCall call)
        {
            Console.WriteLine($"FindFn: {this}, {call}");

            var matches = FnDecls.Where(x => x.Id.Text == call.Id.Text).ToList();
            if (matches.Count == 0)
            {
                if (Parent == null)
                    throw new CompileError(call.Token,
                        $"No matching function for {call}");

                return Parent.FindFn(call);
            }
            else if (matches.Count > 1)
            {
                // TODO: do signature matching.
                throw new NotImplementedException();
            }
            else
                return (FnDecl)matches[0];
        }
    }

    public class FnDecl : IShowable
    {
        public TypeSpec? ReturnType = null;
        public List<Param> Params = new List<compiler2.ast.Param>();
        public Block? Body = null;
        public Token Id;
        public Block Block;

        public FnDecl(Block block, Token id)
        {
            Block = block;
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

        public void Show()
        {
            Printer.Print(ToString());
            if (Body != null)
            {
                Printer.Promote();
                foreach (var decl in Body.FnDecls)
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

        private void EmitPrototype(StreamWriter stream)
        {
            // Emit return type:
            if (ReturnType == null)
                stream.Write("void");
            else
                ReturnType.Emit(stream);
            stream.Write(' ');

            // Append __ZERM__ to the front of the identifier 
            // and also a return suffix because our languages counts 
            // return type in a function signature.
            stream.Write("__ZERM__");
            stream.Write(Id.Text);
            stream.Write("__");
            stream.Write(ReturnType?.ToString());
            stream.Write('(');

            foreach (var param in Params.Take(Params.Count - 1))
            {
                param.Emit(stream);
                stream.Write(',');
            }

            if (Params.Count > 0)
                Params.Last().Emit(stream);

            stream.Write(')');
        }

        public void EmitForwardDeclaration(StreamWriter stream)
        {
            EmitPrototype(stream);
            stream.WriteLine(";\n");
        }

        public void EmitImplementation(StreamWriter stream)
        {
            EmitPrototype(stream);
            stream.WriteLine('{');
            Body?.Emit(stream);
            stream.WriteLine("}\n");
        }
    }

    public abstract class TypeSpec : IShowable
    {
        abstract public void Show();

        public abstract void Emit(StreamWriter stream);
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

        public override void Emit(StreamWriter stream)
        {
            // Immutable borrow => const __ZERM__Type&
            stream.Write("const ");
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
            stream.Write('&');
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

        public void Emit(StreamWriter stream)
        {
            Type.Emit(stream);
            stream.Write(' ');
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
        }
    }

    public abstract class Stmt : IShowable
    {
        public Block Block;

        public Stmt(Block block)
        {
            Block = block;
        }

        abstract public void Show();

        public abstract void Emit(StreamWriter stream);
    }

    public class ExprStmt : Stmt
    {
        public Expr Expr;

        public ExprStmt(Block block, Expr expr) : base(block)
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

        public override void Emit(StreamWriter stream)
        {
            Expr.Emit(stream);
        }
    }

    public abstract class Expr : IShowable
    {
        public Block Block;

        public Expr(Block block)
        {
            Block = block;
        }

        public abstract Token Token { get; }

        public abstract void Show();

        public abstract void Emit(StreamWriter stream);
    }

    public class FnCall : Expr
    {
        public Token Id;
        public List<Expr> Args = new List<ast.Expr>();

        public override Token Token => Id;

        public FnCall(Block block, Token id) : base(block)
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

        public override void Emit(StreamWriter stream)
        {
            // Write function identifier:
            if (Id.Text.StartsWith('#'))
            {
                if (Id.Text == "#cpp")
                {
                    // Writes C++ code directly, like a macro.
                    foreach (var arg in Args)
                    {
                        if (arg is StrExpr expr)
                            stream.Write(
                                expr.Value.Text.Substring(1, expr.Value.Text.Length - 2));
                        else
                        {
                            throw new CompileError(arg.Token,
                                "#cpp only takes string arguments.");
                        }
                    }
                }
            }
            else
            {
                // Try to find a matching function:
                var fn = Block.FindFn(this);

                stream.Write(Compiler.Prefix);
                stream.Write(Id.Text);
                // Functions also have suffixes relating to return type:
                stream.Write("__");
                stream.Write(fn.ReturnType?.ToString());

                // Write function arguments:
                stream.Write('(');
                foreach (var arg in Args.Take(Args.Count - 1))
                {
                    arg.Emit(stream);
                    stream.Write(',');
                }

                if (Args.Count > 0)
                    Args.Last().Emit(stream);

                stream.Write(");");
            }
        }
    }
    
    public class NumExpr : Expr
    {
        public Token Value;
        
        public override Token Token => Value;

        public NumExpr(Block block, Token value) : base(block)
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

        public override void Emit(StreamWriter stream)
        {
            if (Value.Text.EndsWith("ci32"))
            {
                // CInt32
                stream.Write("(int)");
                stream.Write(Value.Text.Substring(0, Value.Text.Length - 4));
            }
            else
                throw new NotImplementedException();
        }
    }
    
    public class VarExpr : Expr
    {
        public Token Value;

        public override Token Token => Value;

        public VarExpr(Block block, Token value) : base(block)
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

        public override void Emit(StreamWriter stream)
        {
            throw new NotImplementedException();
        }
    }

    public class StrExpr : Expr
    {
        public Token Value;

        public override Token Token => Value;

        public StrExpr(Block block, Token value) : base(block)
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

        public override void Emit(StreamWriter stream)
        {
            stream.Write(Value.Text);
        }
    }

    public class AlgExpr : Expr
    {
        public Expr Lhs;
        public Token Op;
        public Expr Rhs;

        public override Token Token => Lhs.Token;

        public AlgExpr(Block block, Expr lhs, Token op, Expr rhs) : base(block)
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

        public override void Emit(StreamWriter stream)
        {
            stream.Write('(');
            Lhs.Emit(stream);
            switch (Op.Text)
            {
                case "+":
                    stream.Write('+'); break;
                case "-":
                    stream.Write('-'); break;
                case "*":
                    stream.Write('*'); break;
            }
            Rhs.Emit(stream);
            stream.Write(')');
        }
    }

    public class Assn : Stmt
    {
        public Token Id;
        public Expr Value;

        public Assn(Block block, Token id, Expr value) : base(block)
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

        public override void Emit(StreamWriter stream)
        {
            throw new NotImplementedException();
        }
    }
}