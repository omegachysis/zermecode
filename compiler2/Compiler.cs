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
#include <math.h>
#include <string>
");
                
            Body?.EmitDecl(stream);
            Body?.EmitImpl(stream);

            stream.Write(
                $"\nint main(){{{Compiler.Prefix}main__();}}");
        }
    }

    public struct VarDecl
    {
        public Token Id;
        public string CName;
        public TypeSpec TypeSpec;
        public bool Mutable;

        public VarDecl(Block block, Token id, TypeSpec typeSpec, bool mutable)
        {
            if (block.FindVar(new VarExpr(block, id), throws: false) != null)
                CName = "x" + block.Vars[id.Text].CName;
            else
                CName = Compiler.Prefix + id.Text;

            Id = id;
            TypeSpec = typeSpec;
            Mutable = mutable;
            block.Vars[id.Text] = this;
        }
    }

    public class Block
    {
        public Token Token;

        public HashSet<TypeDecl> TypeDecls = new HashSet<ast.TypeDecl>();
        public HashSet<FnDecl> FnDecls = new HashSet<ast.FnDecl>();
        public List<Stmt> Stmts = new List<ast.Stmt>();
        public Block? Parent;

        /// <summary>
        /// Refers to the parent declaration if this is a function or a type decl.
        /// </summary>
        public Decl? ParentDecl = null;

        public Dictionary<string, VarDecl> Vars = 
            new Dictionary<string, VarDecl>();

        public Block(Token token, Block? parent)
        {
            Parent = parent;
        }

        public override string ToString()
        {
            return $"[Bl:{Token}]";
        }

        public void EmitDecl(StreamWriter stream)
        {
            // If this is the global block, don't allow statements.
            if (Parent == null && Stmts.Count > 0)
                throw new CompileError(Stmts.First().Token,
                $"Statements not allowed in the global block");

            var typeDeclIds = new HashSet<string>();
            foreach (var decl in TypeDecls)
            {
                typeDeclIds.Add(decl.Id.Text);
                decl.EmitDecl(stream);
            }

            foreach (var decl in FnDecls)
            {
                if (typeDeclIds.Contains(decl.Id.Text))
                {
                    throw new CompileError(decl.Id, 
                    $"Type already declared in this scope with ID '{decl.Id.Text}'");
                }

                decl.EmitForwardDecl(stream);
            }
        }

        public void EmitImpl(StreamWriter stream) 
        {
            // If this is the global block, don't allow statements.
            if (Parent == null && Stmts.Count > 0)
                throw new CompileError(Stmts.First().Token,
                $"Statements not allowed in the global block");

            foreach (var decl in TypeDecls)
                decl.EmitImpl(stream);

            foreach (var decl in FnDecls)
                decl.EmitImpl(stream);
            foreach (var stmt in Stmts)
                stmt.Emit(stream);
        }

        public VarDecl? FindVar(VarExpr expr, bool throws)
        {
            var matches = Vars.Where(x => x.Key == expr.Token.Text).ToArray();

            if (matches.Length == 0)
            {
                if (Parent == null)
                {
                    if (throws)
                        throw new CompileError(expr.Token,
                            $"Unknown variable {expr.Token}");
                    else 
                        return null;
                }

                return Parent.FindVar(expr, throws);
            }
            else
                return matches.Single().Value;
        }

        public TypeDecl FindType(TypeSpec spec)
        {
            var matches = TypeDecls.Where(x => x.Id.Text == spec.Token.Text).ToArray();
            if (matches.Length == 0)
            {
                if (Parent == null)
                    throw new CompileError(spec.Token,
                        $"No matching type for {spec}");

                return Parent.FindType(spec);
            }
            else
                return matches.Single();
        }

        public FnDecl FindFn(FnCall call)
        {
            var matches = FindMatchingSignatures(call).ToArray();
            if (matches.Length == 0)
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
            var matches = FnDecls.Where(x => x.Id.Text == call.Id.Text).ToArray();
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

                        var paramType = FindType(param.Type);
                        if (paramType != arg.TypeDecl)
                        {
                            all = false;
                            break;
                        }
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
        public Block Body;

        public TypeDecl(Block block, Token id, Block body) : base(block, id) 
        {
            Body = body;
            Body.ParentDecl = this;
        }

        public override bool Equals(object? obj)
        {
            return obj is TypeDecl decl &&
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

        public void EmitDecl(StreamWriter stream)
        {
            stream.Write("struct ");
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);

            stream.WriteLine('{');
            Body?.EmitDecl(stream);
            stream.WriteLine("};\n");
        }

        public void EmitImpl(StreamWriter stream)
        {
            Body?.EmitImpl(stream);
        }
    }

    public class FnDecl : Decl
    {
        public readonly TypeSpec? ReturnType;
        public readonly List<Param> Params;
        public readonly Block? Body = null;

        public FnDecl(Block block, Token id, 
            List<Param> parameters, 
            TypeSpec? returnType, Block body) : base(block, id) 
        {
            Params = parameters;
            ReturnType = returnType;
            Body = body;
            Body.ParentDecl = this;

            // Give the body of this function variable declarations for the parameters.
            foreach (var param in parameters)
            {
                new VarDecl(Body, param.Id, param.Type, mutable: false);
            }
        }

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

        public void EmitForwardDecl(StreamWriter stream)
        {
            EmitPrototype(stream);
            stream.WriteLine(";\n");
        }

        public void EmitImpl(StreamWriter stream)
        {
            EmitPrototype(stream);
            stream.WriteLine('{');
            Body?.EmitImpl(stream);
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
            stream.WriteLine(";");
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
                return Block.FindType(fn.ReturnType);
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

    public enum NumLiteralType
    {
        Int,
        Rat,
        Float32,
        Int32,
    }
    
    public class NumExpr : Expr
    {
        public Token Value;
        
        public override Token Token => Value;

        public readonly NumLiteralType LitType;

        public override TypeDecl TypeDecl
        {
            get
            {
                if (LitType == NumLiteralType.Int)
                {
                    var literal = Value;
                    literal.Text = "Int";
                    return Block.FindType(new SimpleTypeSpec(literal));
                }
                else if (LitType == NumLiteralType.Rat)
                {
                    var literal = Value;
                    literal.Text = "Rat";
                    return Block.FindType(new SimpleTypeSpec(literal));
                }
                else if (LitType == NumLiteralType.Float32)
                {
                    var literal = Value;
                    literal.Text = "Float32";
                    return Block.FindType(new SimpleTypeSpec(literal));
                }
                else 
                    throw new NotImplementedException();
            }
        }

        public NumExpr(Block block, Token value) : base(block)
        {
            Value = value;
            
            if (value.Text.EndsWith("f"))
                LitType = NumLiteralType.Float32;
            else if (value.Text.Contains('.') || value.Text.EndsWith("r"))
                LitType = NumLiteralType.Rat;
            else if (value.Text.EndsWith("i32"))
                LitType = NumLiteralType.Int32;
            else
                LitType = NumLiteralType.Int;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[N:{LitType} {Value.Text}]";
        }

        public override void Emit(StreamWriter stream)
        {
            if (LitType == NumLiteralType.Int)
            {
                // _ZRM_Int("str", base)
                stream.Write(Compiler.Prefix); stream.Write("Int(\"");
                stream.Write(Value.Text.Replace("_", ""));
                stream.Write("\",10)");
            }
            else if (LitType == NumLiteralType.Rat)
            {
                // _ZRM_Rat("str", base)
                var text = Value.Text.Replace("r", "");
                if (text.Contains('.'))
                {
                    // e.g. 0.1, index of '.' is 1, so 10^1 goes on the denom.
                    var denom = "1" + new string('0', text.IndexOf('.'));
                    text = text.Replace(".", "") + "/" + denom;
                }

                stream.Write(Compiler.Prefix); stream.Write("Rat(\"");
                stream.Write(text);
                stream.Write("\",10)");
            }
            else if (LitType == NumLiteralType.Float32)
            {
                // _ZRM_Float32(val)
                stream.Write(Compiler.Prefix); stream.Write("Float32(");
                var text = Value.Text.Replace("_", "");
                if (!text.Contains('.'))
                    text += ".0";
                stream.Write(text.Replace("f", ""));
                stream.Write("f)");
            }
            else if (LitType == NumLiteralType.Int32)
            {
                // _ZRM_Int32(val)
                throw new NotImplementedException();
            }
            else 
                throw new NotImplementedException();   
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
                var varDecl = Block.FindVar(this, throws: true)!.Value;
                return Block.FindType(varDecl.TypeSpec);
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
            var varDecl = Block.FindVar(this, throws: true)!.Value;
            stream.Write(varDecl.CName);
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
                return Block.FindType(new SimpleTypeSpec(literal));
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
        public readonly Token Id;
        public readonly Expr Value;
        public readonly bool Mutable;

        public override Token Token => Id;

        public Assn(Block block, Token id, Expr value, bool mutable) : base(block)
        {
            Id = id;
            Value = value;
            Mutable = mutable;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            var op = Mutable ? ":=" : "=";
            return $"[A:{Id.Text} {op} {Value}]";
        }

        public override void Emit(StreamWriter stream)
        {
            var decl = Block.FindVar(
                new VarExpr(Block, Id), throws: false
            );

            if (decl.HasValue)
            {
                // We need to call the destructor on the old 
                // value first.
                throw new NotImplementedException();
            }

            // Declaration of a variable.
            var typeSpec = new SimpleTypeSpec(Value.TypeDecl.Id);
            decl = new VarDecl(Block, Id, typeSpec, mutable: false);
            if (!Mutable)
                stream.Write("const ");
            decl.Value.TypeSpec.Emit(stream);
            stream.Write(' ');
            stream.Write(decl.Value.CName);
            stream.Write('=');
            Value.Emit(stream);
            stream.WriteLine(';');
        }
    }

    public class ReturnStmt : Stmt
    {
        public Expr Value;

        public override Token Token => Value.Token;

        public ReturnStmt(Block block, Expr value) : base(block)
        {
            Value = value;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[Ret:{Value}]";
        }

        public override void Emit(StreamWriter stream)
        {
            stream.Write("return ");
            Value.Emit(stream);
            stream.WriteLine(';');
        }
    }
}