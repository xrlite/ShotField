using System.Collections.Generic;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Gate 2 dual-state controller: macro tabletop proxy (offline-batched low-poly) versus
    /// micro 1:1 block. Orbs choose the map preset; the arch/fade transition plays on switch.
    /// No runtime light baking is ever requested here.
    /// </summary>
    public sealed class SceneOrbDirector : MonoBehaviour
    {
        public TabletopDirector tabletop;
        public StudioFadeTransition transition;
        public TabletopMapManipulator manipulator;
        public Transform livingPreset;
        public Transform streetPreset;

        private readonly List<SceneOrb> _orbs = new List<SceneOrb>();
        public string ActiveOrb { get; private set; } = "living";

        public void Register(SceneOrb orb)
        {
            if (orb == null || _orbs.Contains(orb)) return;
            orb.director = this;
            _orbs.Add(orb);
        }

        public void Activate(SceneOrb orb)
        {
            if (orb == null || tabletop == null) return;
            ActiveOrb = orb.orbId;
            bool street = orb.orbId == "street";
            if (livingPreset != null) livingPreset.gameObject.SetActive(!street);
            if (streetPreset != null) streetPreset.gameObject.SetActive(street);
            if (manipulator != null) manipulator.Bind(tabletop.tabletopProxyRoot);
            if (transition != null) transition.TransitionTo(TabletopDirector.ScaleMode.TabletopMini);
            else tabletop.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
            Debug.Log("[SceneOrb] Activate " + orb.orbId + " preset=" + (street ? "street" : "living"));
        }

        public void EnterImmersive()
        {
            if (tabletop == null) return;
            if (transition != null) transition.TransitionTo(TabletopDirector.ScaleMode.Immersive1to1);
            else tabletop.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);
            Debug.Log("[SceneOrb] EnterImmersive orb=" + ActiveOrb);
        }
    }
}
