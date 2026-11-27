using System;
using System.Collections.Generic;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.Tests.PlayMode
{
    internal sealed class PlayModeTestHelpers
    {
        private readonly List<UnityEngine.Object> _tracked = new List<UnityEngine.Object>();

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) _tracked.Add(obj);
            return obj;
        }

        public Camera CreateMainCamera(string name = "TestMainCamera")
        {
            var go = Track(new GameObject(name));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 1.6f, -2f);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            return cam;
        }

        public XRUIInputModule CreateEventSystemWithXRUI()
        {
            var go = Track(new GameObject("TestEventSystem"));
            go.AddComponent<EventSystem>();
            var mod = go.AddComponent<XRUIInputModule>();
            mod.enableXRInput = true;
            return mod;
        }

        public Canvas CreateWorldCanvas(string name)
        {
            var go = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas)));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            return canvas;
        }

        public static Type FindTypeByName(string typeName)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<object>())
            {
                if (t != null && (t.Name == typeName || t.FullName == typeName))
                    return t;
            }
            return null;
        }

        public void CleanupAll()
        {
            ActorSelection.Clear();
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                if (_tracked[i] != null) UnityEngine.Object.DestroyImmediate(_tracked[i]);
            }
            _tracked.Clear();
        }
    }
}