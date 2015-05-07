using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using XML = XAMLText;

namespace XAMLText
{
    class Program
    {
        // Input filename.
        static string Input;
        // Current output line.
        static int Line = 1;
        // Output filename.
        static string Output;
        // Output writer.
        static StreamWriter Writer;

        // Known control characters replaceable with \-escaped literals.
        static string[] ControlCharacters = { "\a", "\b", "\f", "\n", "\r", "\t", "\v" };
        static string[] ControlReplacements = { @"\a", @"\b", @"\f", @"\n", @"\r", @"\t", @"\v" };

        // Frees resources on exit.
        static int Exit(int errno)
        {
            // Cleanup.
            if (Writer != null) Writer.Dispose();

            return errno;
        }

        // Main entry point.
        static int Main(string[] args)
        {
            if (args.Length < 2)
                return Exit(Usage());

            Input = args[0];
            Output = args[1];

            try
            {
                Writer = new StreamWriter(Output);

            } catch (Exception e)
            {
                Console.WriteLine(String.Format("Write error: {0}", e.Message));

                return Exit(1);
            }

            XML.StartElement = StartElement;

            bool success = XML.Parse(Input);

            if (success) Writer.WriteLine();

            return Exit(success ? 0 : 1);
        }

        // Invoked when start tag of element was encountered.
        static void StartElement(string tagName, Dictionary<string, string> attrs, bool empty)
        {
            if (tagName != "l:Catalog" || attrs.Count == 0 || !attrs.ContainsKey("Message")) return;

            if (attrs.ContainsKey("Plural"))
            {
                if (!attrs.ContainsKey("N")) return;

                WritePlural(XML.LineNumber, attrs["Message"], attrs["Plural"], attrs["N"], attrs.ContainsKey("Context") ? attrs["Context"] : null);
            }
            else
                WriteMessage(XML.LineNumber, attrs["Message"], attrs.ContainsKey("Context") ? attrs["Context"] : null);
        }

        // Converts message string into literal string.
        static string ToLiteral(string str)
        {
            // Escape known control characters with literals.
            for (int i = 0; i < ControlCharacters.Length; ++i)
                str = str.Replace(ControlCharacters[i], ControlReplacements[i]);

            // Escape double quote.
            str = str.Replace("\"", @"\""");

            return str;
        }

        // Displays usage.
        static int Usage()
        {
            Console.WriteLine(String.Format("Usage: {0} <input-file> <output-file>", "XAMLText"));

            return 1;
        }

        // Writes message to output.
        static void WriteMessage(int lineno, string message, string context)
        {
            try
            {
                // Output new lines to align line numbers.
                while (lineno > Line)
                {
                    Writer.WriteLine();
                    ++Line;
                }

                if (context == null)
                    Writer.Write("L10n.Message(\"{0}\"); ", ToLiteral(message));
                else
                    Writer.Write("L10n.Message(\"{0}\", \"{1}\"); ", ToLiteral(message), ToLiteral(context));
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Write error: {0}", e.Message));
                XML.Stop();
            }
        }

        // Writes plural message to output.
        static void WritePlural(int lineno, string message, string plural, string n, string context)
        {
            try
            {
                // Output new lines to align line numbers.
                while (lineno > Line)
                {
                    Writer.WriteLine();
                    ++Line;
                }

                if (context == null)
                    Writer.Write("L10n.Plural(\"{0}\", \"{1}\", \"{2}\");", ToLiteral(message), ToLiteral(plural), ToLiteral(n));
                else
                    Writer.Write("L10n.Plural(\"{0}\", \"{1}\", \"{2}\", \"{3}\"); ", ToLiteral(message), ToLiteral(plural), ToLiteral(n), ToLiteral(context));
            }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Write error: {0}", e.Message));
                XML.Stop();
            }
        }
    }
}
