using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KhimTools.Core.Automation
{
    /// <summary>Stateless MCP JSON-RPC 2.0 message adapter. Framing/lifecycle belongs to the local transport host.</summary>
    public sealed class McpJsonRpcAdapter
    {
        public const string ProtocolVersion = "2026-07-28";
        public const int MaximumMessageBytes = 65536;
        public const int MaximumTextLength = 256;
        private const string ToolName = "sheet-export.naming-preview";
        private const string ProtocolVersionMeta = "io.modelcontextprotocol/protocolVersion";
        private const string ClientCapabilitiesMeta = "io.modelcontextprotocol/clientCapabilities";
        private readonly InternalAutomationApi _api;

        public McpJsonRpcAdapter(InternalAutomationApi api)
        {
            if (api == null) throw new ArgumentNullException("api");
            _api = api;
        }

        /// <summary>Returns one JSON-RPC response, or null for notifications. Does not retain request/session state.</summary>
        public string HandleMessage(string message)
        {
            if (message == null) return Error(null, -32700, "Parse error");
            if (Encoding.UTF8.GetByteCount(message) > MaximumMessageBytes) return Error(null, -32600, "Request exceeds the 64 KiB limit.");
            JObject request;
            try
            {
                using (var text = new System.IO.StringReader(message))
                using (var reader = new JsonTextReader(text) { MaxDepth = 16, DateParseHandling = DateParseHandling.None })
                {
                    var loadSettings = new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, LineInfoHandling = LineInfoHandling.Ignore };
                    request = JObject.Load(reader, loadSettings);
                    if (reader.Read()) return Error(null, -32600, "Only one JSON-RPC object is allowed per message.");
                }
            }
            catch (JsonException) { return Error(null, -32700, "Malformed JSON-RPC JSON."); }
            catch (ArgumentException) { return Error(null, -32700, "Malformed JSON-RPC JSON."); }

            JToken id = request["id"];
            if (id == null)
            {
                // MCP notifications are intentionally no-ops; this adapter performs no session or cancellation work.
                return null;
            }
            if (request["jsonrpc"] == null || request["jsonrpc"].Type != JTokenType.String || (string)request["jsonrpc"] != "2.0" ||
                (id.Type != JTokenType.String && id.Type != JTokenType.Integer))
                return Error(id, -32600, "Invalid JSON-RPC request.");

            string method = request["method"] == null || request["method"].Type != JTokenType.String ? null : (string)request["method"];
            if (string.IsNullOrWhiteSpace(method)) return Error(id, -32600, "Request method is required.");
            if (!ValidateMetadata(request, out string metadataError))
            {
                if (metadataError.StartsWith("UNSUPPORTED_VERSION:", StringComparison.Ordinal))
                    return UnsupportedVersion(id, metadataError.Substring("UNSUPPORTED_VERSION:".Length));
                return Error(id, -32602, metadataError);
            }

            JToken rawParameters = request["params"];
            if (rawParameters != null && !(rawParameters is JObject)) return Error(id, -32602, "params must be an object.");
            JObject parameters = rawParameters as JObject ?? new JObject();
            switch (method)
            {
                case "server/discover": return Success(id, new JObject
                {
                    ["resultType"] = "complete",
                    ["supportedVersions"] = new JArray(ProtocolVersion),
                    ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
                    ["ttlMs"] = 300000,
                    ["cacheScope"] = "public"
                }, true);
                case "tools/list":
                    if (parameters.Properties().Any()) return Error(id, -32602, "tools/list accepts no parameters.");
                    return Success(id, new JObject
                    {
                        ["resultType"] = "complete",
                        ["tools"] = new JArray(CreateToolDefinition()),
                        ["ttlMs"] = 300000,
                        ["cacheScope"] = "public"
                    }, true);
                case "tools/call":
                    try { return CallTool(id, parameters); }
                    catch (ArgumentException ex) { return ToolError(id, ex.Message); }
                default: return Error(id, -32601, "Method not found: " + method);
            }
        }

        private string CallTool(JToken id, JObject parameters)
        {
            string name = parameters["name"] == null || parameters["name"].Type != JTokenType.String ? null : (string)parameters["name"];
            if (!string.Equals(name, ToolName, StringComparison.Ordinal)) return Error(id, -32602, "Unknown tool: " + (name ?? "<missing>"));
            if (parameters.Properties().Any(property => property.Name != "name" && property.Name != "arguments"))
                return Error(id, -32602, "Unexpected tools/call parameter.");
            JObject arguments = parameters["arguments"] as JObject;
            if (arguments == null) return Error(id, -32602, "arguments object is required.");
            string[] allowed = { "sheetNumber", "sheetName", "revision", "revisionDate", "paperSize", "orientation", "projectCode", "issueDate", "expression", "regexPattern", "unitSystem", "dryRun" };
            if (arguments.Properties().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
                return Error(id, -32602, "Unexpected tool argument; only declared typed fields are accepted.");

            string unitSystem = ReadString(arguments, "unitSystem", true);
            if (!string.Equals(unitSystem, "NotApplicable", StringComparison.Ordinal)) return ToolError(id, "unitSystem must be NotApplicable for filename preview.");
            JToken dryRunToken = arguments["dryRun"];
            if (dryRunToken == null || dryRunToken.Type != JTokenType.Boolean || !(bool)dryRunToken)
                return ToolError(id, "dryRun=true is required; this tool is preview-only.");

            string sheetNumber = ReadString(arguments, "sheetNumber", true);
            string sheetName = ReadString(arguments, "sheetName", true);
            if (string.IsNullOrWhiteSpace(sheetNumber) || sheetName == null) return ToolError(id, "sheetNumber and sheetName are required.");
            DateTime? issueDate = null;
            string dateText = ReadString(arguments, "issueDate", false);
            if (dateText != null)
            {
                DateTime parsed;
                if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                    return ToolError(id, "issueDate must use yyyy-MM-dd.");
                issueDate = parsed;
            }

            var automationRequest = new SheetNamingPreviewRequest
            {
                CapabilityId = ToolName,
                UnitSystem = AutomationUnitSystem.NotApplicable,
                DryRun = true,
                SheetNumber = sheetNumber,
                SheetName = sheetName,
                Revision = ReadString(arguments, "revision", false),
                RevisionDate = ReadString(arguments, "revisionDate", false),
                PaperSize = ReadString(arguments, "paperSize", false),
                Orientation = ReadString(arguments, "orientation", false),
                ProjectCode = ReadString(arguments, "projectCode", false),
                IssueDate = issueDate,
                Expression = ReadString(arguments, "expression", false),
                RegexPattern = ReadString(arguments, "regexPattern", false)
            };
            AutomationResult result = _api.Invoke(automationRequest);
            JObject structured = JObject.FromObject(result);
            string contentText = structured.ToString(Formatting.None);
            return Success(id, new JObject
            {
                ["resultType"] = "complete",
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = contentText }),
                ["structuredContent"] = structured,
                ["isError"] = result.Status != AutomationStatus.Succeeded
            }, true);
        }

        private static string ReadString(JObject source, string field, bool required)
        {
            JToken token = source[field];
            if (token == null) return required ? null : null;
            if (token.Type != JTokenType.String) throw new ArgumentException(field + " must be a string.");
            string value = (string)token;
            if (value != null && value.Length > MaximumTextLength) throw new ArgumentException(field + " exceeds the 256 character limit.");
            return value;
        }

        private static bool ValidateMetadata(JObject request, out string error)
        {
            error = null;
            JObject meta = request["_meta"] as JObject;
            if (meta == null) { error = "Request _meta is required."; return false; }
            string version = meta[ProtocolVersionMeta] == null || meta[ProtocolVersionMeta].Type != JTokenType.String ? null : (string)meta[ProtocolVersionMeta];
            if (!string.Equals(version, ProtocolVersion, StringComparison.Ordinal))
            {
                error = "UNSUPPORTED_VERSION:" + (version ?? "<missing>");
                return false;
            }
            if (!(meta[ClientCapabilitiesMeta] is JObject)) { error = "Client capabilities metadata is required."; return false; }
            return true;
        }

        private static JObject CreateToolDefinition()
        {
            return new JObject
            {
                ["name"] = ToolName,
                ["title"] = "Preview sheet export filename",
                ["description"] = "PREVIEW_ONLY: preview one sanitized output filename through the canonical Sheet Export naming service. No document or file is changed. Requires dryRun=true.",
                ["annotations"] = new JObject { ["readOnlyHint"] = true, ["destructiveHint"] = false, ["idempotentHint"] = true, ["openWorldHint"] = false },
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object", ["additionalProperties"] = false,
                    ["properties"] = new JObject
                    {
                        ["sheetNumber"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["sheetName"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["revision"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["revisionDate"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["paperSize"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["orientation"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["projectCode"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["issueDate"] = new JObject { ["type"] = "string", ["pattern"] = "^[0-9]{4}-[0-9]{2}-[0-9]{2}$", ["maxLength"] = 10 },
                        ["expression"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["regexPattern"] = new JObject { ["type"] = "string", ["maxLength"] = MaximumTextLength },
                        ["unitSystem"] = new JObject { ["type"] = "string", ["enum"] = new JArray("NotApplicable") },
                        ["dryRun"] = new JObject { ["type"] = "boolean", ["const"] = true }
                    },
                    ["required"] = new JArray("sheetNumber", "sheetName", "unitSystem", "dryRun")
                },
                ["outputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["Status"] = new JObject { ["type"] = "integer" },
                        ["Summary"] = new JObject { ["type"] = "string" },
                        ["Requested"] = new JObject { ["type"] = "integer" },
                        ["Affected"] = new JObject { ["type"] = "integer" },
                        ["Skipped"] = new JObject { ["type"] = "integer" },
                        ["Failed"] = new JObject { ["type"] = "integer" },
                        ["Diagnostics"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } },
                        ["Postcondition"] = new JObject { ["type"] = "boolean" },
                        ["OutputName"] = new JObject { ["type"] = new JArray("string", "null"), ["description"] = "Filename preview, or null on rejection." }
                    },
                    ["required"] = new JArray("Status", "Summary", "Requested", "Affected", "Skipped", "Failed", "Diagnostics", "Postcondition")
                }
            };
        }

        private static string Success(JToken id, JObject result, bool includeServerInfo)
        {
            var response = new JObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = result };
            if (includeServerInfo) response["_meta"] = new JObject { ["io.modelcontextprotocol/serverInfo"] = new JObject { ["name"] = "K-TOOL", ["version"] = "2.7.2" } };
            return response.ToString(Formatting.None);
        }

        private static string Error(JToken id, int code, string message)
        {
            return new JObject { ["jsonrpc"] = "2.0", ["id"] = id == null ? JValue.CreateNull() : id.DeepClone(), ["error"] = new JObject { ["code"] = code, ["message"] = message } }.ToString(Formatting.None);
        }

        private static string UnsupportedVersion(JToken id, string requested)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(),
                ["error"] = new JObject { ["code"] = -32022, ["message"] = "Unsupported protocol version", ["data"] = new JObject { ["supported"] = new JArray(ProtocolVersion), ["requested"] = requested } }
            }.ToString(Formatting.None);
        }

        private static string ToolError(JToken id, string message)
        {
            return Success(id, new JObject
            {
                ["resultType"] = "complete",
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = message }),
                ["isError"] = true
            }, true);
        }
    }
}
