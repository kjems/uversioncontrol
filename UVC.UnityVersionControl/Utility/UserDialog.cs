using System;
using UnityEngine;
using UnityEditor;

namespace UVC
{
    public static class UserDialog
    {
        public static Func<string, string, string, bool> displayDialogSimpleOverride = null;
        public static bool DisplayDialog(string title, string message, string ok)
        {
            return displayDialogSimpleOverride?.Invoke(title, message, ok) ?? EditorUtility.DisplayDialog(title, message, ok);
        }
        
        public static Func<string, string, string, string, bool> displayDialogOverride = null;
        public static bool DisplayDialog(string title, string message, string ok, string cancel)
        {
            return displayDialogOverride?.Invoke(title, message, ok, cancel) ?? EditorUtility.DisplayDialog(title, message, ok, cancel);
        }

        public static Func<string, string, string, string, string, int> displayDialogComplexOverride = null;
        public static int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt)
        {
            return displayDialogComplexOverride?.Invoke(title, message, ok, cancel, alt) ?? EditorUtility.DisplayDialogComplex(title, message, ok, cancel, alt);
        }
        
    }
}