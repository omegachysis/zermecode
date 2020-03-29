#nullable enable
using System.IO;
using System.Linq;

namespace compiler2
{
    public static class Metafunctions
    {
        /// <summary>
        /// Emit this as a metafunction, if it is one.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fn"></param>
        /// <param name="forward">True if this is part of a forward decl process.</param>
        public static void Emit(StreamWriter stream, ast.FnCall fn, bool forward)
        {
            if (fn.Id.Text == "#cpp" ||
                fn.Id.Text == "#cpp_forward" && forward)
            {
                // Writes C++ code directly, like a macro, in the forward declaration.
                foreach (var arg in fn.Args)
                {
                    if (arg is ast.StrExpr expr)
                        stream.WriteLine(
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
