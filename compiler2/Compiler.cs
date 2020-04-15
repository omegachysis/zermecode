#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace compiler2
{
    public class CompileError : Exception 
    {
        public CompileError(Token t, string message) : base(
            $"Line {t.Line}, Col {t.Col}\n{message}") { }

        public CompileError(string message) : base(
            $"{message}") { }
    }

    public class Compiler
    {
        public const string Prefix = "_ZRM_";

        public void Write(ast.Program ast, TranspilerStream stream)
        {
            if (ast.Body == null)
                throw new InvalidOperationException("AST is invalid");
            ast.Emit(stream);
        }
    }
}

namespace compiler2.ast
{
    public class Program : IShowable
    {
        public readonly Block? Body;

        public Program(Block? body)
        {
            Body = body;
        }

        public void Show()
        {
            if (Body != null)
            {
                Printer.Print("Program");
                Printer.Print("");
                Body.Show();
            }
            else
            {
                Console.WriteLine("ERROR COMPILING THE PROGRAM");
            }
        }

        public void Emit(TranspilerStream stream)
        {
            if (Body == null) return;

            // Emit a preamble to wrap C++:
            stream.Write(
$@"#include <iostream>
#include <gmp.h>
#include <math.h>
#include <string>
");
                
            Body.EmitDecl(stream);
            Body.EmitImpl(stream);

            // Check that there is a main function in the global scope.
            var mainFn = Body.FindFn(new FnCall(Body, new Token(TokenId.Id,
                "Main", 0, 0)));

            if (mainFn == null)
                throw new CompileError("No Main function found");

            stream.Write(
                $"\nint main(){{{Compiler.Prefix}Main__();}}");
        }
    }

    public struct VarDecl
    {
        public readonly Block Block;
        public readonly Token Id;
        public readonly string CName;
        public readonly TypeSpec TypeSpec;
        public readonly VarAccess AccessType;

        public TypeDecl TypeDecl
        {
            get
            {
                return Block.FindType(TypeSpec);
            }
        }

        public bool Mutable
        {
            get
            {
                return AccessType == VarAccess.MutableBorrow || 
                    AccessType == VarAccess.MutableLocal || 
                    AccessType == VarAccess.DynamicLocal ||
                    AccessType == VarAccess.DynamicTake;
            }
        }

        public bool Returnable
        {
            get
            {
                return AccessType == VarAccess.DynamicLocal || 
                    AccessType == VarAccess.DynamicTake || 
                    AccessType == VarAccess.ImmutableLocal || 
                    AccessType == VarAccess.MutableLocal;
            }
        }

        public VarDecl(Block block, Token id, TypeSpec typeSpec, VarAccess accessType)
        {
            if (block.FindVar(new VarExpr(block, id), throws: false) != null)
                CName = "x" + block.Vars[id.Text].CName;
            else
                CName = Compiler.Prefix + id.Text;

            Block = block;
            Id = id;
            TypeSpec = typeSpec;
            AccessType = accessType;
        }
    }

    public class Block : IShowable
    {
        public readonly Token Token;

        public readonly HashSet<TypeDecl> TypeDecls = new HashSet<ast.TypeDecl>();
        public readonly HashSet<FnDecl> FnDecls = new HashSet<ast.FnDecl>();
        public readonly List<Stmt> Stmts = new List<ast.Stmt>();
        public readonly Block? Parent;

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

        public void EmitDecl(TranspilerStream stream)
        {
            // If this is the global block, don't allow statements.
            if (Parent == null && Stmts.Count > 0)
                throw new CompileError(Stmts.First().Token,
                $"Statements not allowed in the global block");

            var typeDeclIds = new HashSet<string>();
            foreach (var decl in TypeDecls)
            {
                typeDeclIds.Add(decl.Id.Text);
                decl.EmitForwardDecl(stream);
            }

            // Some metafunctions should be emitted within the type declaration 
            // itself. Emit those now.
            if (ParentDecl is TypeDecl)
            {
                foreach (var stmt in Stmts)
                {
                    if (stmt is ExprStmt expr)
                    {
                        if (expr.Expr is FnCall call)
                        {
                            Metafunctions.Emit(stream, call, forward: true);
                        }
                    }
                }
            }

            foreach (var decl in TypeDecls)
                decl.EmitDecl(stream);

            foreach (var decl in FnDecls)
            {
                if (typeDeclIds.Contains(decl.Id.Text) && !decl.IsGeneratedConstructor)
                {
                    throw new CompileError(decl.Id, 
                    $"Type already declared in this scope with ID '{decl.Id.Text}'");
                }

                decl.EmitForwardDecl(stream);
            }
        }

