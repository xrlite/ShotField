using System;

namespace ShotField.Core
{
    // ---- Tripo v3 响应包装（JsonUtility 可序列化结构）----

    [Serializable]
    internal class TripoSubmitData
    {
        public string task_id;
    }

    [Serializable]
    internal class TripoSubmitResponse
    {
        public int code;
        public TripoSubmitData data;
        public string message;
    }

    [Serializable]
    public class TripoTaskOutput
    {
        public string model_url;
        public string rendered_image_url;
    }

    [Serializable]
    public class TripoTaskData
    {
        public string task_id;
        public string type;
        public string status;
        public int progress;
        public TripoTaskOutput output;
    }

    [Serializable]
    internal class TripoTaskResponse
    {
        public int code;
        public TripoTaskData data;
        public string message;
    }

    [Serializable]
    internal class TripoBalanceData
    {
        public double balance;
        public double frozen;
    }

    [Serializable]
    internal class TripoBalanceResponse
    {
        public int code;
        public TripoBalanceData data;
        public string message;
    }

    [Serializable]
    internal class TripoTextToModelPayload
    {
        public string prompt;
        public string model;
        public int face_limit;
    }
}
