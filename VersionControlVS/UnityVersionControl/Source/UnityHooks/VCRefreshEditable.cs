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
            VCCommands.Instance.StatusCompleted += RefreshGUI;
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
                    VCUtility.SetEditable(selectedObject, VCUtility.HaveAssetControl(selectedObject.GetAssetStatus()));
                }
                else if (selectedObject is GameObject)
                {
                    VCUtility.RefreshEditableObject(selectedObject as GameObject);
                }
            }
        }
    }
}
