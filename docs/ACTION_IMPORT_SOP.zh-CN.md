# 新增生活动作 SOP

这份协议用于把一张**已经由用户确认**的四帧横向动作图导入 Sophie。
目标是减少重复说明和上下文消耗，同时避免改错旧图集、动作时序或发布状态。

## 最短指令

```text
按 docs/ACTION_IMPORT_SOP.zh-CN.md 导入 <图片绝对路径>：
state=<PascalCase 状态名>，slug=<kebab-case 名称>；其余默认。本地预览后等我确认。
```

如果用户已经明确说“可以”或“定稿”，将最后一句改成：

```text
其余默认，接入并安装本地版；不要推送。
```

只有用户当次明确要求推送，才允许 commit、push、tag 或创建 Release。

## 固定默认值

- PNG 横向四帧；最终单元格 `192x208`，图集宽 `768`。
- 每帧 `500ms`；主计时器 `100ms`，所以每帧保持 5 tick。
- 非循环动作持续 20 tick，四帧恰好播放一次，然后回到 20 秒待机。
- 同时加入透明桌面模式和小世界；不增加手动点播菜单。
- 晴朗白天触发概率默认 7%，其他环境默认 5%。
- 新动作追加到图集最后一行；旧行必须逐像素保持不变。
- 使用统一缩放和基线，默认缩放 `0.30`、底部留白 6px。
- 抠除近洋红背景，删除小于 20 像素的孤立抠图碎屑。
- 版本只升 patch；安装前备份当前 App。
- 未经当次明确授权，不推送、不打 tag、不发布 Release。

## 配置文件

从 [`art/action-import.template.json`](../art/action-import.template.json)
复制一份到动作目录，命名为 `import.json`。通常只需填写：

- `state`：C# 状态名，例如 `Singing`。
- `slug`：文件名用短名，例如 `singing`。
- `source`：仓库内已确认原图路径。

可选字段只在需要覆盖默认值时填写：`scale`、`row`、`boxes`、`loops`、
`probability`、`islandX`。

先运行：

```bash
python3 tools/validate_action_import.py art/<slug>-action/import.json
```

校验器输出的 `resolved` 对象是本次导入的唯一参数来源；不要再次猜测。

## 导入步骤

1. 未确认的图只放 `art/drafts/`，展示给用户后停止；确认后才复制到动作目录。
2. 创建并校验 `import.json`；检查人物身份、两只手、道具接触、四帧顺序和无裁切。
3. 在 `tools/composite_generated_life_rows.py` 中按配置切帧、统一缩放、抠透明并追加新行。
4. 在 `PetState.cs` 增加状态和图集行映射；默认 `Loops=false`。
5. 在 `PetWindow.cs` 的可爱动作选择器中接入桌面/小世界概率；默认活动 20 tick。
6. 构建图集后逐像素比较旧行；只允许新增行变化，并生成 PNG 与 500ms GIF 预览。
7. 检查每帧非空、Alpha、边缘留白、洋红残留、孤立碎点、肢体数量和动作连续性。
8. 升 patch 版本，运行 `dotnet build` 和 macOS 打包校验；按用户授权决定仅本地安装或推送发布。

## 必须通过

```text
旧图集行未变化
新行 4 帧非空且不贴边
生产 PNG 有 Alpha，残留洋红像素为 0
四帧 × 500ms = 20 tick，无末帧僵停
dotnet build：0 warning / 0 error
ZIP、SHA-256、Info.plist、架构和 codesign 校验通过
```

## 最终回复格式

只报告：动作名、版本、触发概率、播放时序、QA 结果、本地安装/远端发布状态，
并附生产 GIF 或预览 PNG。不要重复整个实现过程。
