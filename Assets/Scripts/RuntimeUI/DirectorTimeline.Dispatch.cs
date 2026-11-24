using UnityEngine;
using ShotField.Core;
using ShotField.RuntimeUI.Tabletop;
using ShotField.RuntimeUI.Probes;

namespace ShotField.RuntimeUI
{
    public partial class DirectorTimeline
    {
        [Header("多轨扩展联动 (L3载具 / L5空间探针)")]
        public VehiclePathNavigator vehicleNavigator;
        public SpatialProbeVisualizer probeVisualizer;

        partial void AutoWireExtensions()
        {
            if (vehicleNavigator == null) vehicleNavigator = FindAnyObjectByType<VehiclePathNavigator>();
            if (probeVisualizer == null) probeVisualizer = FindAnyObjectByType<SpatialProbeVisualizer>();
        }

        public void ApplyCurrentFrame()
        {
            DispatchL1CameraTrack();
            DispatchL2ActorTrack();
            DispatchL3VehicleTrack();
            DispatchL4PlayablesTrack();
            DispatchL5ProbeTrack();
        }

        public void DispatchL1CameraTrack()
        {
            if (cameraRecorder == null || cameraRecorder.filmCamera == null) return;
            var traj = cameraRecorder.ActiveTrajectory;
            if (traj != null && traj.Count > 0)
            {
                float sampleTime = NormalizedTime * traj.totalDuration;
                var sample = traj.Evaluate(sampleTime);
                cameraRecorder.filmCamera.transform.SetPositionAndRotation(sample.position, sample.rotation);
                cameraRecorder.filmCamera.fieldOfView = sample.fieldOfView;
            }
        }

        public void DispatchL2ActorTrack()
        {
            if (actorPlayer != null && actorPlayer.actorTransform != null && actorPlayer.currentTake != null && actorPlayer.currentTake.Count > 0)
            {
                var take = actorPlayer.currentTake;
                var sample = take.Evaluate(NormalizedTime * take.duration);
                actorPlayer.actorTransform.SetPositionAndRotation(sample.position, sample.rotation);
            }
            if (actorA != null)
            {
                actorA.transform.position = Sc01PrevisShotData.EvaluateActorA(currentTime);
                actorA.transform.rotation = Sc01PrevisShotData.EvaluateActorARotation(currentTime);
            }
            if (actorB != null)
            {
                actorB.transform.position = Sc01PrevisShotData.EvaluateActorB(currentTime);
                actorB.transform.rotation = Sc01PrevisShotData.EvaluateActorBRotation(currentTime);
            }
        }

        public void DispatchL3VehicleTrack()
        {
            if (vehicleNavigator == null) vehicleNavigator = FindAnyObjectByType<VehiclePathNavigator>();
            if (vehicleNavigator != null)
            {
                vehicleNavigator.EvaluateTrajectory(NormalizedTime);
            }
        }

        public void DispatchL4PlayablesTrack()
        {
            if (videoPlayer != null && videoPlayer.HasSource) videoPlayer.SeekNormalized(NormalizedTime);

            if (playableDirector != null && playableDirector.playableAsset != null)
            {
                playableDirector.time = currentTime;
                playableDirector.Evaluate();
            }
        }

        public void DispatchL5ProbeTrack()
        {
            if (probeVisualizer == null) probeVisualizer = FindAnyObjectByType<SpatialProbeVisualizer>();
            if (probeVisualizer != null)
            {
                probeVisualizer.RefreshProbes();
            }
        }
    }
}
