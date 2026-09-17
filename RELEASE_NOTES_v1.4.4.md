# StarPie v1.4.4 发布说明 (Release Notes)

🎉 **StarPie v1.4.4** 正式发布！本次更新重点解决了外部应用程序（如 OBS Studio）启动时的工作目录丢失报错问题，并在「触发与场景」中为广大用户带来了期望已久的**轮盘高亮过渡响应动效三挡自由调速**体系。

---

## 🌟 核心更新与修复亮点

### 1. 🛠️ 外部应用工作目录（WorkingDirectory）自适应补全
- **问题背景**：直接双击可以打开 OBS Studio，但将 OBS 添加到 StarPie 轮盘动作中启动时会报 `Failed to find locale/en-US.ini` 和 `Failed to load locale` 错误。
- **深层原因**：Windows 启动外部子进程时，如未显式配置 `WorkingDirectory`，默认会继承主程序（StarPie）所在路径。OBS 会根据当前工作目录查找语言包和插件，从而造成缺失报错。
- **完美修复**：在执行启动时，自动推断目标应用程序的物理父目录并绑定为子进程专属 `WorkingDirectory`，保证所有外部应用与游戏拥有独立完整的资源运行环境。

### 2. ⚡ 功能区高亮与平滑过渡动效三挡调速
在「🎯 触发与场景」中新增专属调速面板，支持三种不同手感档位自由选择：
- 🌸 **优雅 (Elegant / 130ms)**：缓动柔和温润、富有层次感，适合喜欢高级视觉动效的用户；
- ⚡ **流畅 (Fluent / 80ms)** 【推荐 / 默认】：阻尼适中、跟手动感，兼顾灵敏反馈与丝滑视觉；
- 🚀 **快速 (Snappy / 35ms)**：极速瞬时响应，切换几乎无延迟，适合电竞与超快手速盲操。

---

## 📦 发布包下载与使用
- 📁 **独立单文件版 (免安装运行时)**：`StarPie-v1.4.4-Standalone-win-x64.zip`
- 📁 **极简轻量版 (依赖 .NET 8 Desktop Runtime)**：`StarPie-v1.4.4-Lightweight.zip`
