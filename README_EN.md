<div align="center">

<img src="assets/logo.png" width="128" height="128" alt="StarPie Logo" />

# StarPie

### Ultra-Lightweight, Blazing-Fast & Aesthetic Radial Pie Menu for Windows 10 / 11
**现代化 Windows 鼠标轮盘手势与极速生产力增强系统**

[![Release Version](https://img.shields.io/badge/Release-v1.4.1-2563EB.svg?style=flat-square&logo=github)](https://github.com/SoftBlack42/StarPie/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4.svg?style=flat-square&logo=windows)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-10B981.svg?style=flat-square)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-16%2F16%20Passed-success.svg?style=flat-square&logo=pytest)](tests/)
[![Language](https://img.shields.io/badge/Language-zh--CN%20%7C%20zh--TW%20%7C%20en%20%7C%20ja-8B5CF6.svg?style=flat-square)](#i18n)
[![Co-Authored](https://img.shields.io/badge/Co--Authored%20with-AI%20Agent-6366F1.svg?style=flat-square&logo=openai)](#acknowledgements)

<br/>

**[English](README_EN.md)** • **[简体中文](README.md)**

<br/>

[🚀 Quick Start](#download) • [✨ Key Features](#features) • [🎨 Visual Customization](#visuals) • [🌐 i18n](#i18n) • [🛠️ Build & Dev](#build) • [💡 Acknowledgements](#acknowledgements) • [📋 Changelog](CHANGELOG.md)

</div>

---

## <a id="intro"></a>📖 Introduction

**StarPie** is an **ultra-lightweight, zero-bloat, sub-2ms responsive, and beautifully designed** modern radial pie menu and mouse gesture productivity tool engineered specifically for Windows 10 & 11.

Hold the **Right Mouse Button and flick in any direction** within any desktop window or application to summon a silky 60FPS fluid radial menu instantly beneath your cursor. Featuring 4/8/12-sector dynamic layouts, per-application profile switching, hotkey recorder, quick app launcher, and full-spectrum customizable glow effects, StarPie turns tedious repetitive workflows into effortless muscle memory.

> 🪶 **Ultra-Lightweight & Zero Bloat**:
> - **🍃 3 ～ 5 MB Ultra-Low Resident Memory**: Built purely with native C# WPF and low-level Win32 APIs (no bulky Electron/Chromium runtime). Includes an intelligent working set trimmer that runs silently in the background with near-zero resource consumption;
> - **⚡ < 2ms Instantaneous Response**: Powered by low-level `WH_MOUSE_LL` event processing, eliminating input lag entirely;
> - **🟢 Zero-Dependency Standalone Single File**: Available as a self-contained portable executable (`StarPie.exe`) with bundled .NET 8 runtime—double-click to launch with no runtime installations required!

---

## <a id="features"></a>✨ Key Features

### 1. 🪶 Ultra-Lightweight Architecture & 3～5MB Memory Guard
- **Pure Native Performance**: Engineered entirely with C# and Win32 APIs, saving hundreds of megabytes compared to Electron-based gesture utilities;
- **Automatic Working Set Trimming**: Automatically trims and reclaims memory when the radial menu is dismissed, idling at just **3 ～ 5 MB** of RAM—flawless even on ultra-low-spec ultrabooks;
- **100% Portable & Non-Intrusive**: All user preferences are stored cleanly in local `config.json`. No background services, no telemetry, and no registry clutter.

---

### 2. ⚡ Sub-2ms Low-Level Mouse Hook & Heartbeat Self-Healing
- Implements a high-precision `WH_MOUSE_LL` low-level mouse hook with latency below **2ms**;
- Built-in background heartbeat monitoring and auto-recovery mechanism prevents hook timeouts and unexpected unhooking by Windows;
- Intelligently distinguishes standard right-clicks from gesture flicks—regular right-click context menus popup seamlessly as expected.

<div align="center">
  <img src="attachments/1787481575418-2026-08-23_05-38-41.gif" width="680" alt="Sub-2ms Instant Summon Demo" />
  <br/>
  <img src="attachments/1787485997633-2026-08-23_06-52-54.gif" width="680" alt="Smooth Radial Gesture Feedback" />
</div>

---

### <a id="visuals"></a>3. 🎨 4 Geometric Shapes & 7 Preset Themes
- **4 Geometric Shapes**:
  - **Original (Classic Wedge)**: Compact sectors with rock-solid spatial feel;
  - **Circle (Floating Discs)**: Elevated circular buttons for enhanced visual clarity;
  - **Capsule (Rounded Pills)**: Modern pill-shaped chamfers with sleek minimalist aesthetics;
  - **HexagonHive (Sci-Fi Honeycomb)**: Cyberpunk-inspired rigid hexagonal layout.
- **Rich Themes & 60FPS Live Preview Canvas**:
  - Includes **Minimalist White**, **Midnight Obsidian**, **Glassmorphism**, **Twilight Violet**, **Matcha Green**, **Glacier Cyan**, and **Kawaii Cat Paw**;
  - Features an interactive **60FPS live preview canvas** supporting full HEX color picker, screen eye-dropper pipette, and custom preset palette management.

<div align="center">
  <img src="attachments/1787482485505-2026-08-23_05-54-05.gif" width="680" alt="Themes & Live Canvas Demo" />
</div>

---

### 4. 🌈 8 Radiant Highlight Glow Effects
- **Full-Spectrum Edge Glow**:
  - Includes **Lilac Purple**, **Glacier Blue**, **Emerald Green**, **Cherry Blossom Pink**, **Amber Gold**, **Coral Red**, and **Frost White** presets;
  - Supports arbitrary custom RGB color tinting and screen color sampling;
  - Adjustable glow blur radius (8 ～ 48px) and opacity (0% ～ 100%).

<div align="center">
  <img src="attachments/1787483334642-2026-08-23_06-07-59.gif" width="680" alt="Highlight Glow Customization Demo" />
</div>

---

### 5. 🖼️ Center Core Icons & Custom Image Textures
- **Built-in Vector Patterns**: Crosshair, Windows Logo, Bullseye, Home, Power, Compass, Cat Paw, and more;
- **Custom Local Image Support**: Import any local image (`PNG`, `JPG`, `WebP`, `ICO`, `GIF`) as your custom center badge with automatic scaling and toggleable visibility.

<div align="center">
  <img src="attachments/1787484060868-2026-08-23_06-20-27.gif" width="680" alt="Core Pattern and Custom Image Demo" />
</div>

---

### 6. 🎯 Dynamic 4 / 8 / 12 Sector Adaptation
- **Cross 4 (4-Way)**: Wide 90-degree quadrant angles for 0% misclick rate and ultra-fast blind navigation;
- **Standard 8 (8-Way)**: The golden standard for balanced gesture accessibility (Default & Recommended);
- **Clock 12 (12-Way)**: High-density clock-face mapping for heavy power users.

<div align="center">
  <img src="attachments/1787484180250-2026-08-23_06-22-39.gif" width="680" alt="4/8/12 Sector Adaptation Demo" />
</div>

---

### 7. 💼 Per-App Profiles & Smart Application Scanner
- **Automatic Per-App Profile Matching**:
  - Automatically activates dedicated wheel shortcuts when switching between Chrome, VS Code, Photoshop, Word, CAD, etc.;
- **Intelligent Program Scanner**:
  - Deeply scans Start Menu hierarchies, Desktop, `AppData\Local\Programs`, `WindowsApps`, and 64/32-bit registry paths;
  - Aggressively purges uninstalled ghost links, setup wizards, and internal CLI helper scripts;
  - Extracts and caches clean high-resolution icons with real-time fuzzy search.

<div align="center">
  <img src="attachments/1787484615956-2026-08-23_06-29-46.gif" width="680" alt="Per-App Profiles & Program Picker Demo" />
</div>

---

### 8. 🛡️ Gaming Isolation & Modifier Key Passthrough
- **Full-Screen & 3D Game Detection**:
  - Automatically identifies exclusive full-screen games and bypasses gestures to preserve native mouse handling;
- **Modifier Key Passthrough & Blacklist**:
  - Passthrough gestures while holding `Ctrl`, `Shift`, or `Alt`;
  - One-click process blacklisting for specialized applications.

<div align="center">
  <img src="attachments/1787484659466-2026-08-23_06-30-56.png" width="680" alt="Gaming Safety & Blacklist Settings" />
</div>

---

### 9. 🌐 Multi-Language Hot-Switching (i18n)
- Natively supports **Simplified Chinese (zh-CN)**, **Traditional Chinese (zh-TW)**, **English (en)**, **Japanese (ja)**, and **System Default (Auto)**;
- Instant hot-reloading across all UI panels and context menus without restarting.

<div align="center">
  <img src="attachments/1787484707963-2026-08-23_06-31-34.gif" width="680" alt="Multi-Language Hot Switching Demo" />
</div>

---

## <a id="download"></a>🚀 Quick Start & Download

### Latest Official Release: `v1.4.1`

| Package Type | Target Audience | Description | Download Link |
| :--- | :--- | :--- | :--- |
| 🟢 **Standalone Single File (Recommended)** | **All Users** | Self-contained single `.exe` with embedded .NET 8 runtime (zero dependencies) | [⬇️ Download StarPie.exe (Standalone)](https://github.com/SoftBlack42/StarPie/releases) |
| ⚡ **Lightweight Portable Package** | Users with .NET 8 Desktop Runtime installed | Ultra-small package size (several megabytes) | [⬇️ Download StarPie Lightweight](https://github.com/SoftBlack42/StarPie/releases) |
| 🏷️ **Releases Archive** | Developers & Version History | Browse all historical release binaries and source code | [📂 Browse Releases](https://github.com/SoftBlack42/StarPie/releases) |

### Basic Usage:
1. Double-click `StarPie.exe` to run (StarPie resides unobtrusively in your system tray);
2. **Hold Right Mouse Button** and slide toward any direction to summon the radial menu;
3. Hover over the desired sector and **release the Right Mouse Button** to trigger the assigned action instantly;
4. Double-click the system tray icon or right-click and choose "Preferences" to open the control panel.

---

## <a id="i18n"></a>🌐 Internationalization (i18n)

StarPie control panel and radial UI support multiple languages with real-time switching in **"⚙️ Advanced & System"**:

| Language Code | Display Name | Status |
| :--- | :--- | :---: |
| `en` | 🇺🇸 English (US/UK) | 🟢 Fully Supported |
| `zh-CN` | 🇨🇳 简体中文 (Simplified Chinese) | 🟢 Fully Supported |
| `zh-TW` | 🇭🇰/🇹🇼 繁體中文 (Traditional Chinese) | 🟢 Fully Supported |
| `ja` | 🇯🇵 日本語 (Japanese) | 🟢 Fully Supported |
| `Auto` | 🖥️ Follow Operating System Language | 🟢 Fully Supported |

---

## <a id="build"></a>🛠️ Development & Build

### Prerequisites
- Windows 10 / 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (v8.0.100 or higher)
- Python 3.10+ (Required only for automated GUI test suite: `pytest`, `pywinauto`)

### Build & Run
```bash
# 1. Clone the repository
git clone https://github.com/SoftBlack42/StarPie.git
cd StarPie

# 2. Build the project (Release)
dotnet build WinPieGestures/WinPieGestures.csproj -c Release

# 3. Run the application
dotnet run --project WinPieGestures/WinPieGestures.csproj
```

### Publish Release Packages
```bash
# Generate Zero-Dependency Standalone Executable
dotnet publish WinPieGestures/WinPieGestures.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o releases/v1.4.1/StarPie-v1.4.1-Standalone-win-x64

# Generate Framework-Dependent Lightweight Package
dotnet publish WinPieGestures/WinPieGestures.csproj -c Release -r win-x64 --self-contained false -o releases/v1.4.1/StarPie-v1.4.1-Lightweight
```

### Run Automated Tests (16/16 Passed 🟢)
```bash
# Install testing dependencies
pip install pytest pywinauto

# Execute all 15 end-to-end GUI automation test suites
python -m pytest tests/test_settings.py -v
```

---

## <a id="structure"></a>📂 Project Structure

```
StarPie/
├── .github/                   # GitHub Actions CI pipelines and issue templates
├── WinPieGestures/            # StarPie Core WPF / C# .NET 8 Project
│   ├── ActionExecutor.cs      # Hotkey simulation, process launching, and system actions
│   ├── MouseHook.cs           # Low-level mouse hook with heartbeat health inspection
│   ├── GestureController.cs   # Trajectory tracking, angle detection, and state machine
│   ├── StyleRendererFactory.cs# Multi-geometry renderer factory
│   ├── I18n.cs                # Centralized 4-language internationalization dictionary
│   ├── ConfigManager.cs       # JSON configuration manager and cross-session persistence
│   ├── MemoryOptimizer.cs     # Working set memory trimming engine
│   ├── ProgramPickerWindow.xaml# Smart application picker (dead-link purging & high recall)
│   ├── SettingsWindow.xaml    # 5-dimension modern graphical control panel
│   ├── RadialWindow.xaml      # 60FPS transparent interactive radial window
│   └── app_icon.ico           # Anti-aliased high-DPI squircle app icon
├── releases/                  # Version release binaries and source mirrors
│   └── v1.4.1/                # v1.4.1 Standalone and Lightweight builds
├── attachments/               # Official GIF demonstrations and screenshot assets
├── tests/                     # pywinauto-based end-to-end automated GUI test suite
│   ├── conftest.py            # Sandbox isolation and process lifecycle fixtures
│   └── test_settings.py       # 16 integration and UI test cases
├── CHANGELOG.md               # Version evolution and detailed changelogs
├── CONTRIBUTING.md            # Community contribution guidelines
├── SECURITY.md                # Security policy and vulnerability reporting
├── LICENSE                    # MIT License
├── README.md                  # Primary Chinese documentation
└── README_EN.md               # English documentation
```

---

## <a id="acknowledgements"></a>💡 Developer Story, Acknowledgements & Maintenance

### 🌟 Inspiration & Developer Story
I am an undergraduate student majoring in Mechanical Design, Manufacturing & Automation. In my daily studies and 3D modeling work, I frequently use SolidWorks and find its built-in mouse gesture wheel exceptionally intuitive and handy.

After exploring the capabilities of modern AI Agents, I thought it would be great to bring this gesture wheel workflow to the global Windows desktop to enhance everyday efficiency and comfort. For those who haven't used radial mouse gestures before, it might be a novel experience—and being able to build and share this experience with others genuinely brings me joy.

While similar pie menu tools exist on GitHub, StarPie focuses on slightly different workflow details and nuances. Between university coursework and competitions, development was paused occasionally; over roughly one week of part-time human-AI collaboration, I managed to bring StarPie to its current state.

Although the feature set might still feel simple and some rough edges remain, this project truly represents my honest dedication. Every implemented feature, resolved bug, performance optimization, and UI improvement has brought me simple, authentic joy.

Please bear with any imperfections. If you encounter issues or have ideas for improvements, feel free to submit a bug report, open an issue, or create a Pull Request! As long as time and energy permit alongside my studies and career path, I will gladly do my best to keep refining it.

### 🤖 Human-AI Co-Creation (Co-Authored with AI Agent)
StarPie was architected, designed, and tuned by the human developer in close collaboration with the advanced AI coding agent (**AI Agent - Antigravity**):
- **Engineering Paradigm**: Built rigorously following **Specification-Driven Development (SDD)** and **Test-Driven Development (TDD)**, backed by a 16-suite end-to-end GUI automation test harness;
- **Division of Responsibility**: The human developer steered Windows interaction intuition, Win32 safety boundaries, and multilingual terminology, while the AI Agent accelerated modular code construction, iterative refactoring, and boundary-case regressions.

### 📌 Maintenance Notice
- **Current Status**: StarPie v1.4.1 is fully implemented, verified, and **Feature-Complete & Production-Ready** for daily driver workflows;
- **Future Roadmap**: As the developer is currently prioritizing academic studies and career transitions, subsequent updates will adopt a periodic maintenance cadence. Pull requests and community issues are always warmly welcomed!

---

## <a id="contributing"></a>🤝 Contributing

Contributions of all kinds are welcome! Whether submitting bug reports, suggesting new features, or opening Pull Requests, please check [CONTRIBUTING.md](CONTRIBUTING.md).

1. Fork this repository;
2. Create your feature branch (`git checkout -b feature/AmazingFeature`);
3. Commit your changes (`git commit -m 'feat: Add some AmazingFeature'`);
4. Push to the branch (`git push origin feature/AmazingFeature`);
5. Open a Pull Request.

---

## <a id="license"></a>📄 License

This project is licensed under the [MIT License](LICENSE).
