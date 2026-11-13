# 实例承载关系协议与操作说明

实现版本：1.1.0-rc4（文件版本 1.1.0.3）；关系格式：1.1；目标联调环境：SOLIDWORKS 2025 SP5.0。可读取 rc1/rc2/rc3 的 1.0 关系；新增或修改关系后使用支持多父的 1.1 格式，旧插件不能编辑新存储。

## 组件选择与绑定

1. 打开已保存、可写的总装配，在 Asset / assembly_package 导出命令组中选择“承载关系”。面板先显示当前配置已保存的关系。
2. 在 SOLIDWORKS 图形区或原生设计树中选一个父资产，点击“读取父组件”。需要减少图形区干扰时，可点击“仅显示普通资产”，在当前视图临时隔离可见的普通资产。点击“恢复全部组件”或关闭面板，恢复隔离前的视图。
3. 在 SOLIDWORKS 图形区 Ctrl 多选或框选子组件，点击“添加所选子组件”。同一资产上的多个面会合并为一个实例；无法参与绑定的选择项会提示跳过。需要逐个选取时，勾选“连续点选子组件”。
4. 枪头等重复对象可用“同级同款批量添加”。以当前选择、待绑定列表中的选中项或最后添加项为起点，读取其直接 CAD 父级下同源同版本的可绑定实例。
5. 点击“应用绑定”核对并提交整批关系。插件按父、子实例区分关系，同一对只保留一条。新增关系通过接口校验后即可绑定，已有关系的接口差异会在提交后标注在子组件明细的状态列，悬停可查看实际接口和父组件可接受的接口。若要替换子组件的全部父级，使用“替换全部父组件…”。
6. 在关系区搜索、选择并高亮或解除指定父子关系。点击“校验已有关系”时，插件读取关系所涉及的实例和接口。启用“链式绑定”可将已绑定子组件作为下一轮父组件。
7. 点击“保存总装配”或按 Ctrl+S 写入关系，再重新分类预览、导出。切换文档或配置后，请重新打开承载关系面板。

只有普通 Asset 实例可参与绑定。选中 Asset 内部的几何时，选择归到包含它的外层 Asset 实例。

承载关系面板默认显示“资产关系”。按 Asset UUID 和版本汇总父资产、子资产及关联的实例关系数。切换到“实例绑定”可修改具体的承载实例。

点选普通资产时，图形区用临时彩色轮廓区分点中的实例、它的直接父实例和直接子实例、同资产的其他实例，以及资产级父、子资产的其余可见实例。资产级关系汇总当前配置内同资产实例的有效承载关系，跨越 CAD 分组。连续点选子组件时会暂停自动高亮。

## 维护接口与槽位

在承载关系面板顶部点击“接口属性 / 建议…”，可编辑本轮父子组件或所选资产的文件级接口和槽位。源文件相同的实例合并编辑。可以根据已绑定或本轮待绑定的直接子组件追加接受接口建议。保存前查询当前资产版本的发布状态，已发布时使用新的资产版本。

Agent 通过 `asset_update_interfaces` 维护资产文件级接口和槽位。启用 `sync_accepts_from_bindings` 时，插件追加当前配置已绑定子级的连接接口。已有接受接口保留，移除关系时仍保留已声明的兼容能力。`slots_num` 记录包含空槽的总容量，由 agent 根据设计数据确认。源文件属性作用于所有同源实例。

