using ShotField.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShotField.RuntimeUI
{
    internal static class WorkbenchStatus
    {
        private static readonly Color PassGreen = new Color(0.32f, 0.77f, 0.10f, 0.95f);
        private static readonly Color FailRed = new Color(1.00f, 0.30f, 0.31f, 0.95f);
        private static readonly Color PendingAmber = new Color(1.00f, 0.75f, 0.10f, 0.95f);
        private static readonly Color NeutralMuted = new Color(0.20f, 0.22f, 0.25f, 0.85f);

        internal static void UpdatePill(Image pill, TMP_Text text, CheckStatus? status, string label)
        {
            string detail = status switch
            {
                CheckStatus.Pass => "通过",
                CheckStatus.Violation => "违规",
                CheckStatus.Pending => "待确认",
                _ => "未配置"
            };
            if (text != null) text.text = label + " [" + detail + "]";
            if (pill != null)
                pill.color = status switch
                {
                    CheckStatus.Pass => PassGreen,
                    CheckStatus.Violation => FailRed,
                    CheckStatus.Pending => PendingAmber,
                    _ => NeutralMuted
                };
        }

    }
}
