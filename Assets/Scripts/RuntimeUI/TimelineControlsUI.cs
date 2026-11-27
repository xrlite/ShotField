using System;
using ShotField.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间时间轴与预设交互控制条：
    /// 串联 DirectorTimeline、5 种运镜预设与 5 种演员姿态预设。
    /// </summary>
    public class TimelineControlsUI : MonoBehaviour
    {
        public DirectorTimeline timeline;
        public CameraMotionRecorder cameraRecorder;
        public ActorTakePlayer actorPlayer;

        public Button playPauseBtn;
        public Button motionPresetBtn;
        public Button posePresetBtn;
        public Slider scrubber;
        public TMP_Text presetStatusText;

        public CameraMotionType currentMotion = CameraMotionType.DollyPush;
        public ActorPoseType currentPose = ActorPoseType.Idle;

        private bool wired;

        void Start() => Wire();

        public void Wire()
        {
            if (wired) return;
            if (timeline == null) timeline = FindAnyObjectByType<DirectorTimeline>();
            if (cameraRecorder == null) cameraRecorder = FindAnyObjectByType<CameraMotionRecorder>();
            if (actorPlayer == null) actorPlayer = FindAnyObjectByType<ActorTakePlayer>();

            if (playPauseBtn != null && timeline != null)
                playPauseBtn.onClick.AddListener(timeline.TogglePlay);
            if (motionPresetBtn != null)
                motionPresetBtn.onClick.AddListener(CycleMotionPreset);
            if (posePresetBtn != null)
                posePresetBtn.onClick.AddListener(CyclePosePreset);
            if (scrubber != null && timeline != null)
            {
                scrubber.onValueChanged.AddListener(v => timeline.SeekNormalized(v));
                timeline.OnTimeUpdated += t => scrubber.SetValueWithoutNotify(t);
            }
            wired = true;
            RefreshStatus();
        }

        public void ApplyMotionPreset(CameraMotionType type)
        {
            currentMotion = type;
            if (cameraRecorder != null)
            {
                Vector3 target = ResolveTargetPosition();
                float dur = timeline != null ? timeline.duration : CameraMotionPresets.DefaultDuration;
                var traj = CameraMotionPresets.CreatePreset(type, target, dur);
                cameraRecorder.rawTrajectory = traj;
                cameraRecorder.smoothedTrajectory = traj;
            }
            if (timeline != null) timeline.ApplyCurrentFrame();
            RefreshStatus();
        }

        public void ApplyPosePreset(ActorPoseType type)
        {
            currentPose = type;
            if (actorPlayer != null)
            {
                Vector3 basePos = actorPlayer.actorTransform != null
                    ? actorPlayer.actorTransform.position
                    : new Vector3(0.8f, 0f, 1.25f);
                Vector3 target = ResolveTargetPosition();
                float dur = timeline != null ? timeline.duration : ActorPosePresets.DefaultDuration;
                actorPlayer.currentTake = ActorPosePresets.CreatePreset(type, basePos, target, dur);
            }
            if (timeline != null) timeline.ApplyCurrentFrame();
            RefreshStatus();
        }

        public void CycleMotionPreset()
        {
            int count = Enum.GetValues(typeof(CameraMotionType)).Length;
            ApplyMotionPreset((CameraMotionType)(((int)currentMotion + 1) % count));
        }

        public void CyclePosePreset()
        {
            int count = Enum.GetValues(typeof(ActorPoseType)).Length;
            ApplyPosePreset((ActorPoseType)(((int)currentPose + 1) % count));
        }

        private Vector3 ResolveTargetPosition()
        {
            foreach (var m in SemanticMarker.All())
            {
                if (m != null && m.kind == SemanticMarker.Kind.KeyProp)
                    return m.transform.position;
            }
            if (actorPlayer != null && actorPlayer.actorTransform != null)
                return actorPlayer.actorTransform.position;
            return new Vector3(1.5f, 0.46f, 2.15f);
        }

        private void RefreshStatus()
        {
            if (presetStatusText != null)
                presetStatusText.text = $"运镜:{currentMotion} | 姿态:{currentPose}";
        }
    }
}
