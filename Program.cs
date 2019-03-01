using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ngc
{
    class Program
    {

        static double AvgRuntime(LLVMJITter.Main ac, int ms)
        {
            int times = 0;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                ac();
                times++;
            }
            return (double)sw.ElapsedMilliseconds / times;
        }


        static void Main(string[] args)
        {
            var lexer = new Lexer();
            var parser = new Parser();
            var analyzer = new SemanticsAnalyzer();
            var irGen = new LLVMIRGenerator();
            var jitter = new LLVMJITter();

            Console.WriteLine("Enter '#' to  terminate inputing.");

            while (true)
            {
                var strb = new StringBuilder();
                
                while (true)
                {
                    Console.Write(">>> ");
                    var line = Console.ReadLine();
                    if (line == "#") break;
                    strb.AppendLine(line);
                }
                var code = strb.ToString();
                try
                {
                    var tokens = lexer.Tokenize(code);
                    var tree = parser.Parse(tokens);
                    analyzer.Analyze(tree);

                    var module = irGen.Generate(tree);
                    var moduleStr = LLVMUtil.ModuleToString(module);
                    Console.WriteLine("LLVM IR:");
                    Console.Write(moduleStr);
                    Console.WriteLine();

                    var main = jitter.Compile(module);

                    Console.WriteLine($"Exit code: {main()}");
                    Console.WriteLine("Benchmarking...");
                    Console.WriteLine($"Average runtime: {AvgRuntime(main, 500)}ms");

                    
                }
                catch (LexerException ex)
                {
                    Console.WriteLine("Lexer: " + ex.Message);
                }
                catch (ParserException ex)
                {
                    Console.WriteLine("Parser: " + ex.Message);
                }
                catch (SemanticsException ex)
                {
                    Console.WriteLine("Semantics Analyzer: " + ex.Message);
                }
            }
        }
    }
}
