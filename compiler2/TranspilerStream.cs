#nullable enable
using System;
using System.IO;

namespace compiler2
{
    public class TranspilerStream
    {
        public readonly StreamWriter BaseStream;

        public TranspilerStream(StreamWriter baseStream)
        {
            BaseStream = baseStream;
        }

        public void Write(object val)
        {
            BaseStream.Write(val.ToString());
            Console.Write(val.ToString());
        }

        public void WriteLine(object val)
        {
            BaseStream.WriteLine(val.ToString());
            Console.WriteLine(val.ToString());
        }
    }
}
