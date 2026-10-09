using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Small dependency-free JSON reader for RuntimeVmd manifests. Unity hosts
    /// do not expose one consistent System.Runtime.Serialization.Json profile:
    /// notably MateEngine's Mono profile lacks DataContractJsonSerializer.
    /// This reader intentionally supports only standard JSON values and enforces
    /// conservative depth/shape limits.
    /// </summary>
    internal static class RuntimeVmdJson
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            object value = new Parser(json).Parse();
            Dictionary<string, object> result = value as Dictionary<string, object>;
            if (result == null) throw new InvalidDataException("The JSON root must be an object.");
            return result;
        }

        public static bool Contains(Dictionary<string, object> value, string name)
        {
            return value != null && value.ContainsKey(name);
        }

        public static string GetString(Dictionary<string, object> value, string name)
        {
            object raw;
            if (value == null || !value.TryGetValue(name, out raw) || raw == null) return null;
            string result = raw as string;
            if (result == null) throw new InvalidDataException("JSON field '" + name + "' must be a string.");
            return result;
        }

        public static string[] GetStringArray(Dictionary<string, object> value, string name)
        {
            object raw;
            if (value == null || !value.TryGetValue(name, out raw) || raw == null) return null;
            List<object> list = raw as List<object>;
            if (list == null) throw new InvalidDataException("JSON field '" + name + "' must be an array.");
            string[] result = new string[list.Count];
            for (int index = 0; index < list.Count; index++)
            {
                result[index] = list[index] as string;
                if (result[index] == null)
                    throw new InvalidDataException("JSON field '" + name + "' must contain only strings.");
            }
            return result;
        }

        public static int GetInt32(Dictionary<string, object> value, string name, int fallback)
        {
            object raw;
            if (value == null || !value.TryGetValue(name, out raw) || raw == null) return fallback;
            double number = RequireNumber(raw, name);
            if (number < int.MinValue || number > int.MaxValue || Math.Floor(number) != number)
                throw new InvalidDataException("JSON field '" + name + "' must be an integer.");
            return (int)number;
        }

        public static float GetSingle(Dictionary<string, object> value, string name, float fallback)
        {
            object raw;
            if (value == null || !value.TryGetValue(name, out raw) || raw == null) return fallback;
            double number = RequireNumber(raw, name);
            if (double.IsNaN(number) || double.IsInfinity(number) || number < -float.MaxValue || number > float.MaxValue)
                throw new InvalidDataException("JSON field '" + name + "' is outside the Single range.");
            return (float)number;
        }

        public static bool GetBoolean(Dictionary<string, object> value, string name, bool fallback)
        {
            object raw;
            if (value == null || !value.TryGetValue(name, out raw) || raw == null) return fallback;
            if (!(raw is bool)) throw new InvalidDataException("JSON field '" + name + "' must be a boolean.");
            return (bool)raw;
        }

        private static double RequireNumber(object raw, string name)
        {
            if (!(raw is double)) throw new InvalidDataException("JSON field '" + name + "' must be a number.");
            return (double)raw;
        }

        private sealed class Parser
        {
            private const int MaximumDepth = 64;
            private readonly string text;
            private int position;

            public Parser(string json)
            {
                if (json == null) throw new ArgumentNullException("json");
                text = json;
            }

            public object Parse()
            {
                SkipWhiteSpace();
                object value = ParseValue(0);
                SkipWhiteSpace();
                if (position != text.Length) throw Error("Unexpected trailing JSON content.");
                return value;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaximumDepth) throw Error("JSON nesting is too deep.");
                SkipWhiteSpace();
                if (position >= text.Length) throw Error("Unexpected end of JSON.");
                char token = text[position];
                if (token == '{') return ParseObjectValue(depth + 1);
                if (token == '[') return ParseArray(depth + 1);
                if (token == '"') return ParseString();
                if (token == 't') { ExpectLiteral("true"); return true; }
                if (token == 'f') { ExpectLiteral("false"); return false; }
                if (token == 'n') { ExpectLiteral("null"); return null; }
                if (token == '-' || (token >= '0' && token <= '9')) return ParseNumber();
                throw Error("Unexpected JSON token '" + token + "'.");
            }

            private Dictionary<string, object> ParseObjectValue(int depth)
            {
                position++;
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhiteSpace();
                if (Consume('}')) return result;
                while (true)
                {
                    SkipWhiteSpace();
                    if (position >= text.Length || text[position] != '"') throw Error("Expected a JSON object key.");
                    string key = ParseString();
                    SkipWhiteSpace();
                    if (!Consume(':')) throw Error("Expected ':' after a JSON object key.");
                    object value = ParseValue(depth);
                    if (result.ContainsKey(key)) throw Error("Duplicate JSON object key '" + key + "'.");
                    result.Add(key, value);
                    SkipWhiteSpace();
                    if (Consume('}')) return result;
                    if (!Consume(',')) throw Error("Expected ',' or '}' in a JSON object.");
                }
            }

            private List<object> ParseArray(int depth)
            {
                position++;
                List<object> result = new List<object>();
                SkipWhiteSpace();
                if (Consume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue(depth));
                    SkipWhiteSpace();
                    if (Consume(']')) return result;
                    if (!Consume(',')) throw Error("Expected ',' or ']' in a JSON array.");
                }
            }

            private string ParseString()
            {
                position++;
                StringBuilder result = new StringBuilder();
                while (position < text.Length)
                {
                    char value = text[position++];
                    if (value == '"') return result.ToString();
                    if (value < 0x20) throw Error("An unescaped control character occurred in a JSON string.");
                    if (value != '\\')
                    {
                        result.Append(value);
                        continue;
                    }
                    if (position >= text.Length) throw Error("Unexpected end of a JSON escape sequence.");
                    char escaped = text[position++];
                    switch (escaped)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ParseUnicodeEscape()); break;
                        default: throw Error("Invalid JSON escape sequence.");
                    }
                }
                throw Error("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > text.Length) throw Error("Truncated JSON Unicode escape.");
                int value = 0;
                for (int index = 0; index < 4; index++)
                {
                    char digit = text[position++];
                    value <<= 4;
                    if (digit >= '0' && digit <= '9') value += digit - '0';
                    else if (digit >= 'a' && digit <= 'f') value += digit - 'a' + 10;
                    else if (digit >= 'A' && digit <= 'F') value += digit - 'A' + 10;
                    else throw Error("Invalid JSON Unicode escape.");
                }
                return (char)value;
            }

            private double ParseNumber()
            {
                int start = position;
                if (Consume('-') && position >= text.Length) throw Error("Truncated JSON number.");
                if (Consume('0'))
                {
                    if (position < text.Length && char.IsDigit(text[position])) throw Error("A JSON number has a leading zero.");
                }
                else
                {
                    if (position >= text.Length || text[position] < '1' || text[position] > '9') throw Error("Invalid JSON number.");
                    while (position < text.Length && char.IsDigit(text[position])) position++;
                }
                if (Consume('.'))
                {
                    if (position >= text.Length || !char.IsDigit(text[position])) throw Error("Invalid JSON fraction.");
                    while (position < text.Length && char.IsDigit(text[position])) position++;
                }
                if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
                {
                    position++;
                    if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                    if (position >= text.Length || !char.IsDigit(text[position])) throw Error("Invalid JSON exponent.");
                    while (position < text.Length && char.IsDigit(text[position])) position++;
                }
                double value;
                if (!double.TryParse(text.Substring(start, position - start), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value))
                    throw Error("Invalid or non-finite JSON number.");
                return value;
            }

            private void ExpectLiteral(string expected)
            {
                if (position + expected.Length > text.Length ||
                    !string.Equals(text.Substring(position, expected.Length), expected, StringComparison.Ordinal))
                    throw Error("Invalid JSON literal.");
                position += expected.Length;
            }

            private bool Consume(char expected)
            {
                if (position >= text.Length || text[position] != expected) return false;
                position++;
                return true;
            }

            private void SkipWhiteSpace()
            {
                while (position < text.Length)
                {
                    char value = text[position];
                    if (value != ' ' && value != '\t' && value != '\r' && value != '\n') break;
                    position++;
                }
            }

            private InvalidDataException Error(string message)
            {
                return new InvalidDataException(message + " Position " + position + ".");
            }
        }
    }
}
