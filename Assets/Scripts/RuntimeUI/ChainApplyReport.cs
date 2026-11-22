using System.Collections.Generic;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 一轮提案应用结果：计数 + 成功落位清单 + 失败/缺资产清单。
    /// 纯报告 DTO，不碰场景；与编排器拆开以守单文件行数预算。
    /// </summary>
    public class ChainApplyReport
    {
        public int acceptedCount;
        public int rejectedCount;
        public List<string> placedPropIds = new List<string>();
        public List<string> placedActorIds = new List<string>();
        public bool cameraApplied;
        public List<string> missingAssets = new List<string>();
        public List<string> placementFailures = new List<string>();
    }
}
