#if UNITY_EDITOR
using System;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class VerifyAssert
    {
        public static void Require(string label, bool condition, ref int passed)
        {
            if (!condition)
            {
                Debug.LogError($"[FAIL] {label}");
                throw new InvalidOperationException($"Assertion failed: {label}");
            }
            passed++;
            Debug.Log($"[PASS] {label}");
        }

        public static void Finish(string tag, int passed, int expected)
        {
            string result = passed == expected ? "PASS" : "FAIL";
            Debug.Log($"{tag}={result} ({passed}/{expected})");
            if (Application.isBatchMode) EditorApplication.Exit(result == "PASS" ? 0 : 1);
        }
    }
}
#endif
