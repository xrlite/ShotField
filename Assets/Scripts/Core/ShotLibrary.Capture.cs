using UnityEngine;

namespace ShotField.Core
{
    public partial class ShotLibrary
    {
        /// <summary>把当前场景状态捕获成一份快照。</summary>
        public SceneSnapshot CaptureSnapshot()
        {
            var snap = new SceneSnapshot();
            var markers = SemanticMarker.All(FindObjectsInactive.Include);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m.gameObject.scene != gameObject.scene) continue;
                snap.actors.Add(new ActorState
                {
                    actorId = m.markerId,
                    position = m.transform.position,
                    rotation = m.transform.rotation,
                    scale = m.transform.localScale,
                    active = m.gameObject.activeSelf
                });
            }
            return snap;
        }

        /// <summary>把当前相机状态捕获成机位。</summary>
        public ShotCameraState CaptureCamera()
        {
            var cam = ShotCamera != null ? ShotCamera : Camera.main;
            return ShotCameraState.FromCamera(cam);
        }

        /// <summary>把一份快照刷到场景上（这是唯一允许的"应用"入口）。</summary>
        public void ApplySnapshot(SceneSnapshot snap)
        {
            if (snap == null) return;
            var markers = SemanticMarker.All(FindObjectsInactive.Include);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m.gameObject.scene != gameObject.scene) continue;
                var st = snap.Find(m.markerId);
                if (st == null) continue;
                m.transform.position = st.position;
                m.transform.rotation = st.rotation;
                m.transform.localScale = st.scale;
                m.gameObject.SetActive(st.active);
            }
            Physics.SyncTransforms();
        }

        public void ApplyShot(ShotData shot)
        {
            if (shot == null) return;
            ApplySnapshot(shot.snapshot);
            var cam = ShotCamera != null ? ShotCamera : Camera.main;
            shot.camera.ApplyTo(cam);
        }
    }
}
