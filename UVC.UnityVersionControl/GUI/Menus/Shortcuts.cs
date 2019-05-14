#if UNITY_2019_1_OR_NEWER
using UnityEngine;
using UnityEditor.ShortcutManagement;
using UVC.UserInterface;

namespace UVC
{
    public class Shortcuts
    {
        [Shortcut("UVC/" + Terminology.getlock, null, KeyCode.Q, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void GetLock()
        {
            VCCommands.Instance.GetLockTask(new[] {VCSceneViewGUI.currentContext()});
        }
        
        [Shortcut("UVC/" + Terminology.allowLocalEdit, null, KeyCode.W, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void AllowLocalEdit()
        {
            VCCommands.Instance.AllowLocalEditTask(new[] {VCSceneViewGUI.currentContext()});
        }
        
        [Shortcut("UVC/" + Terminology.revert, null, KeyCode.E, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void Revert()
        {
            VCCommands.Instance.RevertTask(new[] {VCSceneViewGUI.currentContext()});
        }
    }
}
#endif