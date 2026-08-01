# Sophie 桌面宠物

[![Build](https://github.com/hackermengzhi/sophie-desktop-pet/actions/workflows/build.yml/badge.svg)](https://github.com/hackermengzhi/sophie-desktop-pet/actions/workflows/build.yml)
[![macOS](https://img.shields.io/badge/macOS-12%2B-black?logo=apple)](https://github.com/hackermengzhi/sophie-desktop-pet/releases)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

> 一个具有透明背景、天气、完整生活动作和可展开像素浮岛的 macOS
> 桌面宠物。

[English](README.md)

![Sophie 在迷你草坪上休息](docs/screenshots/desktop-pet.png)

## 功能

- macOS 原生透明、无边框、始终置顶的小桌宠窗口。
- 20 种四帧 Sophie 像素动画：待机、左右行走、坐下、吃东西、睡觉、
  浇花、看书、雨天打伞、堆雪人、冷天穿小棉袄、热天擦汗、背包郊游、喜欢表情
  和开心跳跃，以及修理更新机器人、拿纸坐马桶、脸红着急后躲进草丛、拿金色
  小麦克风唱歌，以及蹲下来摸猫。缩小状态平时不显示猫，只在随机互动动作
  中出现；打开小世界后，猫会作为独立角色眨眼摆尾、散步和蜷睡。
- 每帧固定显示 500ms；每次待机 20 秒后保证播放一个非待机随机动作。
  马桶动作完成后第四帧会停留约 15 秒，草丛动作第四帧约 13 秒。
  Sophie 的睡觉概率会随昼夜和天气变化：晴朗白天最低，夜间较高，但不会
  长时间频繁占用生活动作。
- 通过 [ipwho.is](https://ipwho.is/) 自动估算大致位置，并从
  [Open-Meteo](https://open-meteo.com/) 获取当前天气。
- 天气每 15 分钟刷新一次；失败后会在 2 分钟后重试。
- 待机始终保持正面 Sophie；晴天自动天气时，冷天会随机穿上小棉袄、热天会随机
  擦汗，雨雪则优先出现打伞或堆雪人。不显示具体温度数字；右侧对称的像素
  温度计用四档颜色与高度表示温度。
- 手动选择天气会暂停自动天气，直到选择“恢复自动定位天气”。
- 普通模式只在脚下的小草坪活动，不会横穿整个桌面。
- 双击 Sophie 可以展开或收起她的小世界。
- 小世界支持昼夜、云随风移动、萤火虫、像素雨和像素雪。
- 晴朗白天还会随机背上小包出发、露出喜欢的表情，或高兴地跳一下；它们和
  其他生活动作一样自动发生，不放进手动菜单。
- 启动时及每小时检查 GitHub 最新正式版；发现新版后自动下载并校验
  SHA-256。检查、下载和安装时，Sophie 会在桌边修理小机器人，下方像素
  进度条会显示扫描动画或真实下载进度；下载展示至少持续 20 秒，让维修动画
  完整播完。只有点击右键菜单中的安装项才会替换应用并重启。
- 提供 Apple Silicon 自包含构建，不需要另装 .NET。

## 小世界

![Sophie 的像素浮岛](docs/screenshots/little-world.png)

在小世界中，Sophie 会先走到适合的位置，再坐下、吃东西、睡觉、浇花、
看书或进行与天气相符的活动。人物脚底沿岛内地面透视线移动，并仅使用
轻微纵深缩放。坐下和堆雪人都是单向动作，完成后保持最后姿势，不会倒放
循环。

## 操作

| 操作 | 功能 |
| --- | --- |
| 左键拖动 | 移动 Sophie 或小世界窗口 |
| 双击 Sophie | 展开或收起小世界 |
| 右键 | 设置自动生活、昼夜、天气、安装更新或退出 |

生活动作全部自动发生，不提供手动点播菜单。

## 自动更新

更新器只接受名称为 `Sophie-Desktop-Pet-macOS-arm64.zip` 的唯一正式版
附件，并在安装前验证附件大小、GitHub 提供的 SHA-256 摘要、App 结构和
代码签名。安装失败时会恢复旧版本。如果 macOS 通过 App Translocation
隔离路径运行 Sophie，或当前目录不可写，验证过的新版本会自动安装到
`~/Applications` 并从那里重新启动。

更新器匿名读取这个公开仓库，不会请求、保存、嵌入或记录 GitHub 凭据。

## 编译

需要 macOS 12+ 和 .NET 10 SDK：

```bash
dotnet restore SophieDesktopPet/SophieDesktopPet.csproj
dotnet build SophieDesktopPet/SophieDesktopPet.csproj -c Release
```

发布 Apple Silicon 自包含版本：

```bash
dotnet publish SophieDesktopPet/SophieDesktopPet.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

普通 Build 工作流只验证安装包，不再提供容易混淆的 Actions artifact；
GitHub 会给 artifact 强制再套一层 ZIP。给用户下载的成品只放在仓库的
Releases 页面，下载后解压一次就是 App。

创建正式版时，先同步 `SophieDesktopPet.csproj` 中的 `Version`，再推送
同版本标签，例如 `v1.5.0`。`release.yml` 会自动构建、签名、验证、
保存 Actions artifact，并创建带 ZIP 和 SHA-256 文件的 GitHub Release。

新增已经确认的生活动作时，使用精简的
[动作导入 SOP](docs/ACTION_IMPORT_SOP.zh-CN.md) 和经过校验的 JSON 配置，
无需重复描述图集、时序和发布规则。

## 隐私

应用不包含统计分析、广告、账号或后台数据收集。网络请求仅用于通过
[ipwho.is](https://ipwho.is/) 获取大致位置、通过
[Open-Meteo](https://open-meteo.com/) 获取当前天气，以及从 GitHub
检查和下载正式版更新。

## 许可证

C# 源代码采用 [MIT License](LICENSE)。

Sophie 的人物形象及生成美术素材不属于 MIT 授权范围。转载、重新发布或
复用素材前请阅读 [NOTICE.md](NOTICE.md)。
