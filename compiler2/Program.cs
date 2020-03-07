#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace compiler2
{
    class Program
    {
        static void Main(string[] args)
        {
            ast.Program ast;
            using (var libStream = new StreamReader("lib/builtin.zrm"))
            using (var inputStream = new StreamReader("input.zrm"))
            {
                var inStream = new StreamReader(new ConcatenatedStream(libStream, inputStream));

                Console.WriteLine("Loading...");
                var lex = new Lexer(inStream);
                var tokens = lex.Tokens().GetEnumerator();
                var parser = new Parser();

                Console.WriteLine("Parsing...");
                var sw = Stopwatch.StartNew();
                ast = parser.Parse(tokens);
                sw.Stop();

                Console.WriteLine($"Parsed: {sw.ElapsedMilliseconds} ms");
            }

            ast.Show();
            Console.WriteLine();

            Console.WriteLine("Transpiling...");
            var compiler = new Compiler();
            var sw1 = Stopwatch.StartNew();
            using (var outStream = new StreamWriter("bin/ir.cpp"))
                compiler.Write(ast, outStream);
            sw1.Stop();

            Console.WriteLine($"Transpiled: {sw1.ElapsedMilliseconds} ms");
        }
    }

    public class ConcatenatedStream : Stream
    {
        Queue<Stream> streams;

        public ConcatenatedStream(params StreamReader[] streams)
        {
            this.streams = new Queue<Stream>(streams.Select(x => x.BaseStream));
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;

            while (count > 0 && streams.Count > 0)
            {
                int bytesRead = streams.Peek().Read(buffer, offset, count);
                if (bytesRead == 0)
                {
                    streams.Dequeue().Dispose();
                    continue;
                }

                totalBytesRead += bytesRead;
                offset += bytesRead;
                count -= bytesRead;
            }

            return totalBytesRead;
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
