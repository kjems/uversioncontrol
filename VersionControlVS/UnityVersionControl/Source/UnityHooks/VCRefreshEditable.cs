// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEditor;
using UnityEngine;

namespace VersionControl
{
    [InitializeOnLoad]
    internal sealed class VCRefreshEditable
    {
        private static Object selectedObject;

        static VCRefreshEditable()
        {
            EditorApplication.update += RefreshEditable;
            VCCommands.Instance.StatusUpdated += RefreshGUI;
            VCSettings.SettingChanged += () => { RefreshGUI(); D.Log("Settings changed"); };
        }

        static void RefreshGUI()
        {
            selectedObject = null;
            RefreshEditable();
        }

        private static void RefreshEditable()
        {
            if (selectedObject != Selection.activeObject)
            {
                selectedObject = Selection.activeObject;
                if (selectedObject is Material)
                {
                    EditableManager.RefreshEditableMaterial(selectedObject as Material);
                }
                else if (selectedObject is GameObject)
                {
                    EditableManager.RefreshEditableObject(selectedObject as GameObject);
                }
            }
        }
    }
}
