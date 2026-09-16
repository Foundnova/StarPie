using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「屏幕截屏文字识别」的插件模型实现。
/// <para>
/// <b>无参数</b>：识别区域由用户在按下扇区之后现场框选，没有任何需要事先保存的配置。
/// 这也是为什么它的手写面板里没有输入控件，只有一段说明与两枚按钮
/// （✂️ 立即测试截屏、⚙️ 接口配置）—— 那两枚按钮操作的是<b>全局</b> OCR 设置与一次性测试，
/// 不属于某个扇区的动作参数，因此不进 <see cref="Parameters"/>。
/// </para>
/// </summary>
internal sealed class BuiltinActionOcr : IActionContribution
{
	public ActionDescriptor Descriptor => new()
	{
		Id = "ocr",
		DisplayName = I18n.T("ActionTypeOcrShort"),
		Description = null,
		Category = "",
		IconKey = null,
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	/// <summary>无参数。按约定返回空列表而不是 null。</summary>
	public IReadOnlyList<ParameterField> Parameters => Array.Empty<ParameterField>();

	/// <summary>无参数可校验。</summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters) => null;

	/// <summary>无参数可预览。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters) => "";

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		OcrManager.StartCaptureAndRecognize();
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "Ocr",
		Aliases = new[] { "ScreenOcr" },
		FullId = BuiltinActionCatalog.ProviderId + ".ocr",
		Contribution = new BuiltinActionOcr(),

		// 无参数可投影，用目录的默认实现即可（见 BuiltinActionRegistration.ProjectParameters 的默认值）。
	};
}