        public void EmitImpl(TranspilerStream stream) 
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

        public Block Global()
        {
            if (Parent == null)
                return this;
            else
                return Parent.Global();
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

        public TypeDecl FindType(string id)
        {
            return FindType(new SimpleTypeSpec(new Token(TokenId.Id, id, 0, 0)));
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

        public FnDecl? FindFn(FnCall call, bool thisBlockOnly = false)
        {
            if (call.ParentAccess != null && !thisBlockOnly)
                return call.ParentAccess.Left.TypeDecl.Body.FindFn(call,
                    thisBlockOnly: true);

            var matches = FindMatchingSignatures(call).ToArray();
            if (matches.Length == 0)
            {
                if (Parent == null || thisBlockOnly)
                    return null;
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

                        if (param.TypeDecl != arg.TypeDecl)
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

        public void Show()
        {
            foreach (var decl in TypeDecls)
                decl.Show();
            foreach (var decl in FnDecls)
                decl.Show();
            foreach (var stmt in Stmts)
                stmt.Show();
        }
    }

    public abstract class Decl : IShowable
    {
        public readonly Token Id;
        public readonly Block Block;

        public Decl(Block block, Token id)
        {
            Id = id;
            Block = block;
        }

        public abstract void Show();
    }

    public class TypeDecl : Decl
    {
        public readonly Block Body;

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

        private void GenerateConstructor()
        {
            var fn = new FnDecl(block: Body, id: new Token(Id.Text),
                parameters: new List<Param>(),
                returnType: new SimpleTypeSpec(new Token(Id.Text)),
                body: new Block(Id, parent: Body));
            fn.IsGeneratedConstructor = true;
            Body.FnDecls.Add(fn);
        }

        public void EmitForwardDecl(TranspilerStream stream)
        {
            GenerateConstructor();

            stream.Write("struct ");
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
            stream.WriteLine(';');
        }

        public void EmitDecl(TranspilerStream stream)
        {
            stream.Write("struct ");
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);

            stream.WriteLine('{');
            Body?.EmitDecl(stream);
            stream.WriteLine("};\n");
        }

        public void EmitImpl(TranspilerStream stream)
        {
            Body?.EmitImpl(stream);
        }

        public void EmitSpec(TranspilerStream stream)
        {
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
        }
    }

    public class FnDecl : Decl
    {
        public readonly TypeSpec? ReturnType;
        public readonly List<Param> Params;
        public readonly Block? Body = null;
        public bool IsGeneratedConstructor = false;

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
                // Verify the type for each parameter is defined now:
                Block.FindType(param.TypeSpec);

                VarAccess varAccess;
                switch (param.PassedBy)
                {
                    case PassedBy.ImmutableBorrow:
                        varAccess = VarAccess.ImmutableBorrow;
                        break;
                    case PassedBy.MutableBorrow:
                        varAccess = VarAccess.MutableBorrow;
                        break;
                    case PassedBy.DynamicTake:
                        varAccess = VarAccess.DynamicTake;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                var varDecl = new VarDecl(Body, param.Id, param.TypeSpec, varAccess);
                Body.Vars.Add(varDecl.Id.Text, varDecl);
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is FnDecl decl &&
                Id.Text == decl.Id.Text && ReturnType == decl.ReturnType && 
                Params.Select(x => x.TypeDecl).SequenceEqual(
                    decl.Params.Select(x => x.TypeDecl));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id.Text, ReturnType);
        }

        public override void Show()
        {
            Printer.Print(ToString());
            Printer.Promote();
            Body?.Show();
            Printer.Demote();
        }

        public override string ToString()
        {
            var args = string.Join(',', Params.Select(x => x.ToString()));
            return $"[FnD:{Id.Text}({args}) -> {ReturnType}]";
        }

        private void EmitReturnType(TranspilerStream stream)
        {
            if (ReturnType == null)
                stream.Write("void");
            else
                ReturnType.Emit(stream);
        }

        private void EmitPrototype(TranspilerStream stream)
        {
            EmitReturnType(stream);
            stream.Write(' ');
            EmitIdAndParams(stream);
        }

        private void EmitIdAndParams(TranspilerStream stream)
        {
            // Append __ZERM__ to the front of the identifier 
            // and also a return suffix because our languages counts 
            // return type in a function signature.
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
            stream.Write("__");
            ReturnType?.Emit(stream);
            stream.Write('(');

            var isFirst = true;
            foreach (var param in Params)
            {
                if (!isFirst)
                    stream.Write(',');
                else
                    isFirst = false;
                param.Emit(stream);

            }

            stream.Write(')');
        }

        public void EmitForwardDecl(TranspilerStream stream)
        {
            EmitPrototype(stream);
            stream.WriteLine(';');
        }

        public void EmitImpl(TranspilerStream stream)
        {
            EmitReturnType(stream);
            stream.Write(' ');

            if (Block.ParentDecl is TypeDecl type)
            {
                // Member function
                type.EmitSpec(stream);
                stream.Write("::");
            }

            EmitIdAndParams(stream);
            stream.WriteLine('{');
            Body?.EmitImpl(stream);
            stream.WriteLine('}');
        }
    }

    public abstract class TypeSpec : IShowable
    {
        public abstract Token Token { get; }

        abstract public void Show();

        public abstract void Emit(TranspilerStream stream);
    }

    public class SimpleTypeSpec : TypeSpec
    {
        public readonly Token Id;

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

        public override void Emit(TranspilerStream stream)
        {
            // Immutable borrow => const __ZERM__Type&
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
        }
    }

    public class Param : IShowable
    {
        public readonly Block Block;
        public readonly TypeSpec TypeSpec;
        public readonly PassedBy PassedBy;
        public readonly Token Id;

        public TypeDecl TypeDecl
        {
            get
            {
                return Block.FindType(TypeSpec);
            }
        }

        public Param(Block block, TypeSpec typeSpec, PassedBy passedBy, Token id)
        {
            Block = block;
            TypeSpec = typeSpec;
            PassedBy = passedBy;
            Id = id;
        }

        public void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[P:{PassedBy} {TypeSpec} {Id.Text}]";
        }

        public void Emit(TranspilerStream stream)
        {
            if (PassedBy == PassedBy.ImmutableBorrow)
                stream.Write("const ");
            TypeSpec.Emit(stream);
            stream.Write("& ");
            stream.Write(Compiler.Prefix);
            stream.Write(Id.Text);
        }

        public bool CompatibleWith(Expr arg, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (arg is VarExpr varExpr)
            {
                if (!varExpr.VarDecl.Mutable && 
                    PassedBy == PassedBy.MutableBorrow)
                    errorMessage = "Cannot mutably borrow an immutable variable";
            }
            else if (PassedBy == PassedBy.MutableBorrow)
                errorMessage = "Cannot mutably borrow a non-variable expression";

            return errorMessage.Length == 0;
        }
    }

