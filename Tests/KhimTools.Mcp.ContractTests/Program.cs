using System;
using System.IO;
using KhimTools.Core.Automation;
using Newtonsoft.Json.Linq;

namespace KhimTools.Mcp.ContractTests
{
    internal static class Program
    {
        private static int _checks;

        private static void Check(bool condition, string label)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
        }

        private static McpJsonRpcAdapter CreateAdapter()
        {
            return new McpJsonRpcAdapter(new InternalAutomationApi(false));
        }

        private static JObject Message(string method, JObject parameters = null, string version = "2026-07-28", bool includeMeta = true)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method,
                ["params"] = parameters ?? new JObject()
            };
            if (includeMeta)
                request["_meta"] = new JObject
                {
                    ["io.modelcontextprotocol/protocolVersion"] = version,
                    ["io.modelcontextprotocol/clientCapabilities"] = new JObject()
                };
            return request;
        }

        private static JObject Call(JObject arguments)
        {
            return Message("tools/call", new JObject { ["name"] = "sheet-export.naming-preview", ["arguments"] = arguments });
        }

        private static JObject Arguments()
        {
            return new JObject { ["sheetNumber"] = "A101", ["sheetName"] = "Plan", ["unitSystem"] = "NotApplicable", ["dryRun"] = true };
        }

        private static JObject Send(McpJsonRpcAdapter adapter, JObject message)
        {
            return JObject.Parse(adapter.HandleMessage(message.ToString(Newtonsoft.Json.Formatting.None)));
        }

        private static void TestDiscoveryAndList()
        {
            var adapter = CreateAdapter();
            JObject discover = Send(adapter, Message("server/discover"));
            Check((string)discover["result"]["supportedVersions"][0] == McpJsonRpcAdapter.ProtocolVersion, "server/discover advertises supported version");
            Check((bool)discover["result"]["capabilities"]["tools"]["listChanged"] == false, "server/discover advertises tools feature");
            JObject listed = Send(adapter, Message("tools/list"));
            Check(((JArray)listed["result"]["tools"]).Count == 1, "capability listing is deterministic and limited to verified tool");
            Check((bool)listed["result"]["tools"][0]["inputSchema"]["additionalProperties"] == false, "schema rejects extra properties");
            Check((bool)listed["result"]["tools"][0]["annotations"]["readOnlyHint"] && ((string)listed["result"]["tools"][0]["description"]).Contains("PREVIEW_ONLY"), "tool is clearly read-only and preview-only");
        }

        private static void TestTypedToolCall()
        {
            JObject response = Send(CreateAdapter(), Call(Arguments()));
            Check((bool)response["result"]["isError"] == false, "valid call succeeds");
            Check((string)response["result"]["structuredContent"]["OutputName"] == "A101 - Plan", "call result preserves structured output");
            Check((string)response["result"]["content"][0]["type"] == "text", "call result includes text content");
            JObject unknownToken = Arguments(); unknownToken["expression"] = "{ExecutableToken}";
            JObject rejectedToken = Send(CreateAdapter(), Call(unknownToken));
            Check((bool)rejectedToken["result"]["isError"], "canonical naming service rejects unregistered expression tokens");
        }

        private static void TestVersionAndMetadata()
        {
            JObject unsupported = Send(CreateAdapter(), Message("server/discover", null, "1900-01-01"));
            Check((int)unsupported["error"]["code"] == -32022, "unsupported version includes negotiation error");
            JObject missingMeta = Send(CreateAdapter(), Message("tools/list", null, null, false));
            Check((int)missingMeta["error"]["code"] == -32602, "request without required per-request metadata is rejected");
        }

        private static void TestUnknownCalls()
        {
            Check((int)Send(CreateAdapter(), Message("shell/execute"))["error"]["code"] == -32601, "unknown method rejected");
            JObject unknown = Message("tools/call", new JObject { ["name"] = "unregistered", ["arguments"] = Arguments() });
            Check((int)Send(CreateAdapter(), unknown)["error"]["code"] == -32602, "unknown tool rejected");
        }

        private static void TestMutationBoundary()
        {
            JObject badUnit = Arguments(); badUnit["unitSystem"] = "Feet";
            Check((bool)Send(CreateAdapter(), Call(badUnit))["result"]["isError"], "non-dimensional capability rejects implicit/foreign unit system");
            JObject execute = Arguments(); execute["dryRun"] = false;
            Check((bool)Send(CreateAdapter(), Call(execute))["result"]["isError"], "preview cannot transition to execute");
            JObject extra = Arguments(); extra["command"] = "shell";
            Check((int)Send(CreateAdapter(), Call(extra))["error"]["code"] == -32602, "arbitrary command fields rejected");
            JObject oversizedText = Arguments(); oversizedText["sheetName"] = new string('x', McpJsonRpcAdapter.MaximumTextLength + 1);
            Check((bool)Send(CreateAdapter(), Call(oversizedText))["result"]["isError"], "text length is bounded");
        }

        private static void TestCanonicalRegexTimeout()
        {
            JObject arguments = Arguments();
            arguments["sheetName"] = new string('a', 254) + "!";
            arguments["expression"] = "{SheetName}";
            arguments["regexPattern"] = "^(a+)+$";
            var timer = System.Diagnostics.Stopwatch.StartNew();
            JObject response = Send(CreateAdapter(), Call(arguments));
            timer.Stop();
            Check((bool)response["result"]["isError"], "catastrophic regex timeout returns an actionable tool error");
            Check(timer.ElapsedMilliseconds < 2000, "user regex evaluation is time-bounded");
        }

        private static void TestMessageLimitsAndMalformedInput()
        {
            string huge = new string('x', McpJsonRpcAdapter.MaximumMessageBytes + 1);
            Check((int)JObject.Parse(CreateAdapter().HandleMessage(huge))["error"]["code"] == -32600, "request byte-size limit enforced");
            Check((int)JObject.Parse(CreateAdapter().HandleMessage("not-json"))["error"]["code"] == -32700, "malformed JSON returns parse error");
            Check((int)JObject.Parse(CreateAdapter().HandleMessage("[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}]"))["error"]["code"] == -32700, "batch JSON-RPC is rejected");
            string duplicate = "{\"jsonrpc\":\"2.0\",\"id\":1,\"id\":2,\"method\":\"tools/list\",\"_meta\":{\"io.modelcontextprotocol/protocolVersion\":\"2026-07-28\",\"io.modelcontextprotocol/clientCapabilities\":{}}}";
            Check(JObject.Parse(CreateAdapter().HandleMessage(duplicate))["error"] != null, "duplicate properties rejected");
        }

        private static void TestStdioFraming()
        {
            string input = Message("server/discover").ToString(Newtonsoft.Json.Formatting.None) + "\n" + Message("tools/list").ToString(Newtonsoft.Json.Formatting.None) + "\n";
            var output = new StringWriter();
            McpStdioTransport.Run(CreateAdapter(), new StringReader(input), output);
            string[] lines = output.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Check(lines.Length == 2, "stdio emits one newline-delimited response per request");
            Check((string)JObject.Parse(lines[0])["result"]["supportedVersions"][0] == McpJsonRpcAdapter.ProtocolVersion, "stdio transports MCP response end to end");
            var boundedOutput = new StringWriter();
            string tooLong = new string('x', McpJsonRpcAdapter.MaximumMessageBytes + 10) + "\n" + Message("tools/list").ToString(Newtonsoft.Json.Formatting.None) + "\n";
            McpStdioTransport.Run(CreateAdapter(), new StringReader(tooLong), boundedOutput);
            string[] boundedLines = boundedOutput.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Check(boundedLines.Length == 1 && (int)JObject.Parse(boundedLines[0])["error"]["code"] == -32600, "oversized stdio frame returns error and stops without consuming later data");
        }

        public static int Main()
        {
            try
            {
                TestDiscoveryAndList();
                TestTypedToolCall();
                TestVersionAndMetadata();
                TestUnknownCalls();
                TestMutationBoundary();
                TestCanonicalRegexTimeout();
                TestMessageLimitsAndMalformedInput();
                TestStdioFraming();
                Console.WriteLine("PASS: MCP protocol, canonical service and local stdio contract (25 checks)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL after " + _checks + " checks: " + ex);
                return 1;
            }
        }
    }
}
