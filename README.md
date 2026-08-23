# StarPie (星芒轮盘)

### 现代化 Windows 鼠标轮盘笔势

**Next-Generation Radial Pie Menu & Mouse Gestures for Windows 10/11**

![Release Version](https://img.shields.io/badge/Release-v1.3.8-2563EB.svg?style=flat-square&logo=github)

![Platform](<https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4.svg?style=flat-square&logo=windows>)

![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg?style=flat-square&logo=dotnet)

![License](https://img.shields.io/badge/License-MIT-10B981.svg?style=flat-square)

![Tests](https://img.shields.io/badge/Tests-15%2F15%20Passed-success.svg?style=flat-square&logo=pytest)

![Language](https://img.shields.io/badge/Language-zh--CN%20%7C%20zh--TW%20%7C%20en%20%7C%20ja-8B5CF6.svg?style=flat-square)

[🚀 快速开始](#-%E5%BF%AB%E9%80%9F%E5%BC%80%E5%A7%8B--%E4%B8%8B%E8%BD%BD) • ✨ 核心特性 • [🎨 视觉定制](#-%E5%A4%9A%E5%BD%A2%E6%80%81%E4%B8%8E%E5%85%89%E6%99%95%E8%A7%86%E8%A7%89) • [🌐 多语言](#-%E5%A4%9A%E8%AF%AD%E8%A8%80%E5%9B%BD%E9%99%85%E5%8C%96-internationalization) • [🛠️ 开发者指南](#-%E6%9C%AC%E5%9C%B0%E6%9E%84%E5%BB%BA%E4%B8%8E%E5%BC%80%E5%8F%91) • [📋 更新日志](CHANGELOG.md)

---

## 📖 简介 (Introduction)

**StarPie (星芒轮盘)** 是一款专为 Windows 10 / 11 打造的**高性能、高颜值、低延迟**现代鼠标轮盘手势（Radial / Pie Menu）系统。

在任意桌面或应用程序中，只需**按住鼠标右键轻轻滑动**，即可在光标所在位置瞬间呼出 60FPS 丝滑液态轮盘。配合 4/8/12 方位动作映射、多程序专属配置（Per-App Profiles）、快捷键录制、程序极速启动与全光谱发光特效，将重复冗长的操作化为指尖瞬时的肌肉记忆。

> 🟢 **免安装即开即用**：提供独立单文件版（`StarPie.exe`），内置 .NET 8 运行时，下载双击即可直接运行，无需安装任何额外依赖！

---

## ✨ 核心功能亮点 (Key Features)

- **⚡ 极速低级鼠标钩子与毫秒级触发**：
  - 基于 Windows Win32 `WH_MOUSE_LL` 低级钩子架构，输入延迟低于 2ms，轻量无感；
  - 自动心跳守护与自愈恢复机制，杜绝钩子被系统超时释放导致的失效。

![2026-08-23\_05-38-41.gif](attachments/1787481575418-2026-08-23_05-38-41.gif)

![2026-08-23\_06-52-54.gif](attachments/1787485997633-2026-08-23_06-52-54.gif)

- **🎨 4 大几何形态与 7 套精美主题**：
  - **经典紧凑扇区 (Original)**、**独立悬浮圆形 (Circle)**、**圆角胶囊 (Capsule)**、**蜂巢六边形 (HexagonHive)**；
  - **极简纯白**、**极夜曜黑**、**液态毛玻璃 (Glassmorphism)**、**暗夜紫罗兰**、**抹茶森林**、**冰川透蓝**、**萌宠猫爪**等多种主题，支持 16 进制自由配色与保存；
  - **实时画布60FPS预览，随心把控自定义，制作属于你自己的主题配色。**

![2026-08-23\_05-54-05.gif](attachments/1787482485505-2026-08-23_05-54-05.gif)

- **🌈 8 种高亮边缘发光光晕 (Highlight Glow)**：
  - 支持 **丁香晶紫**、**冰川湛蓝**、**翡翠荧绿**、**樱花粉晕**、**琥珀金光**、**珊瑚赤光**、**冰魄纯白** 及自定义调色盘与屏幕吸色；
  - 弥散半径（8~48px）与不透明度（0~100%）自由微调。

![2026-08-23\_06-07-59.gif](attachments/1787483334642-2026-08-23_06-07-59.gif)

- **🖼️ 中心核圆图案与本地图片贴图**：
  - 支持十字准心、Windows 徽标、靶心圆点、快捷主页、电源、罗盘、猫爪等内置矢量图案；
  - 支持一键导入本地任意图片（PNG/JPG/WebP/ICO/GIF）作为中心核圆图案，并支持独立显示开关。

![2026-08-23\_06-20-27.gif](attachments/1787484060868-2026-08-23_06-20-27.gif)

- **🎯 4 / 8 / 12 键多向方位自适应**：
  - **4 键十字方位**：最快盲操，误触率为 0；
  - **8 键全向方位**：标准全能配置（推荐）；
  - **12 键钟表方位**：适合高密度功能定制。

![2026-08-23\_06-22-39.gif](attachments/1787484180250-2026-08-23_06-22-39.gif)

- **💼 多程序专属方案与自动识别 (Per-App Profiles)**：
  - 针对 Chrome、VS Code、Photoshop、Word、CAD 等不同软件自动匹配专属轮盘映射；
  - 内置高清图标提取器，自动检索软件纯净图标。

![2026-08-23\_06-29-46.gif](attachments/1787484615956-2026-08-23_06-29-46.gif)

- **🛡️ 场景隔离与全屏游戏防误触**：
  - 智能检测全屏独占应用与 3D 游戏，自动放行原生右键；
  - 支持 Ctrl / Shift / Alt 修饰键穿透与进程黑名单排除。

![2026-08-23\_06-30-56.png](attachments/1787484659466-2026-08-23_06-30-56.png)

- **🌐 四国多语言即时热切换**：
  - 原生支持 **简体中文 (zh-CN)**、**繁體中文 (zh-TW)**、**English (en)**、**日本語 (ja)** 与 **跟随系统**。

![2026-08-23\_06-31-34.gif](attachments/1787484707963-2026-08-23_06-31-34.gif)

- **🚀 零依赖单文件发布**：
  - 自包含独立打包，即开即用，无需配置环境。

---

## 🚀 快速开始 / 下载 (Quick Start & Download)

### 最新正式版：`v1.3.8`

| 版本类型 | 适用人群 | 下载链接 |
| --- | --- | --- |
| 🟢 **独立免安装单文件版 (推荐)** | **所有用户**，即开即用，无需安装 .NET 运行库 | ⬇️ 下载 StarPie.exe (Standalone) |
| ⚡ **极简轻量便携版** | 已安装 .NET 8 Desktop Runtime 的用户（体积数 MB） | ⬇️ 下载 StarPie 极简便携包 |
| 🏷️ **历史版本归档** | 查看所有版本发行包与更新源码快照 | 📂 浏览 Releases 归档目录 |

### 基础使用方式：

1. 双击运行 `StarPie.exe`，应用自动在系统托盘驻留运行；
2. **按住鼠标右键**并向任意方向轻推，即可呼出手势轮盘；
3. 滑动至目标扇区后**松开鼠标右键**，即可瞬间执行预设动作；
4. 双击系统托盘图标或右键点击「偏好设置」，即可打开图形化控制台进行个性化配置。

---

## 🌐 多语言国际化 (Internationalization)

StarPie 控制台与轮盘界面支持多国语言，并在「⚙️ 高级与系统 (Advanced & System)」中提供即时热切换选项：

| 语言代码 | 显示名称 | 状态 |
| --- | --- | --- |
| `zh-CN` | 🇨🇳 简体中文 (Simplified Chinese) | 🟢 完整支持 |
| `zh-TW` | 🇭🇰/🇹🇼 繁體中文 (Traditional Chinese) | 🟢 完整支持 |
| `en` | 🇺🇸 English (US/UK) | 🟢 完整支持 |
| `ja` | 🇯🇵 日本語 (Japanese) | 🟢 完整支持 |
| `Auto` | 🖥️ 跟随操作系统语言 (System Default) | 🟢 完整支持 |

---

## 🛠️ 本地构建与开发 (Development & Build)

### 环境依赖

- Windows 10 / 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (v8.0.100 或更高版本)
- Python 3.10+ (仅运行自动化测试需要，`pytest`, `pywinauto`)

### 编译与运行

```bash
# 1. 克隆代码仓库
git clone https://github.com/<your-username>/StarPie.git
cd StarPie

# 2. 编译项目 (Debug / Release)
dotnet build WinPieGestures/WinPieGestures.csproj -c Release

# 3. 运行应用程序
dotnet run --project WinPieGestures/WinPieGestures.csproj
```

### 打包发布产物

```bash
# 生成独立免安装单文件版 (Zero-Dependency Self-Contained)
dotnet publish WinPieGestures/WinPieGestures.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o releases/v1.3.8/StarPie-v1.3.8-Standalone-win-x64

# 生成轻量框架依赖版 (Framework-Dependent)
dotnet publish WinPieGestures/WinPieGestures.csproj -c Release -r win-x64 --self-contained false -o releases/v1.3.8/StarPie-v1.3.8-Lightweight
```

### 运行自动化测试 (15/15 Passed 🟢)

```bash
# 安装测试依赖
pip install pytest pywinauto

# 执行全量 15 项端到端 GUI 自动化测试
python -m pytest tests/test_settings.py -v
```

---

## 📂 项目结构 (Project Structure)

```
StarPie/
├── .github/                   # GitHub Actions 持续集成与 Issue 模板
├── WinPieGestures/            # StarPie 核心源码工程 (WPF / C# .NET 8)
│   ├── ActionExecutor.cs      # 热键模拟、程序启动、系统命令执行引擎
│   ├── MouseHook.cs           # 低级鼠标全局钩子与健康巡检
│   ├── GestureController.cs   # 笔势轨迹跟踪、角度判定与状态机
│   ├── StyleRendererFactory.cs# 多形态渲染器工厂
│   ├── I18n.cs                # 集中式四国语言国际化字典
│   ├── ConfigManager.cs       # 配置存储、跨会话记忆与热重载
│   ├── SettingsWindow.xaml    # 5 维度现代图形化设置控制台
│   ├── RadialWindow.xaml      # 60FPS 实时轮盘透明交互窗口
│   └── app_icon.ico           # 高清 Squircle 嵌入式图标
├── releases/                  # 各版本发布包与归档镜像
│   └── v1.3.8/                # v1.3.8 独立单文件与轻量版二进制
├── tests/                     # 基于 pywinauto 的端到端自动化测试套件
│   ├── conftest.py            # 沙箱隔离与进程生命周期管理
│   └── test_settings.py       # 15 项集成与界面测试用例
├── CHANGELOG.md               # 完整版本演进与更新日志
├── LICENSE                    # MIT 开源许可证
└── README.md                  # 项目说明主文档
```

---

## 🤝 参与贡献 (Contributing)

欢迎任何形式的贡献！无论是提交 Bug 报告、提出新功能建议，还是提交 Pull Request。

1. Fork 本仓库；
2. 新建特性分支 (`git checkout -b feature/AmazingFeature`)；
3. 提交修改 (`git commit -m 'feat: Add some AmazingFeature'`)；
4. 推送至分支 (`git push origin feature/AmazingFeature`)；
5. 开启 Pull Request。

---

## 📄 开源许可证 (License)

本项目采用 [MIT License](LICENSE) 许可协议开源。

---
