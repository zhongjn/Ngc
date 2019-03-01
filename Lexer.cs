using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ngc
{
    public enum TokenType
    {
        Literal,
        Keyword,
        Identifier,
        Space,
        Seperator, // ( ) [ ] { } , ;
        Operator // + - * /
    }

    public class Token
    {
        public Token(TokenType type, string content)
        {
            Type = type;
            Content = content;
        }

        public TokenType Type { get; }
        public string Content { get; }

        #region Misc
        public override bool Equals(object obj)
        {
            var token = obj as Token;
            return token != null &&
                   Type == token.Type &&
                   Content == token.Content;
        }

        public override int GetHashCode()
        {
            var hashCode = 998978007;
            hashCode = hashCode * -1521134295 + Type.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Content);
            return hashCode;
        }

        public override string ToString()
        {
            return $"{Content} ({Type})";
        }

        public static bool operator==(Token t1, Token t2)
        {
            return t1.Type == t2.Type && t1.Content == t2.Content;
        }

        public static bool operator !=(Token t1, Token t2)
        {
            return !(t1 == t2);
        }
        #endregion
    }


    public class Lexer
    {
        private static Regex CompiledRegex(string pattern) => new Regex(pattern, RegexOptions.Compiled);
        private static Dictionary<TokenType, Regex> m_TokenRegex = new Dictionary<TokenType, Regex>() {
            { TokenType.Literal, CompiledRegex("\\G(([0-9]+(\\.[0-9]+)?)|(\"([^\\\"]|\\.)*\"))") },
            { TokenType.Keyword, CompiledRegex("\\G(if|else|while|for|break|return)") },
            { TokenType.Identifier, CompiledRegex("\\G[a-zA-Z_][a-zA-Z0-9_]*") },
            { TokenType.Space, CompiledRegex("\\G(\\s|\\t|\\n)+") },
            { TokenType.Seperator, CompiledRegex("\\G[,;\\(\\)\\[\\]\\{\\}]") },
            { TokenType.Operator, CompiledRegex("\\G(\\+|-|\\*|/|==|!=|=|%|>=|<=|>|<)|\\|\\||&&") },
        };

        private class LineNumberInfo
        {
            private int[] m_CharPerLine;
            public LineNumberInfo(string str)
            {
                m_CharPerLine = str.Split('\n').Select(s => s.Length).ToArray();
            }
            public int GetLineNumber(int charPos)
            {
                int accum = 0;
                int line = 0;
                for (; line < m_CharPerLine.Length; line++)
                {
                    accum += m_CharPerLine[line];
                    if (accum >= charPos) break;
                }
                return line + 1;
            }

        }

        public List<Token> Tokenize(string str)
        {
            var lineInfo = new LineNumberInfo(str);
            var tokenLst = new List<Token>();
            int cursor = 0;
            while (cursor < str.Length)
            {
                TokenType? curType = null;
                Match curMatch = null;
                foreach (var pair in m_TokenRegex)
                {
                    Match match = pair.Value.Match(str, cursor);
                    if (match.Success)
                    {
                        curType = pair.Key;
                        curMatch = match;
                        break;
                    }
                }
                if (curType == null) throw new LexerException(lineInfo.GetLineNumber(cursor), $"Unsuccessful match.");
                tokenLst.Add(new Token(curType.Value, curMatch.Value));
                cursor += curMatch.Length;
            }
            return tokenLst;
        }
    }

    public class LexerException : Exception
    {
        public int LineNumber { get; }
        public LexerException(int lineNumber, string message) : base(message)
        {
            LineNumber = lineNumber;
        }
        public override string ToString()
        {
            return $"Lexer error at or near line {LineNumber}: {Message}";
        }
    }
}
