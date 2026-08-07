using System;
using System.Text;
using XLua;

namespace WitchModMCP.Terminal
{
    public static class ConsoleFormatter
    {
        private const string R = "\x1b[0m";
        private const string GRAY = "\x1b[90m";
        private const string RED = "\x1b[31m";
        private const string GREEN = "\x1b[32m";
        private const string YELLOW = "\x1b[33m";
        private const string BLUE = "\x1b[34m";
        private const string CYAN = "\x1b[36m";

        public static string Format(object[] results)
        {
            if (results == null || results.Length == 0)
                return GRAY + "nil" + R;

            var sb = new StringBuilder();
            for (int i = 0; i < results.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(FormatValue(results[i], 0));
            }
            return sb.ToString();
        }

        public static string FormatError(string message)
        {
            return RED + message + R;
        }

        private static string FormatValue(object val, int depth)
        {
            if (val == null)
                return GRAY + "nil" + R;
            if (val is bool b)
                return BLUE + (b ? "true" : "false") + R;
            if (val is string s)
                return GREEN + "\"" + s + "\"" + R;
            if (val is int || val is long || val is float || val is double || val is decimal)
                return YELLOW + val.ToString() + R;
            if (val is LuaTable table)
                return FormatTable(table, depth);

            return CYAN + val.GetType().Name + R + ": " + val;
        }

        private static string FormatTable(LuaTable table, int depth)
        {
            if (depth > 4) return GRAY + "{...}" + R;

            var sb = new StringBuilder();
            sb.AppendLine("{");

            var keys = table.GetKeys<object>();
            foreach (var key in keys)
            {
                string keyStr;
                if (key is string ks)
                    keyStr = ks;
                else if (key is long kl)
                    keyStr = "[" + kl + "]";
                else
                    keyStr = "[" + FormatValue(key, depth + 1) + "]";

                for (int i = 0; i <= depth; i++) sb.Append("  ");
                sb.Append(keyStr).Append(" = ");
                sb.AppendLine(FormatValue(table.Get<object, object>(key), depth + 1));
            }

            for (int i = 0; i < depth; i++) sb.Append("  ");
            sb.Append("}");
            return sb.ToString();
        }
    }
}