    public abstract class Stmt : IShowable
    {
        public readonly Block Block;

        public Stmt(Block block)
        {
            Block = block;
        }

        public abstract Token Token { get; }

        abstract public void Show();

        public abstract void Emit(TranspilerStream stream);
    }

    public class ExprStmt : Stmt
    {
        public readonly Expr Expr;

        public override Token Token => Expr.Token;

        public ExprStmt(Block block, Expr expr) : base(block)
        {
            if (!(expr is FnCall) && !(expr is MemberAccessExpr))
                throw new CompileError(
                    expr.Token, "Expression statement must be a function call or member access.");

            Expr = expr;
        }

        public override void Show()
        {
            Printer.Print(ToString());
            Printer.Promote();
            Expr.Show();
            Printer.Demote();
        }

        public override string ToString()
        {
            return $"[ExS:{Token}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            Expr.Emit(stream);
            stream.WriteLine(";");
        }
    }

    public abstract class Expr : IShowable
    {
        public readonly Block Block;
        
        public MemberAccessExpr? ParentAccess = null;
        public Token? NamedArg = null;

        public Expr(Block block)
        {
            Block = block;
        }

        public abstract TypeDecl TypeDecl { get; }

        public abstract Token Token { get; }

        public abstract void Show();

        public abstract void Emit(TranspilerStream stream);
    }

    public class Conjunction : Expr 
    {
        public readonly Expr Left;
        public readonly Expr Right;

        public override Token Token => Left.Token;

        public override TypeDecl TypeDecl => Block.FindType("Bool");

        public Conjunction(Block block, Expr left, Expr right) : base(block)
        {
            Left = left;
            Right = right;
        }

