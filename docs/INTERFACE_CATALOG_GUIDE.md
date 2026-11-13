# 接口候选库与只读属性模板

候选库使用接口库格式 1.0。可从承载关系面板的“接口属性 / 建议…”或通过 MCP 读取和维护候选。

## 旧属性保留

插件继续读取已有文件级 `connection_interface`、`accepts_interfaces` 和 `slots_num`。候选库保存在当前用户的 `%APPDATA%\SolidWorksAssetExporter\interfaces.json`。

## 命名方式

采用 `类别.规格[.关键尺寸][.厂商或型号]`，例如用户指定的 `试管.15ml.D20`。容量统一使用 `ml` 或 `ul`，尺寸默认 mm，`D` 表示事先约定的圆形定位/承载部位直径。可追加 `H120` 或 `底座L80xW60` 等字段，但这些只是你确认的尺寸定义，不是插件测量结果。

候选库包含 103 个基础名称，覆盖试管、离心管、冻存管、锥形瓶、培养瓶/皿、试剂瓶、储液槽、孔板、PCR、枪头/盒、移液管、架/底座、托盘/载台、快换及工具安装。`A型`/`B型` 是可自行定义的接口系列，不是厂商通用标准。

这些是录入候选，不是经过验证的兼容性数据库：相同容量或孔数的不同厂家可能具有不同外形、口径和定位结构。采用基础名称前应确认它在项目内代表同一种实际接口；有差异时增加尺寸或型号，分别命名。没有尺寸的候选没有暗含默认直径。`试管.15ml.D20` 是命名示例，不声明所有 15 ml 试管直径均为 20 mm。

父级 `accepts_interfaces` 中的值必须与子级 `connection_interface` 一致，包括大小写和单位写法。多个接受接口在模型中使用英文分号分隔，在 MCP 中通过数组传入。`slots_num` 记录包含空槽的总容量。

## 使用

Agent 用 `interface_catalog` 查询候选，必要时用 `interface_catalog_add` 加入已确认的名称，再通过 `asset_update_interfaces` 应用到资产。详见 [MCP 使用指南](MCP.md)。

## 模板修改范围

零件 `part.prtprp` 和装配 `asem.asmprp` 的接口和槽位控件使用 `ReadOnly="True"`。装配包版本为只读，模板仅在 Robot 分类显示外部引用版本输入项。原生“文件 → 属性”和 SOLIDWORKS API 仍拥有文件编辑权限。

## 应用补充包

`Install-InterfaceOptions.ps1` 更新现有模板中的相关控件，并合并当前 Windows 用户的候选名称。默认仅预览，指定 `-Apply` 才实际更新。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-InterfaceOptions.ps1 -TemplateDirectory "C:\C12_SolidWorksTemplate\自定义属性"
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-InterfaceOptions.ps1 -TemplateDirectory "C:\C12_SolidWorksTemplate\自定义属性" -Apply
```

更新前备份所有将改写的文件，输出备份目录及 `manifest.json`；其中记录原路径、原哈希和是否原本存在。接口库使用与插件一致的互斥文件锁及临时文件替换，模板按文件替换；若中途失败，已成功更新的文件会保留改动，按备份清单恢复即可，不承诺跨文件原子更新。

在自定义属性任务窗格重新选择模板或按 F5 刷新。模板按文档类型显示的机制见 [SOLIDWORKS 官方说明](https://help.solidworks.com/2022/english/SolidWorks/oh_propertytabbuilder/t_tab_builder_creating_tab.htm)。
