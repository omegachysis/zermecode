#nullable enable
using System;
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

        public void Write(ast.Program ast, StreamWriter stream)
        {
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

    namespace ast
    {
        public partial class Program
        {
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
                    "\nint main(){__ZERM__main();}");
            }
        }

        public partial class Block
        {
            public void Emit(StreamWriter stream)
            {
                foreach (var decl in Decls)
                    decl.Emit(stream);
                foreach (var stmt in Stmts)
                    stmt.Emit(stream);
            }
        }

        public partial class Decl
        {
            public abstract void Emit(StreamWriter stream);
        }

        public partial class FnDecl
        {
            public override void Emit(StreamWriter stream)
            {
                if (ReturnType == null)
                    stream.Write("void");
                else
                    ReturnType.Emit(stream);
                stream.WriteLine();
                stream.Write("__ZERM__");
                stream.Write(Id.Text);
                stream.Write('(');

                foreach (var param in Params.Take(Params.Count - 1))
                {
                    param.Emit(stream);
                    stream.Write(',');
                }

                if (Params.Count > 0)
                    Params.Last().Emit(stream);

                stream.Write("){");
                Body?.Emit(stream);
                stream.Write('}');
            }
        }

        public partial class TypeSpec
        {
            public abstract void Emit(StreamWriter stream);
        }

        public partial class SimpleTypeSpec
        {
            public override void Emit(StreamWriter stream)
            {
                stream.Write(Compiler.Prefix);
                stream.Write(Id.Text);
            }
        }

        public partial class Param
        {
            public void Emit(StreamWriter stream)
            {
                Type.Emit(stream);
                stream.WriteLine();
                stream.Write(Compiler.Prefix);
                stream.Write(Id.Text);
            }
        }

        public partial class Stmt
        {
            public abstract void Emit(StreamWriter stream);
        }

        public partial class ExprStmt
        {
            public override void Emit(StreamWriter stream)
            {
                Expr.Emit(stream);
            }
        }

        public partial class Expr
        {
            public abstract void Emit(StreamWriter stream);
        }

        public partial class FnCall
        {
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
                            stream.WriteLine();
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
                    stream.Write(Compiler.Prefix);
                    stream.Write(Id.Text);

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
        
        public partial class NumExpr
        {
            public override void Emit(StreamWriter stream)
            {
                if (Value.Text.EndsWith("CI32"))
                {
                    // CInt32
                    stream.Write(Value.Text.Substring(0, Value.Text.Length - 4));
                }
                else
                    throw new NotImplementedException();
            }
        }
        
        public partial class VarExpr
        {
            public override void Emit(StreamWriter stream)
            {

            }
        }

        public partial class StrExpr
        {
            public override void Emit(StreamWriter stream)
            {
                stream.Write(Value.Text);
            }
        }

        public partial class AlgExpr
        {
            public override void Emit(StreamWriter stream)
            {

            }
        }

        public partial class Assn
        {
            public override void Emit(StreamWriter stream)
            {

            }
        }
    }
}