        public override void Emit(TranspilerStream stream)
        {
            stream.Write(Compiler.Prefix);
            stream.Write("Bool(");
            Left.Emit(stream);
            stream.Write(".val &&");
            Right.Emit(stream);
            stream.Write(".val");
            stream.Write(')');
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[{Left} && {Right}]";
        }
    }

    public class Disjunction : Expr 
    {
        public readonly Expr Left;
        public readonly Expr Right;

        public override Token Token => Left.Token;

        public override TypeDecl TypeDecl => Block.FindType("Bool");

        public Disjunction(Block block, Expr left, Expr right) : base(block)
        {
            Left = left;
            Right = right;
        }

        public override void Emit(TranspilerStream stream)
        {
            stream.Write(Compiler.Prefix);
            stream.Write("Bool(");
            Left.Emit(stream);
            stream.Write(".val ||");
            Right.Emit(stream);
            stream.Write(".val");
            stream.Write(')');
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[{Left} || {Right}]";
        }
    }

    public class BooleanNegated : Expr 
    {
        public readonly Token Op;
        public readonly Expr Value;

        public override Token Token => Value.Token;

        public override TypeDecl TypeDecl => Block.FindType("Bool");

        public BooleanNegated(Block block, Token op, Expr value) : base(block)
        {
            Op = op;
            Value = value;
        }

        public override void Emit(TranspilerStream stream)
        {
            stream.Write(Compiler.Prefix);
            stream.Write("Bool(!(");
            Value.Emit(stream);
            stream.Write(").val)");
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[! {Value}]";
        }
    }

    public class FnCall : Expr
    {
        public readonly Token Id;
        public List<Expr> Args = new List<ast.Expr>();

        public override Token Token => Id;

        public FnDecl FnDecl => Block.FindFn(this) ?? 
            throw new CompileError(Token, "No matching function found");

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
            var args = string.Join(',', Args.Select(
                x => {
                    if (x.NamedArg.HasValue)
                        return $"[NArg:{x.NamedArg.Value.Text}:{x}]";
                    else
                        return x.ToString();
                }
            ));
            return $"[FnC:{Id.Text}({args})]";
        }

        public override void Emit(TranspilerStream stream)
        {
            if (Id.Text.StartsWith('#'))
            {
                Metafunctions.Emit(stream, this, forward: false);
            }
            else
            {
                // Try to find a matching function:
                FnDecl? fn = null;
                if (ParentAccess != null)
                    fn = ParentAccess.Left.TypeDecl.Block.FindFn(this);
                else
                    fn = Block.FindFn(this);
                fn = fn ?? throw new CompileError(Token, "Cannot find matching function");

                // Verify that the expressions passed into the function 
                // match the pass-by-tyep of each argument.
                for (int i = 0; i < fn.Params.Count; i++)
                {
                    var param = fn.Params[i];
                    var arg = Args[i];
                    if (!param.CompatibleWith(arg, out var errorMessage))
                        throw new CompileError(arg.Token, errorMessage);
                }

                stream.Write(Compiler.Prefix);
                stream.Write(Id.Text);
                // Functions also have suffixes relating to return type:
                stream.Write("__");
                fn.ReturnType?.Emit(stream);

                // Write function arguments:
                stream.Write('(');

                var actualArgs = new Expr[Args.Count];

                // Process as named args to get a new order of arguments to pass in.
                var namedArgsStarted = false;
                var index = 0;
                var namedArgs = new HashSet<string>();
                foreach (var arg in Args)
                {
                    if (arg.NamedArg.HasValue)
                    {
                        namedArgsStarted = true;

                        if (!namedArgs.Add(arg.NamedArg.Value.Text))
                            throw new CompileError("Named argument already specified");

                        // Find the matching named parameter.
                        var paramIndex = fn.Params.FindIndex(
                            x => x.Id.Text == arg.NamedArg.Value.Text);

                        if (paramIndex == -1)
                            throw new CompileError("Named argument not found");
                        else
                        {
                            if (actualArgs[paramIndex] != null)
                                throw new CompileError("Argument already specified");
                            actualArgs[paramIndex] = arg;
                        }
                    }
                    else
                    {
                        if (namedArgsStarted)
                            throw new CompileError(
                                "Positional arguments cannot follow named arguments");

                        actualArgs[index] = arg;
                        index += 1;
                    }
                }

                // Check for missing arguments.
                if (actualArgs.Any(x => x == null))
                    throw new CompileError("Missing one or more required arguments");
                
                // Process the generated array of positional args.
                var firstArg = true;
                foreach (var arg in actualArgs)
                {
                    if (!firstArg)
                        stream.Write(',');
                    else
                        firstArg = false;

                    

                    arg.Emit(stream);
                }

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
        public readonly Token Value;
        public readonly NumLiteralType LitType;
        
        public override Token Token => Value;

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
            return $"[Num:{LitType} {Value.Text}]";
        }

        public override void Emit(TranspilerStream stream)
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
        public readonly Token Value;

        public override Token Token => Value;

        public VarDecl VarDecl
        {
            get
            {
                return Block.FindVar(this, throws: true)!.Value;
            }
        }

        public override TypeDecl TypeDecl
        {
            get
            {
                var varDecl = Block.FindVar(this, throws: true)!.Value;
                return varDecl.TypeDecl;
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
            return $"[Var:{Value.Text}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            var varDecl = Block.FindVar(this, throws: true)!.Value;
            stream.Write(varDecl.CName);
        }
    }

    public class StrExpr : Expr
    {
        public readonly Token Value;

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
            return $"[Str:{Value.Text}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            // C++ string wrapper:
            // __ZERM__String(<string>)
            stream.Write(Compiler.Prefix);
            stream.Write("String(");
            stream.Write(Value.Text);
            stream.Write(')');
        }
    }

