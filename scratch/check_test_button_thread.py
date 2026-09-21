import re, sys, pathlib

# Windows 默认 GBK 控制台会把中文结果显示成乱码；统一为 UTF-8。
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

root = pathlib.Path(__file__).resolve().parent.parent
src = root / "WinPieGestures"

# 「测试」按钮必须在后台执行线程上跑动作，不能在 UI 线程上同步 Execute。
# 原因见 ActionExecutor.ExecuteForTesting 的注释：插件动作那条路上 PluginInvoker 用
# task.Wait(超时) 等结果，在 UI 线程上调它 = 让 UI 线程等一个「要等 UI 线程空出来才能完成」
# 的任务 —— 任何 await Dispatcher.InvokeAsync 的插件点「测试」必然耗满超时报「执行超时」。
#
# 这条约束编译器抓不到（Execute 与 ExecuteForTesting 都是合法调用），UI 回归套件也够不着
# （要复现得在沙箱里装一个真插件并点一次按钮）。所以只能静态守。
#
# 判据的形状：在「名字里带 Test 的事件处理器」这个作用域内，不许出现直接调用
# ActionExecutor.Execute(。之所以限定作用域而不是全仓禁：GestureController 等真实触发路径
# 本来就该按它们自己的方式调用 Execute，那是有意为之，不是缺陷。
HANDLER = re.compile(r"private\s+void\s+(?P<name>\w*Test\w*)_Click\s*\([^)]*\)\s*\{")
EXECUTE = re.compile(r"ActionExecutor\.Execute\s*\(")

failures = []
checked = 0

for path in sorted(src.glob("*.xaml.cs")):
    text = path.read_text(encoding="utf-8")
    for match in HANDLER.finditer(text):
        checked += 1
        # 逐花括号配平取出该处理器的函数体（够用了：这几个处理器里没有字符串里的花括号）
        depth, end = 1, match.end()
        while end < len(text) and depth:
            if text[end] == "{":
                depth += 1
            elif text[end] == "}":
                depth -= 1
            end += 1
        body = text[match.end():end]
        line_no = text[:match.start()].count("\n") + 1
        for call in EXECUTE.finditer(body):
            failures.append(
                f"{path.name}:{line_no}  {match.group('name')}_Click 里同步调用了 "
                "ActionExecutor.Execute(...) —— UI 线程会卡到插件动作超时，"
                "改走 ActionExecutor.ExecuteForTesting(...)")

print(f"扫描到「测试」类事件处理器 {checked} 个")
if failures:
    for f in failures:
        print("[FAIL] " + f)
    sys.exit(1)
print("PASS —— 全部「测试」按钮都走 ExecuteForTesting，没有在 UI 线程上同步执行动作")
