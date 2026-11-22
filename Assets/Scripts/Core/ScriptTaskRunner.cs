using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShotField.Core
{
    /// <summary>
    /// 同步等待异步解析任务的唯一入口。阻塞点若直接 <c>.GetAwaiter().GetResult()</c>，
    /// 底层异常会被包成 <see cref="AggregateException"/>；这里统一解包，
    /// 命中 <see cref="ScriptParseException"/> 时原样抛出让上游按 ErrorCode 提示，不吞错、不降级假成功。
    /// </summary>
    public static class ScriptTaskRunner
    {
        public static T RunSync<T>(Task<T> task)
        {
            try
            {
                return task.GetAwaiter().GetResult();
            }
            catch (AggregateException aggregate)
            {
                var inner = aggregate.Flatten().InnerExceptions
                    .OfType<ScriptParseException>()
                    .FirstOrDefault();
                if (inner != null) throw inner;
                throw;
            }
        }
    }
}