    public class BoolExpr : Expr
    {
        public readonly Token Value;

        public override Token Token => Value;

        public override TypeDecl TypeDecl
        {
            get
            {
                var literal = Value;
                literal.Text = "Bool";
                return Block.FindType(new SimpleTypeSpec(literal));
            }
        }

        public BoolExpr(Block block, Token value) : base(block)
        {
            Value = value;
        }

        public override void Show()
        {
            Printer.Print(ToString());
        }

        public override string ToString()
        {
            return $"[Bool:{Value.Text}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            // C++ boolean wrapper:
            // __ZERM__Bool(true)
            // __ZERM__Bool(false)
            stream.Write(Compiler.Prefix);
            stream.Write("Bool(");
            stream.Write(Value.Text.ToLowerInvariant());
            stream.Write(')');
        }
    }

    public class MemberAccessExpr : Expr
    {
        public readonly Expr Left;
        public readonly Expr Right;

        public MemberAccessExpr(Block block, Expr left, Token dot, Expr right) : base(block)
        {
            Token = dot;
            Left = left;
            Right = right;
            Right.ParentAccess = this;
        }

        public override TypeDecl TypeDecl => Right.TypeDecl;

        public override Token Token { get; }

        public override void Emit(TranspilerStream stream)
        {
            Left.Emit(stream);
            stream.Write('.');
            Right.Emit(stream);
        }

        public override void Show()
        {
            Printer.Print(ToString());
            Printer.Promote();
            Left.Show();
            Right.Show();
            Printer.Demote();
        }

        public override string ToString()
        {
            return $"[MemAccess:{Token}]";
        }
    }

    public class Assn : Stmt
    {
        public readonly Token Id;
        public readonly Expr Value;

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
            return $"[Assn:{Id.Text} := {Value}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            var decl = Block.FindVar(
                new VarExpr(Block, Id), throws: false
            );

            if (!decl.HasValue)
                throw new CompileError(Id, "Reassignment of undeclared variable");

            if (!decl.Value.Mutable)
                throw new CompileError(Id, "Reassignment of immutable variable");

            // Reassign the value.
            stream.Write(decl.Value.CName);
            stream.Write('=');
            Value.Emit(stream);
            stream.WriteLine(';');
        }
    }

    public class LetStmt : Stmt
    {
        public readonly Token Id;
        public readonly Expr Value;
        public readonly bool Mutable;

        public override Token Token => Id;

        public LetStmt(Block block, Token id, Expr value, bool mutable) : base(block)
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
            return $"[Let:{Id.Text} {op} {Value}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            // Declaration of a variable.
            var typeSpec = new SimpleTypeSpec(Value.TypeDecl.Id);
            var decl = new VarDecl(Block, Id, typeSpec, 
                Mutable ? VarAccess.MutableLocal : VarAccess.ImmutableLocal);
            if (!Mutable)
                stream.Write("const ");
            decl.TypeSpec.Emit(stream);
            stream.Write(' ');
            stream.Write(decl.CName);
            stream.Write('=');
            Value.Emit(stream);
            stream.WriteLine(';');

