#nullable enable
using System.IO;
using System.Linq;

namespace compiler2
{
    public static class Metafunctions
    {
        public static void Emit(StreamWriter stream, ast.FnCall fn)
        {
            if (fn.Id.Text == "#cpp")
            {
                // Writes C++ code directly, like a macro.
                foreach (var arg in fn.Args)
                {
                    if (arg is ast.StrExpr expr)
                        stream.Write(
                            expr.Value.Text.Substring(1, expr.Value.Text.Length - 2)
                            .Replace("#__", Compiler.Prefix));
                    else
                    {
                        throw new CompileError(arg.Token,
                            "#cpp only takes string literals");
                    }
                }
            }
        }
    }
}
