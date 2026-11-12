using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorksAssetExporter.Mcp;

namespace SolidWorksAssetExporter.Core.Tests
{
    internal static partial class Program
    {
        private static void RunMcpTests()
        {
            Run("MCP negotiates protocol, accepts notifications and advertises model tools", delegate
            {
                Func<string, Dictionary<string, object>, object> unused = (name, args) =>
                    { throw new InvalidOperationException("No tool dispatch expected."); };
                var initialize = McpProtocol.Respond(
                    "{\"jsonrpc\":\"2.0\",\"id\":\"init\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\"}}", unused);
                var response = ParseMcp(initialize);
                Equal("init", response["id"]);
                Equal("2025-06-18", McpProtocol.Object(response["result"])["protocolVersion"]);
                Equal<string>(null, McpProtocol.Respond(
                    "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", unused));
                var listing = ParseMcp(McpProtocol.Respond("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", unused));
                var tools = ((object[])McpProtocol.Object(listing["result"])["tools"]).Select(McpProtocol.Object).ToList();
                True(tools.Any(tool => Equals(tool["name"], "binding_add")));
                True(tools.Any(tool => Equals(tool["name"], "asset_update_interfaces")));
                True(tools.Any(tool => Equals(tool["name"], "asset_export")));
            });

            Run("MCP validates write arguments before dispatch and preserves Unicode", delegate
            {
                var calls = 0;
                Func<string, Dictionary<string, object>, object> call = (name, args) =>
                {
                    calls++;
                    Equal("asset_update_interfaces", name);
                    Equal(24, args["slots_num"]);
                    Equal("试管-D20", ((object[])args["accepts_interfaces"]).Single());
                    return new { slots_num = args["slots_num"] };
                };
                var request = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":" +
                    "{\"name\":\"asset_update_interfaces\",\"arguments\":{\"document_path\":\"assembly\",\"configuration\":\"Default\"," +
                    "\"instance\":\"rack-1\",\"source_path\":\"rack\",\"accepts_interfaces\":[\"试管-D20\"],\"slots_num\":24}}}";
                var result = McpProtocol.Object(ParseMcp(McpProtocol.Respond(request, call))["result"]);
                Equal(false, result["isError"]);
                Equal(-32602, McpProtocol.Object(ParseMcp(McpProtocol.Respond(request.Replace(":24", ":-1"), call))["error"])["code"]);
                Equal(1, calls);
                result = McpProtocol.Object(ParseMcp(McpProtocol.Respond(request, (name, args) =>
                    { throw new InvalidOperationException("Save failed."); }))["result"]);
                Equal(true, result["isError"]);
            });
        }

        private static Dictionary<string, object> ParseMcp(string response)
        {
            return McpProtocol.Object(McpProtocol.Json().DeserializeObject(response));
        }
    }
}
