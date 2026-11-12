# SOLIDWORKS Asset Exporter MCP

更新插件后启动 SOLIDWORKS，在同一 Windows 用户和桌面会话中启动 MCP 客户端。标准安装路径为 `%ProgramData%\SolidWorksAssetExporter\SolidWorksAssetExporter.Mcp.exe`。已有按用户更新的安装使用 `%LOCALAPPDATA%\SolidWorksAssetExporter\Addin\SolidWorksAssetExporter.Mcp.exe`。从源码构建使用 `scripts/build-addin.ps1`，可执行文件与 AddIn DLL 输出在同一目录。

MCP 客户端配置示例

```json
{
  "mcpServers": {
    "solidworks-assets": {
      "command": "C:\\ProgramData\\SolidWorksAssetExporter\\SolidWorksAssetExporter.Mcp.exe",
      "args": []
    }
  }
}
```

多个 SOLIDWORKS 进程同时运行时，在 `args` 中指定 `["--pid", "12345"]`。大型装配导出时，根据客户端配置延长工具超时时间。通信通过标准输入输出和当前用户的本地命名管道，SOLIDWORKS 操作由插件调度到其界面线程。

## Agent 操作顺序

1. 调用 `assembly_context` 读取活动装配的 `document_path` 和 `configuration`。其余装配工具均传入这两个值，切换文档或配置后重新读取。开始写入前保存 CAD 修改。
2. 用 `asset_list` 查找资产。用返回的 `instance` 区分同一源文件的不同实例，维护属性时同时传入 `source_path`。根资产的 `instance` 为空字符串。列表支持 `offset` 和 `limit`。
3. 用 `asset_update_interfaces` 保存子资产的 `connection_interface`，以及父资产的 `accepts_interfaces` 和 `slots_num`。省略的字段保留原值，空字符串和空数组可以清除可选接口。源文件属性作用于该文件的所有实例。普通资产当前版本已发布时，插件查询 Wanxiang 并分配新版本。Robot 的外部引用版本由其 URDF 发布方决定。
4. 用 `binding_add` 传入 `parent` 和 `children`。工具保留已有父级并保存当前配置的关系。用 `binding_list` 检查端点和接口问题。对已有绑定的父资产，`asset_update_interfaces` 可设置 `sync_accepts_from_bindings=true`，追加已绑定子资产声明的接口。
5. 用 `asset_export_preview` 检查导出计划。用 `asset_export` 保存必要的版本变更并导出，工具返回本地目录、资产数量及版本变更。显式传入 `upload: true` 时，工具使用插件保存的 Wanxiang 设置上传，并单独返回上传结果或错误。

`slots_num` 是资产的总容量，包含空槽。Agent 根据设计数据确认数值后传入插件。未知时省略该字段。接口候选可通过 `interface_catalog` 查询，通过 `interface_catalog_add` 追加经过确认的名称。

导出工具使用插件保存的 Asset 库目录、装配包目录和 Wanxiang 凭据。初次使用时在导出窗口保存这些设置。默认根据已保存设置决定是否导出装配包，也可用 `export_project` 覆盖。装配包版本根据配置的本地装配包目录和内容指纹维护，普通资产版本以 Wanxiang 注册表为准。

## 属性模板

安装包中的 `Install-CustomPropertyTemplates.ps1` 可部署新版模板。已经定制过模板时，可用 `Install-InterfaceOptions.ps1 -TemplateDirectory <目录> -Apply` 就地更新控件。接口和槽位设为只读，普通资产及装配包版本由插件维护。SOLIDWORKS 原生文件属性和 API 仍拥有编辑权限，日常流程统一使用插件工具。详情见 [接口库与模板说明](INTERFACE_CATALOG_GUIDE.md)。
