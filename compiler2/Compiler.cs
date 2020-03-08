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
            $"Line {t.Line}, Col {t.Col}\n{message}") { }
    }

    public class Compiler
    {
        public const string Prefix = "_ZRM_";

        public static ast.Program Ast = new ast.Program();

        public bool Write(ast.Program ast, StreamWriter stream)
        {
            Ast = ast;
            if (!ast.Success)
                return false;

            try
            {
                ast.Emit(stream);
                return true;
            }
            catch (CompileError err)
            {
                Console.WriteLine("Error compiling the program: ");
                Console.WriteLine(err.ToString());
                return false;
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
$@"#include <iostream>
#include <gmp.h>
#include <string>
typedef int {Compiler.Prefix}__int;
");
                
            Body?.Emit(stream);

            stream.Write(
                $"\nint main(){{{Compiler.Prefix}main__();}}");
        }
    }

    public class Block
    {
        public Token Token;

        public HashSet<TypeDecl> TypeDecls = new HashSet<ast.TypeDecl>();
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
            // If this is the global block, don't allow statements.
            if (Parent == null && Stmts.Count > 0)
                throw new CompileError(Stmts.First().Token,
                $"Statements not allowed in the global block");

            var typeDeclIds = new HashSet<string>();
            foreach (var decl in TypeDecls)
            {
                typeDeclIds.Add(decl.Id.Text);
                decl.EmitForwardDeclaration(stream);
            }
            // foreach (var decl in TypeDecls)
            //     decl.EmitImplementation(stream);

            foreach (var decl in FnDecls)
            {
                if (typeDeclIds.Contains(decl.Id.Text))
                {
                    throw new CompileError(decl.Id, 
                    $"Type already declared in this scope with ID '{decl.Id.Text}'");
                }

                decl.EmitForwardDeclaration(stream);
            }
            foreach (var decl in FnDecls)
                decl.EmitImplementation(stream);
            foreach (var stmt in Stmts)
                stmt.Emit(stream);
        }

        public TypeDecl FindType(Token id)
        {
            Console.WriteLine($"FindType: {this}, {id}");

            var matches = TypeDecls.Where(x => x.Id.Text == id.Text).ToList();
            if (matches.Count == 0)
            {
                if (Parent == null)
                    throw new CompileError(id,
                        $"No matching type for {id}");

                return Parent.FindType(id);
            }
            else
                return matches.Single();
        }

        public FnDecl FindFn(FnCall call)
        {
            Console.WriteLine($"FindFn: {this}, {call}");

            var matches = FindMatchingSignatures(call).ToList();
            if (matches.Count == 0)
            {
                if (Parent == null)
                    throw new CompileError(call.Token,
                        $"No matching function for {call}");
                return Parent.FindFn(call);
            }
            
            return matches.Single();
        }

        public IEnumerable<FnDecl> FindMatchingSignatures(FnCall call)
        {
            var matches = FnDecls.Where(x => x.Id.Text == call.Id.Text).ToList();
            foreach (var fn in matches)
            {
                // Check parameter counts.
                if (fn.Params.Count == call.Args.Count)
                {
                    var all = true;

                    // Check parameter types.
                    for (int i = 0; i < fn.Params.Count; i++)
                    {
                        var param = fn.Params[i];
                        var arg = call.Args[i];

                        var paramType = FindType(param.Type.Token);
                        if (paramType != arg.TypeDecl)
                            all = false;
                    }

                    if (all)
                        yield return fn;
                }
            }
        }
    }

    public abstract class Decl : IShowable
    {
        public Token Id;
        public Block Block;

        public Decl(Block block, Token id)
        {
            Id = id;
            Block = block;
        }

        public abstract void Show();
    }

    public class TypeDecl : Decl
    {
        public Block? Body = null;

        public TypeDecl(Block block, Token id) : base(block, id) {}

        public override bool Equals(object? obj)
        {
            return obj is FnDecl decl &&
                Id.Text == decl.Id.Text;
        }

        public override int GetHashCode()
        {
            return Id.Text.GetHashCode();
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[TyD:{Id.Text}]";
        }

        public void EmitForwardDeclaration(StreamWriter stream)
        {
            stream.Write("struct ");
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);

            stream.WriteLine('{');
            Body?.Emit(stream);
            stream.WriteLine("};\n");
        }
    }

    public class FnDecl : Decl
    {
        public TypeSpec? ReturnType = null;
        public List<Param> Params = new List<compiler2.ast.Param>();
        public Block? Body = null;

        public FnDecl(Block block, Token id) : base(block, id) {}

        public override bool Equals(object? obj)
        {
            return obj is FnDecl decl &&
                Id.Text == decl.Id.Text && ReturnType == decl.ReturnType && 
                Params.Select(x => x.Type).SequenceEqual(decl.Params.Select(x => x.Type));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id.Text, ReturnType);
        }

        public override void Show()
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

        public static string ReplaceOperatorSymbols(string text)
        {
            return text.Replace('+','a').Replace('-','s')
                .Replace('*','m').Replace('/','d').Replace('^','e');
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
            stream.Write(Compiler.Prefix);
            stream.Write(ReplaceOperatorSymbols(Id.Text));
            stream.Write("__");
            ReturnType?.Emit(stream);
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
        public abstract Token Token { get; }

        abstract public void Show();

        public abstract void Emit(StreamWriter stream);
    }

    public class SimpleTypeSpec : TypeSpec
    {
        public Token Id;

        public override Token Token => Id;

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
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
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
            stream.Write("const ");
            Type.Emit(stream);
            stream.Write("& ");
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

        public abstract Token Token { get; }

        abstract public void Show();

        public abstract void Emit(StreamWriter stream);
    }

    public class ExprStmt : Stmt
    {
        public Expr Expr;

        public override Token Token => Expr.Token;

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
            stream.Write("\n;");
        }
    }

    public abstract class Expr : IShowable
    {
        public Block Block;

        public Expr(Block block)
        {
            Block = block;
        }

        public abstract TypeDecl TypeDecl { get; }

        public abstract Token Token { get; }

        public abstract void Show();

        public abstract void Emit(StreamWriter stream);
    }

    public class FnCall : Expr
    {
        public Token Id;
        public List<Expr> Args = new List<ast.Expr>();

        public override Token Token => Id;

        public FnDecl FnDecl => Block.FindFn(this);

        public override TypeDecl TypeDecl
        {
            get
            {
                if (Id.Text.StartsWith('#'))
                    throw new InvalidOperationException("Cannot get type decl of metafunction");
                
                var fn = FnDecl;
                if (fn.ReturnType is null)
                    throw new CompileError(Token, "Cannot use return 'void' function");
                return Block.FindType(fn.ReturnType.Token);
            }
        }

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
                Metafunctions.Emit(stream, this);
            }
            else
            {
                // Try to find a matching function:
                var fn = Block.FindFn(this);

                stream.Write(Compiler.Prefix);
                stream.Write(FnDecl.ReplaceOperatorSymbols(Id.Text));
                // Functions also have suffixes relating to return type:
                stream.Write("__");
                fn.ReturnType?.Emit(stream);

                // Write function arguments:
                stream.Write('(');
                foreach (var arg in Args.Take(Args.Count - 1))
                {
                    arg.Emit(stream);
                    stream.Write(',');
                }

                if (Args.Count > 0)
                    Args.Last().Emit(stream);

                stream.Write(")");
            }
        }
    }
    
    public class NumExpr : Expr
    {
        public Token Value;
        
        public override Token Token => Value;

        public override TypeDecl TypeDecl
        {
            get
            {
                // Int type.
                var literal = Value;
                literal.Text = "Int";
                return Block.FindType(literal);
            }
        }

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
            // Int type
            // _ZRM_Int("str", base)
            stream.Write(Compiler.Prefix);
            stream.Write("Int(\"");
            stream.Write(Value.Text.Replace("_", ""));
            stream.Write("\",10)");
        }
    }
    
    public class VarExpr : Expr
    {
        public Token Value;

        public override Token Token => Value;

        public override TypeDecl TypeDecl
        {
            get
            {
                throw new NotImplementedException();
            }
        }

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

        public override TypeDecl TypeDecl
        {
            get
            {
                var literal = Value;
                literal.Text = "String";
                return Block.FindType(literal);
            }
        }

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
            // C++ string wrapper:
            // __ZERM__String(<string>)
            stream.Write(Compiler.Prefix);
            stream.Write("String(");
            stream.Write(Value.Text);
            stream.Write(')');
        }
    }

    public class Assn : Stmt
    {
        public Token Id;
        public Expr Value;

        public override Token Token => Id;

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