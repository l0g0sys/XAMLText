using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XMLParser;

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
        static TextWriter Writer;

        // Known control characters replaceable with \-escaped literals.
        static string[] ControlCharacters = { "\a", "\b", "\f", "\n", "\r", "\t", "\v" };
        static string[] ControlReplacements = { @"\a", @"\b", @"\f", @"\n", @"\r", @"\t", @"\v" };

        // Frees resources on exit.
        static int Exit(int errno)
        {
            // Cleanup.
            if (Output != null && Writer != null) Writer.Dispose();

            return errno;
        }

        // Main entry point.
        static int Main(string[] args)
        {
            if (args.Length < 1)
                return Exit(Usage());

            Input = args[0];
            if (args.Length == 2)
                Output = args[1];

            try
            {
                Writer = Output == null ? Console.Out : new StreamWriter(Output);

            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Write error: {0}", e.Message));

                return Exit(1);
            }

            Xml xml;
            try
            {
                xml = new Xml(Input);
                xml.StartElement = StartElement;
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Read error: {0}", e.Message));

                return Exit(1);
            }

            bool success = xml.Parse();

            if (success) Writer.WriteLine();

            if (xml.Errors.Length > 0)
            {
                for (int i = 0; i < xml.Errors.Length; ++i)
                    Console.WriteLine(xml.Errors[i]);
            }

            return Exit(success ? 0 : 1);
        }

        // Invoked when start tag of element was encountered.
        static void StartElement(Xml xml, string ns, string localName, Dictionary<string, string> attrs, bool empty)
        {
            if (localName != "Catalog" || ns != "clr-namespace:POESKillTree.Localization.XAML" || attrs.Count == 0 || !attrs.ContainsKey("Message")) return;

            if (attrs.ContainsKey("Plural"))
            {
                if (!attrs.ContainsKey("N")) return;

                WritePlural(xml, attrs["Message"], attrs["Plural"], attrs["N"], attrs.ContainsKey("Context") ? attrs["Context"] : null);
            }
            else
                WriteMessage(xml, attrs["Message"], attrs.ContainsKey("Context") ? attrs["Context"] : null);
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
            Console.WriteLine(string.Format("Usage: {0} <input-file> [output-file]", "XAMLText"));

            return 1;
        }

        // Writes message to output.
        static void WriteMessage(Xml xml, string message, string context)
        {
            try
            {
                // Output new lines to align line numbers.
                while (xml.LineNumber > Line)
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
                Console.WriteLine(string.Format("Write error: {0}", e.Message));
                xml.Stop();
            }
        }

        // Writes plural message to output.
        static void WritePlural(Xml xml, string message, string plural, string n, string context)
        {
            try
            {
                // Output new lines to align line numbers.
                while (xml.LineNumber > Line)
                {
                    Writer.WriteLine();
                    ++Line;
                }

                if (context == null)
                    Writer.Write("L10n.Plural(\"{0}\", \"{1}\", {2});", ToLiteral(message), ToLiteral(plural), n);
                else
                    Writer.Write("L10n.Plural(\"{0}\", \"{1}\", {2}, \"{3}\"); ", ToLiteral(message), ToLiteral(plural), n, ToLiteral(context));
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Write error: {0}", e.Message));
                xml.Stop();
            }
        }
    }
}