            Block.Vars[decl.Id.Text] = decl;
        }
    }

    public class ReturnStmt : Stmt
    {
        public readonly Expr? Value;

        public override Token Token { get; }

        public ReturnStmt(Block block, Token returnKeyword, Expr? value) : base(block)
        {
            Token = returnKeyword;
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

        public override void Emit(TranspilerStream stream)
        {
            // Check that we are in a fn decl.
            if (Block.ParentDecl is FnDecl fn)
            {
                if (fn.ReturnType == null)
                {
                    if (Value != null)
                        throw new CompileError(Value.Token,
                            "Cannot return expression from 'void' procedure");
                }
                else
                {
                    if (Value == null)
                        throw new CompileError(Token, 
                            "Function must return a value");
                    else if (Value.TypeDecl != fn.Block.FindType(fn.ReturnType))
                        throw new CompileError(Value.Token,
                            "Expression does not match function's return type");
                }
            }
            else
                throw new CompileError(Token,
                    "Cannot return from outside a function");

            // Check that we are returning something that can be returned.
            if (Value is VarExpr varExpr)
            {
                if (!varExpr.VarDecl.Returnable)
                    throw new CompileError(Value.Token,
                        "Cannot return a borrowed variable");
            }

            stream.Write("return ");
            Value?.Emit(stream);
            stream.WriteLine(';');
        }
    }

    public class IfStmt : Stmt
    {
        public readonly Expr Condition;
        public readonly Block Body;

        public override Token Token { get; }

        public IfStmt(Block block, Token ifToken, Expr cond, Block body) : base(block)
        {
            Token = ifToken;
            Condition = cond;
            Body = body;
        }

        public override void Show()
        {
            Printer.Print(ToString());
            Printer.Promote();
            Body.Show();
            Printer.Demote();
        }

        public override string ToString()
        {
            return $"[If:{Condition}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            if (Condition.TypeDecl != Block.Global().FindType("Bool"))
                throw new CompileError(Condition.Token, 
                    "If conditional must be a boolean expression");

            stream.Write("if(");
            Condition.Emit(stream);
            stream.Write(".val");
            stream.WriteLine("){");

            Body.EmitDecl(stream);
            Body.EmitImpl(stream);

            stream.WriteLine("}");
        }
    }

    public class UnlessStmt : Stmt
    {
        public readonly Expr Condition;
        public readonly Block Body;

        public override Token Token { get; }

        public UnlessStmt(Block block, Token unlessToken, Expr cond, Block body) : base(block)
        {
            Token = unlessToken;
            Condition = cond;
            Body = body;
        }

        public override void Show()
        {
            Printer.Print(ToString());
            Printer.Promote();
            Body.Show();
            Printer.Demote();
        }

        public override string ToString()
        {
            return $"[Unless:{Condition}]";
        }

        public override void Emit(TranspilerStream stream)
        {
            if (Condition.TypeDecl != Block.Global().FindType("Bool"))
                throw new CompileError(Condition.Token, 
                    "If conditional must be a boolean expression");

            stream.Write("if(!(");
            Condition.Emit(stream);
            stream.Write(".val");
            stream.WriteLine(")){");

            Body.EmitDecl(stream);
            Body.EmitImpl(stream);

            stream.WriteLine("}");
        }
    }

    public class ElseStmt : Stmt
    {
        public readonly Block Body;

        public override Token Token { get; }

        public ElseStmt(Block block, Token elseToken, Block body) : base(block)
        {
            Token = elseToken;
            Body = body;
        }

        public override void Show()
        {
            Printer.Print(ToString());
            Printer.Promote();
            Body.Show();
            Printer.Demote();
        }

        public override string ToString()
        {
            return "[Else]";
        }

        public override void Emit(TranspilerStream stream)
        {
            stream.WriteLine("else{");
            Body.EmitDecl(stream);
            Body.EmitImpl(stream);
            stream.WriteLine("}");
        }
    }

    public enum VarAccess
    {
        ImmutableLocal,
        MutableLocal,
        DynamicLocal,
        ImmutableBorrow,
        MutableBorrow,
        DynamicTake,
    }

    public enum PassedBy
    {
        ImmutableBorrow,
        MutableBorrow,
        DynamicTake,
    }
}