using System;
using System.IO;
using System.Text;

namespace KhimTools.Core.Automation
{
    /// <summary>Newline-delimited MCP stdio binding. The caller owns process lifecycle and must not run it on Revit's UI thread.</summary>
    public static class McpStdioTransport
    {
        public static void Run(McpJsonRpcAdapter adapter, TextReader input, TextWriter output)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            if (input == null) throw new ArgumentNullException("input");
            if (output == null) throw new ArgumentNullException("output");

            while (true)
            {
                bool oversized;
                string message = ReadBoundedLine(input, McpJsonRpcAdapter.MaximumMessageBytes, out oversized);
                if (message == null) return;
                string response = oversized
                    ? adapter.HandleMessage(new string(' ', McpJsonRpcAdapter.MaximumMessageBytes + 1))
                    : adapter.HandleMessage(message);
                if (response == null) continue;
                output.Write(response);
                output.Write('\n');
                output.Flush();
                if (oversized) return;
            }
        }

        private static string ReadBoundedLine(TextReader input, int maximumCharacters, out bool oversized)
        {
            var builder = new StringBuilder(Math.Min(maximumCharacters, 1024));
            oversized = false;
            int value;
            bool readAny = false;
            while ((value = input.Read()) != -1)
            {
                readAny = true;
                if (value == '\n') break;
                if (value == '\r') continue;
                if (builder.Length < maximumCharacters) builder.Append((char)value);
                else { oversized = true; break; }
            }
            if (!readAny && builder.Length == 0) return null;
            return builder.ToString();
        }
    }
}
