using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XAMLText
{
    class XML
    {
        // Namespace scope.
        class Scope
        {
            static Dictionary<string, string> NS = new Dictionary<string, string>();

            static Stack<Dictionary<string, string>> Stack = new Stack<Dictionary<string, string>>();

            // Begins namespace declarations.
            internal static void Begin()
            {
                NS.Clear();
            }

            // Declares namespace.
            internal static bool Declare(string prefix, string ns)
            {
                // Namespace constraint: No Prefix Undeclaring
                if (string.IsNullOrEmpty(ns))
                    return Error("Namespace declaration error");

                if (NS.ContainsKey(prefix))
                    return Error("Duplicit namespace declaration");

                NS.Add(prefix, ns);

                return true;
            }

            // Ends namespace declarations.
            internal static void End()
            {
                Stack.Push(NS.Count > 0 ? new Dictionary<string, string>(NS) : null);
            }

            // Initializes scope.
            internal static void Initialize()
            {
                Stack.Clear();

                Stack.Push(
                    new Dictionary<string, string>()
                    {
                        { "", null } // Default undeclared namespace.
                    }
                );
            }

            // Leaves namespace scope.
            internal static void Leave()
            {
                Stack.Pop();
            }

            // Resolves prefix.
            internal static string Resolve(string prefix)
            {
                foreach (Dictionary<string, string> NS in Stack)
                    if (NS != null && NS.ContainsKey(prefix))
                        return NS[prefix];

                return null;
            }
        }

        // Reserved namespaces.
        public const string NAMESPACE = "http://www.w3.org/XML/1998/namespace";
        public const string XMLNS = "http://www.w3.org/2000/xmlns/";

        public delegate void OnStartElement(string ns, string localName, Dictionary<string, string> attrs, bool empty);

        public static int LineNumber { get { return LineNo;  } }

        public static OnStartElement StartElement;

        static string Input;

        static bool HasError;

        static int LineNo;

        static readonly Regex ReNewLine = new Regex(@"\r\n?|\n");

        /* Attribute ::= Name Eq AttValue
         * AttValue ::= '"' ([^<&"] | Reference)* '"' |  "'" ([^<&'] | Reference)* "'"
         */
        static bool Attribute(out string name, out string value)
        {
            name = value = null;

            if (!S()) return false;

            if (!Name(out name)) return false;

            if (!Eq()) return Error("Attribute");

            string quote = Peek("\"") ? "\"" : (Peek("'") ? "'" : null);
            if (quote == null) return Error("Quote");

            Next(quote);

            value = "";
            do
            {
                string val;
                if (NextRE("[^<&" + Regex.Escape(quote) + "]*", out val))
                    value += val;

                if (Peek("&"))
                {
                    if (! Reference(out val)) return false;

                    value += val;
                }

            } while (!EOF && !Peek(quote));

            if (!Next(quote)) return Error("Quote");

#if (DEBUG)
            Console.WriteLine(string.Format("@{0}", name));
#endif

            if (name == "xmlns")
                return Scope.Declare("", value);
            else
                if (name.StartsWith("xmlns:"))
                {
                    string ns, localName, prefix;
                    if (!ExpandName(name, out ns, out localName, out prefix))
                        return false;

                    if (ns == XMLNS)
                        return Scope.Declare(localName, value);
                }

            return true;
        }

        /* CDSect ::= CDStart CData CDEnd
         * CDStart ::= '<![CDATA['
         * CData ::= (Char* - (Char* ']]>' Char*))
         * Char ::= #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
         * CDEnd ::= ']]>'
         */
        static bool CDSect()
        {
            if (!Next("<![CDATA[")) return false;

            NextRE(@"((?!\]\]>)[\t\n\r\u0020-\uD7FF\uE000-\uFFFD])*");

            if (!Next("]]>")) return Error("CDATA");

#if (DEBUG)
            Console.WriteLine("<![CDATA[]]>");
#endif
            return true;
        }

        /* CharData ::= [^<&]* - ([^<&]* ']]>' [^<&]*)
         */
        static bool CharData()
        {
            if (EOF) return false;

            if (NextRE(@"((?!\]\]>)[^<&])+"))
            {
                // CharData.

                return true;
            }

            return false;
        }

        /* Comment ::= '<!--' ((Char - '-') | ('-' (Char - '-')))* '-->'
         * Char ::= #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
         */
        static bool Comment()
        {
            if (!Next("<!--")) return false;

            NextRE(@"([\t\n\r\x20-\x2C\u002E-\uD7FF\uE000-\uFFFD]|-[\t\n\r\x20-\x2C\u002E-\uD7FF\uE000-\uFFFD])*");

            if (!Next("-->")) return Error("Comment");

#if (DEBUG)
            Console.WriteLine("<!--Comment-->");
#endif
            return true;
        }

        /* content ::= CharData? ((element | Reference | CDSect | PI | Comment) CharData?)*
         */
        static bool Content()
        {
            if (EOF) return false;

            CharData();

            string dummy;
            // Ordered from longest token to shortest.
            while (CDSect() || Comment() || PI() || Reference(out dummy) || Element())
                CharData();

            return true;
        }

        /* doctypedecl ::= '<!DOCTYPE' S Name (S ExternalID)? S? ('[' intSubset ']' S?)? '>'
         */
        static bool DocTypeDecl()
        {
            return false;
        }

        static bool EOF { get { return Input.Length == 0; } }

        /* element ::= EmptyElemTag | STag content ETag
         * EmptyElemTag ::= '<' Name (S Attribute)* S? '/>'
         * STag ::= '<' Name (S Attribute)* S? '>'
         * ETag ::= '</' Name S? '>'
         */
        static bool Element()
        {
            // Don't match '<' in '</'.
            if (Peek("</")) return false;

            if (!Next("<")) return false;

            string startTag;
            if (!Name(out startTag)) return Error("Start tag");

            int line = LineNo;

            Scope.Begin();

            Dictionary<string, string> attrs = new Dictionary<string, string>();
            string name, value;
            while (Attribute(out name, out value))
                attrs.Add(name, value);

            Scope.End();

            S();

            string ns, localName, prefix;
            if (Next("/>"))
            {
                // <Element/>
                if (!ExpandName(startTag, out ns, out localName, out prefix))
                    return Error("Undeclared prefix");
#if (DEBUG)
                Console.WriteLine(string.Format("<{0}/> on line {1}", startTag, line));
#endif
                if (StartElement != null) StartElement(ns, localName, attrs, true);

                Scope.Leave();

                return true;
            }
            else if (!Next(">")) return Error("Start tag");

            // <Element>
            if (!ExpandName(startTag, out ns, out localName, out prefix))
                return Error("Undeclared prefix");
#if (DEBUG)
            Console.WriteLine(string.Format("<{0}> on line {1}", startTag, line));
#endif
            if (StartElement != null) StartElement(ns, localName, attrs, false);

            Content();

            string endTag;
            if (!Next("</") || !Name(out endTag)) return Error("End tag");
            S();
            if (!Next(">")) return Error("End tag");

            if (endTag != startTag) return Error("Mismatched tag", true);

            // </Element>
#if (DEBUG)
            Console.WriteLine(string.Format("</{0}>", startTag));
#endif
            Scope.Leave();

            return true;
        }

        static bool Eq()
        {
            return NextRE(@"\s*=\s*");
        }

        static bool Error(string message = null, bool showLineNo = false)
        {
            // Error was already reported.
            if (HasError) return false;

            if (showLineNo)
                Console.WriteLine(string.Format("Error at line {0}: {1}", LineNo, message));
            else
                Console.WriteLine(string.Format("Error: {0}", message));

            HasError = true;
            Input = "";

            return false;
        }

        static bool ExpandName(string name, out string ns, out string localName, out string prefix)
        {
            ns = localName = prefix = null;

            int comma = name.IndexOf(':');
            if (comma < 0)
            {
                prefix = "";
                localName = name;
            }
            else
            {
                prefix = name.Substring(0, comma);
                localName = name.Substring(comma + 1);
            }

            switch (prefix)
            {
                // Default namespace.
                case "":
                    ns = Scope.Resolve("");
                    break;

                // Reserved prefix and namespace.
                case "xml":
                    ns = NAMESPACE;
                    break;

                // Reserved prefix and namespace.
                case "xmlns":
                    ns = XMLNS;
                    break;

                // Declared namespace.
                default:
                    ns = Scope.Resolve(prefix);
                    if (ns == null)
                        return Error("Undeclared prefix");
                    break;
            }

            return true;
        }

        static int Length { get { return Input.Length; } }

        /* Name ::= NameStartChar (NameChar)*
         * NameStartChar ::= ":" | [A-Z] | "_" | [a-z] | [#xC0-#xD6] | [#xD8-#xF6] | [#xF8-#x2FF] | [#x370-#x37D] | [#x37F-#x1FFF] | [#x200C-#x200D] | [#x2070-#x218F] | [#x2C00-#x2FEF] | [#x3001-#xD7FF] | [#xF900-#xFDCF] | [#xFDF0-#xFFFD] | [#x10000-#xEFFFF]
         * NameChar ::= NameStartChar | "-" | "." | [0-9] | #xB7 | [#x0300-#x036F] | [#x203F-#x2040]
         */
        static bool Name(out string tagName)
        {
            string NameStartChar = @"[:A-Z_a-z\xC0-\xD6\xD8-\xF6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD]",
                   NameChar = @"[:A-Z_a-z\xC0-\xD6\xD8-\xF6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD.0-9\xB7\u0300-\u036F\u203F-\u2040-]*";

            tagName = null;

            string _input = Input;

            if (!NextRE(NameStartChar))
                return false;

            NextRE(NameChar);

            tagName = _input.Substring(0, _input.Length - Input.Length);

            return true;
        }

        static bool Next(string token)
        {
            if (EOF || Length < token.Length) return false;

            if (!Input.StartsWith(token)) return false;

            LineNo += ReNewLine.Matches(token).Count;

            Input = Input.Substring(token.Length);

            return true;
        }

        static bool NextRE(string regex)
        {
            if (EOF) return false;

            Regex re = new Regex("^" + regex);
            Match m = re.Match(Input);
            if (!m.Success) return false;

            // New lines.
            LineNo += ReNewLine.Matches(m.Value).Count;

            Input = Input.Substring(m.Value.Length);

            return true;
        }

        static bool NextRE(string regex, out string match)
        {
            match = null;

            if (EOF) return false;

            Regex re = new Regex("^" + regex);
            Match m = re.Match(Input);
            if (!m.Success) return false;

            match = m.Groups[0].Value;

            // New lines.
            LineNo += ReNewLine.Matches(match).Count;

            Input = Input.Substring(match.Length);

            return true;
        }

        // Starts parsing of XML file.
        public static bool Parse(string path)
        {
            try
            {
                Input = File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                return false;
            }
            if (Input == null) return false;

            HasError = false;
            LineNo = 1;

            Scope.Initialize();

            if (EOF) return true;
            Prolog();
            if (EOF) return true;

            return Element();
        }

        static bool Peek(string token)
        {
            if (EOF || Length < token.Length) return false;

            return Input.StartsWith(token);
        }

        static bool PI()
        {
            string piName;

            if (!Next("<?")) return false;

            if (!Name(out piName)) return false;

            string name, value;
            while (Attribute(out name, out value)) ;

            // XML declaration.
#if (DEBUG)
            Console.WriteLine(string.Format("<?{0}?>", piName));
#endif
            S();

            return Next("?>");
        }

        /* prolog ::= XMLDecl? Misc* (doctypedecl Misc*)?
         * Misc ::= Comment | PI | S
         */
        static bool Prolog()
        {
            XMLDecl();

            while (Comment() || S()) ;

            DocTypeDecl();

            while (Comment() || S()) ;

            return true;
        }

        /* Reference ::= EntityRef | CharRef
         * EntityRef ::= '&' Name ';'
         * CharRef ::= '&#' [0-9]+ ';' | '&#x' [0-9a-fA-F]+ ';'
         */
        static bool Reference(out string value)
        {
            string name;

            value = null;

            if (!Peek("&")) return false;

            if (Peek("&#x"))
            {
                Next("&#x");

                if (!NextRE("[0-9a-fA-F]+", out name)) return Error("Reference");

                int ch;
                if (!int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ch))
                    return Error("Reference");

                value += (char)ch;

            }
            else if (Peek("&#"))
            {
                Next("&#");

                if (!NextRE("[0-9]+", out name)) return Error("Reference");

                int ch;
                if (!int.TryParse(name, out ch)) return Error("Reference");

                value += (char)ch;
            }
            else
            {
                Next("&");

                if (!Name(out name)) return Error("Reference");

                switch (name)
                {
                    case "amp":
                        value = "&";
                        break;

                    case "apos":
                        value = "'";
                        break;

                    case "gt":
                        value = ">";
                        break;

                    case "lt":
                        value = "<";
                        break;

                    case "quot":
                        value = "\"";
                        break;

                    default:
                        return Error("Unknown entity");
                }
            }

            if (!Next(";")) return Error("Reference");

#if (DEBUG)
            Console.WriteLine(string.Format("&{0};", name));
#endif
            return true;
        }

        static bool S()
        {
            if (EOF) return false;

            return NextRE(@"[\s]+");
        }

        // Stops parsing.
        public static void Stop()
        {
            Input = "";
        }

        /* XMLDecl ::= '<?xml' VersionInfo EncodingDecl? SDDecl? S? '?>'
         */
        static bool XMLDecl()
        {
            if (!Next("<?xml")) return false;

            string name, value;
            while (Attribute(out name, out value)) ;

            // XML declaration.
#if (DEBUG)
            Console.WriteLine("<?xml?>");
#endif
            S();

            return Next("?>");
        }
    }
}
