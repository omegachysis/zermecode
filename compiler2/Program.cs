using System.IO;
using System.Text;

namespace compiler2
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var file = new StreamWriter("bin.ir.c"))
            {
                file.WriteLine("#include <stdio.h>");
                file.Write("int main() {");
                file.Write("printf(\"Hello, this is me!\");");
                file.Write("return 0;");
                file.Write("}");
            }
        }
    }
}
