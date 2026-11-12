using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SolidWorksAssetExporter.Mcp
{
    internal sealed class McpTool
    {
        internal readonly string Name;
        internal readonly string Description;
        private readonly IDictionary<string, object> _properties;
        private readonly string[] _required;
        private readonly bool _readOnly;

        internal McpTool(string name, string description, bool readOnly, string[] required,
            params string[] optional)
        {
            Name = name;
            Description = description;
            _readOnly = readOnly;
            _required = required.Select(FieldName).ToArray();
            _properties = required.Concat(optional).ToDictionary(FieldName, Schema);
        }

        internal object Definition()
        {
            return new
            {
                name = Name, description = Description,
                inputSchema = new { type = "object", properties = _properties, required = _required, additionalProperties = false },
                annotations = new { readOnlyHint = _readOnly }
            };
        }

        internal void Validate(Dictionary<string, object> args)
        {
            foreach (var name in _required)
                if (!args.ContainsKey(name)) throw new ArgumentException("缺少参数 " + name + "。");
            foreach (var arg in args)
            {
                object schema;
                if (!_properties.TryGetValue(arg.Key, out schema))
                    throw new ArgumentException("未知参数 " + arg.Key + "。");
                var type = ((Dictionary<string, object>)schema)["type"] as string;
                if (type == "string" && arg.Value is string ||
                    type == "integer" && arg.Value is int && (int)arg.Value >= 0 ||
                    type == "boolean" && arg.Value is bool ||
                    type == "array" && arg.Value is object[] && ((object[])arg.Value).All(value => value is string))
                    continue;
                throw new ArgumentException("参数 " + arg.Key + " 的类型或取值无效。");
            }
        }

        private static string FieldName(string field) { return field.Split(':')[0]; }

        private static object Schema(string field)
        {
            var type = field.Split(':').Last();
            var schema = new Dictionary<string, object> { { "type", type == "strings" ? "array" : type } };
            if (type == "strings") schema.Add("items", new { type = "string" });
            if (type == "integer") schema.Add("minimum", 0);
            return schema;
        }
    }

    internal static class McpProtocol
    {
        private static readonly string[] Target = { "document_path:string", "configuration:string" };
        private static readonly string[] Paging = { "offset:integer", "limit:integer" };
        private static readonly McpTool[] Tools =
        {
            new McpTool("assembly_context", "读取活动 SOLIDWORKS 装配文档路径、配置和保存状态。", true, new string[0]),
            new McpTool("interface_catalog", "读取当前 Windows 用户的接口候选名称。", true, new string[0]),
            new McpTool("interface_catalog_add", "向当前 Windows 用户的接口候选库追加名称。", false, new[] { "names:strings" }),
            new McpTool("asset_list", "列出活动装配内的资产实例及文件级接口、容量和版本。limit 默认 100，最多 500。", true, Target, Paging),
            new McpTool("binding_list", "读取活动配置的承载关系和端点校验结果。limit 默认 100，最多 500。", true, Target, Paging),
            new McpTool("binding_add", "新增并保存显式承载关系，保留已有父级。parent 和 children 使用 asset_list 的 instance。重复关系可安全重试。", false,
                Target.Concat(new[] { "parent:string", "children:strings" }).ToArray()),
            new McpTool("asset_update_interfaces", "维护资产文件的向上接口、兼容接口和总槽位。省略的字段保留，空字符串或空数组可清除接口。sync_accepts_from_bindings 追加当前配置已绑定子级声明的接口。slots_num 是已确认的总容量。", false,
                Target.Concat(new[] { "instance:string", "source_path:string" }).ToArray(),
                "connection_interface:string", "accepts_interfaces:strings", "slots_num:integer", "sync_accepts_from_bindings:boolean"),
            new McpTool("asset_export_preview", "只读检查活动装配的导出计划和版本。使用插件已保存的目录和 Wanxiang 配置。", true,
                Target, "export_project:boolean"),
            new McpTool("asset_export", "维护版本、重新校验并导出资产。export_project 默认使用插件设置。upload=true 时按插件设置上传至 Wanxiang。", false,
                Target, "export_project:boolean", "upload:boolean")
        };

        internal static string PipeName(int processId) { return "SolidWorksAssetExporter.Mcp.v1." + processId; }

        internal static JavaScriptSerializer Json()
        {
            return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 64 };
        }

        internal static StreamReader Reader(Stream stream)
        {
            return new StreamReader(stream, new UTF8Encoding(false, true), false, 4096, true);
        }

        internal static StreamWriter Writer(Stream stream)
        {
            return new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true, NewLine = "\n" };
        }

        internal static Dictionary<string, object> Object(object value)
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary == null) throw new ArgumentException("需要 JSON 对象。");
            return dictionary;
        }

        internal static object Value(IDictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) ? value : null;
        }

        internal static Dictionary<string, object> ToolArguments(string name, object arguments)
        {
            var tool = Tools.SingleOrDefault(item => item.Name == name);
            if (tool == null) throw new ArgumentException("未知 MCP 工具：" + name);
            var args = arguments == null ? new Dictionary<string, object>() : Object(arguments);
            tool.Validate(args);
            return args;
        }

        internal static string Respond(string line, Func<string, Dictionary<string, object>, object> call)
        {
            var json = Json();
            object id = null;
            object value;
            try { value = json.DeserializeObject(line); }
            catch (ArgumentException) { return json.Serialize(Error(null, -32700, "Parse error")); }
            catch (InvalidOperationException) { return json.Serialize(Error(null, -32700, "Parse error")); }
            var request = value as Dictionary<string, object>;
            if (request == null) return json.Serialize(Error(null, -32600, "Invalid request"));
            var method = Value(request, "method") as string;
            var hasId = request.TryGetValue("id", out id);
            if (!Equals(Value(request, "jsonrpc"), "2.0") || string.IsNullOrWhiteSpace(method) ||
                (hasId && !(id is string || id is int || id is long)))
                return json.Serialize(Error(null, -32600, "Invalid request"));
            if (!hasId) return null;

            try
            {
                var rawParams = Value(request, "params");
                var parameters = rawParams == null ? new Dictionary<string, object>() : Object(rawParams);
                object result;
                switch (method)
                {
                    case "initialize":
                        var version = Value(parameters, "protocolVersion") as string;
                        if (string.IsNullOrEmpty(version)) throw new ArgumentException("protocolVersion is required.");
                        var supported = new[] { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" };
                        result = new
                        {
                            protocolVersion = supported.Contains(version) ? version : supported[0],
                            capabilities = new { tools = new { listChanged = false } },
                            serverInfo = new { name = "solidworks-asset-exporter", version = "1.0.0" }
                        };
                        break;
                    case "ping": result = new { }; break;
                    case "tools/list": result = new { tools = Tools.Select(tool => tool.Definition()).ToArray() }; break;
                    case "tools/call":
                        var name = Value(parameters, "name") as string;
                        var args = ToolArguments(name, Value(parameters, "arguments"));
                        try { result = Content(json.Serialize(call(name, args)), false); }
                        catch (Exception ex) { result = Content(ex.Message, true); }
                        break;
                    default: return json.Serialize(Error(id, -32601, "Method not found"));
                }
                return json.Serialize(new { jsonrpc = "2.0", id = id, result = result });
            }
            catch (ArgumentException ex) { return json.Serialize(Error(id, -32602, ex.Message)); }
        }

        private static object Content(string text, bool error)
        {
            return new { content = new[] { new { type = "text", text = text } }, isError = error };
        }

        private static object Error(object id, int code, string message)
        {
            return new { jsonrpc = "2.0", id = id, error = new { code = code, message = message } };
        }
    }
}