插件保存前检查源文件身份、未保存修改、只读状态和链接表达式。每次属性写入前，源文件备份到 `%LOCALAPPDATA%\SolidWorksAssetExporter\PropertyBackups\<唯一目录>\`。保存失败时插件尝试恢复内存属性，并保留源文件打开供检查。工具参数和本机接口库见 [MCP 使用指南](MCP.md)。

## 存储与身份

总装配第三方流 `SWAssetExporter.Bindings.v1` 保存全部配置的关系。配置使用 SW 的持久配置 ID 隔离；新建/复制配置不继承，改名保持同一 ID。配置 ID 的跨会话和改名语义见 [IConfiguration.GetID](https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IConfiguration~GetID.html)。

端点使用总装配上下文中的 GetPersistReference3/GetObjectByPersistReference3，不以导出 node.id、名字、UUID、距离进行恢复。解析成功后还校验完整祖先组件 ID 上下文及源文件身份/路径，不能确认时要求解除重绑。源路径与创建信息仅为替换检测的附加保护，不用于搜索替代实例。依据：[持久引用说明](https://help.solidworks.com/2019/english/api/sldworksapiprogguide/Overview/Persistent_Reference_IDs.htm)。

只在 SaveToStorageNotify 中写入第三方流；成功写入且收到普通保存/另存为的 FileSavePostNotify 后才清除待保存状态。取消、写入失败和另存副本不确认原文档已保存。业务待保存状态优先于导出器内部解析 dirty 豁免。依据：[IGet3rdPartyStorage](https://help.solidworks.com/2021/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IModelDoc2~IGet3rdPartyStorage.html)。

同一路径、同一源模型的替换操作能否在所有 SW 编辑方式下保留旧持久引用仍需专项验收；不承诺任意替换操作天然维持身份。Pack and Go/源文件改名、另存到其他目录后，路径保护可能要求显式重绑。新增/复制配置的 ID 隔离已在实机验证；复杂 PDM 与多 SW 版本未声明支持。

## 导出文件

新增 `assembly-relations.json`，所有新装配包均输出，未绑定时 `relations=[]`。字段定义见 [JSON Schema](assembly-relations.schema.json)，旧版 1.0 真实 SW 导出样例见 [examples/instance-binding](../examples/instance-binding/README.md)。当前生产端输出 1.1 契约以明确多父语义；万象消费方需支持该格式及多父图，本仓库不包含消费方代码，远端导入联调需独立完成。

| 字段 | 约定 |
| --- | --- |
| schema_version | 新导出固定 `1.1`；验证器兼容旧 `1.0` 单父关系文件 |
| assembly_uuid / assembly_version | 与同包 XML、export-report 一致 |
| length_unit / quaternion_order | 固定 `m` / `xyzw` |
| relations[].parent_node_id / child_node_id | 同包 XML 中唯一的普通 Asset 实例 ID；一个 child 可关联多个不同 parent，每个父子对最多一条 |
| relations[].pose | tx/ty/tz/qx/qy/qz/qw；子模型原点相对承载父模型原点 |

位姿为 `inverse(T_world_parent) * T_world_child`。使用 SW 顶层装配上下文的 Transform2；该 API 已包含嵌套祖先变换，不能再次连乘祖先。依据：[IComponent2.Transform2](https://help.solidworks.com/2019/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.IComponent2~Transform2.html)。原 XML 的 CAD parent_id/pose 不变。接口偏移和空槽位不在本协议中。

关系按父 ID、子 ID 排序；允许多父形成有向无环图，禁止自身、环、重复父子对。每条边独立计算相对父组件的位姿，共享子实例只在 CAD XML 中保留一个节点。接口名称按属性协议精确匹配，接受列表用英文分号分隔。连接双方还校验现有角色属性：机器人端快换盘只接受工具端快换盘；快换架接受工具端快换盘，也可直接承载 Tool（例如移液器）；工具端快换盘接收 Tool；机器人端快换盘不作为本功能的被承载子对象。编辑器、预览和导出共用此校验。

快换架直接承载移液器时，支架的 `accepts_interfaces` 必须包含移液器的 `connection_interface`，无需取消支架的 `is_quick_changer_rack=1`。支架仍须为 `class=station`。若移液器实例已经由工具侧快换盘承载，可以“应用绑定”再添加支架作为父组件，保留两个父级；若只想保留支架，则用“替换全部父组件…”。原有“支架→工具侧快换盘→移液器”链仍然有效；已有直接承载关系可重新校验，无需重新绑定。

子组件为 Tool 时，`工具安装.移液器` 与 `移液器` 在接口校验中等价。

隐藏、抑制、包络、删除、替换、失去独立 Asset 身份或变为接口不兼容的已绑定实例不会被静默移除，阻止装配包导出，用户可恢复对象或显式解除。未绑定对象继续正常导出。

export-report 输出 `relations_schema_version=1.1`，必须与关系文件的格式版本一致；文件清单包含关系文件大小/SHA-256。复用及上传前校验关系文件、同包身份、端点和图结构。验证器仍支持版本一致的旧 1.0 单父包；多父包不得标成 1.0。ZIP 沿用原流程，包含原样关系文件，不新增远端完成标记或原子发布承诺。

格式标识及排序后的关系和位姿进入装配包指纹。首次迁移、绑定变更或位姿变化导致指纹变化时，插件为已有包分配新的 `assembly_version`。预览后修改关系需要保存并重新预览。

## 安装与本机升级

首次安装：关闭 SOLIDWORKS，完整解压发布 ZIP，以管理员身份运行 `Install.cmd`。

已存在系统 Add-in 注册与每用户 COM 注册时，可以关闭 SOLIDWORKS 后运行 `Install-UserUpdate.ps1`。该脚本将 DLL 放到 `%LOCALAPPDATA%\SolidWorksAssetExporter\Addin`，备份原 CodeBase，并更新当前用户的 COM 路径；它不会改动系统 DLL。它是现有安装的升级方式，不能替代首次系统注册。

每用户升级回退：关闭 SOLIDWORKS 后，用 `Restore-UserUpdate.ps1 -BackupFile <安装输出的 com-codebases.json>` 恢复原注册路径和已有用户载荷。需卸载时，先回退每用户升级，再使用原系统卸载脚本。

完整 P0 验收进度见 [验收记录](INSTANCE_BINDING_TEST_RESULTS.md)。候选包不表示所有交互、破坏性变更和 P1 工作流均已验收。
