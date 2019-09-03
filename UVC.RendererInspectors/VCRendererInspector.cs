// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UVC.UserInterface
{
    using Extensions;

    [InitializeOnLoad]
    internal class VCRendererInspector
    {
        static VCRendererInspector()
        {
            RendererInspectorManager.AddInspector(SubscribeToInspector, -1);
        }

        private static void SubscribeToInspector(Object[] targets)
        {
            if (!VCCommands.Active || !VCSettings.MaterialGUI || targets.Length == 0) return;

            var renderer = targets[0] as Renderer;
            var sharedMaterials = renderer.sharedMaterials;

            for (int i = 0; i < renderer.sharedMaterials.Length; ++i)
            {
                if (renderer.sharedMaterials[i] == null) continue;
                var material = sharedMaterials[i];
                string assetPath = material.GetAssetPath();
                GUIStyle buttonStyle = EditorStyles.toolbarButton;
                bool builtinMaterial = EditableManager.IsBuiltinAsset(assetPath);
                var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);

                EditorGUILayout.BeginVertical(VCGUIControls.GetVCBox(assetStatus));
                string lockDescription = builtinMaterial ? "" : AssetStatusUtils.GetLockStatusMessage(assetStatus);
                string materialDescription = "[" + (builtinMaterial ? "Unity Default" : material.name) + "] " + lockDescription;
                GUILayout.Label(new GUIContent(materialDescription, assetPath), VCGUIControls.GetLockStatusStyle());

                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                if (GUILayout.Button(new GUIContent("Save as", "This will open a save dialog"), buttonStyle))
                {
                    int index = i;
                    string savePath;
                    string fileName = "";
                    if (assetPath == "")
                    {
                        savePath = "Assets/Graphics/SavedMaterials/";
                    }
                    else
                    {
                        savePath = Path.GetDirectoryName(assetPath).Replace("\\","/");
                        fileName = Path.GetFileNameWithoutExtension(assetPath);
                    }

                    OnNextUpdate.Do(() =>
                    {
                        string newMaterialName = EditorUtility.SaveFilePanel("Save Material as...", savePath, fileName, "mat");
                        newMaterialName = newMaterialName.Substring(newMaterialName.IndexOf("/Assets/", System.StringComparison.Ordinal) + 1);
                        if (newMaterialName != "")
                        {
                            sharedMaterials[index] = SaveMaterial(material, newMaterialName);
                            renderer.sharedMaterials = sharedMaterials;
                        }
                    });
                }

                var validActions = VCGUIControls.GetValidActions(assetPath, material);

                VCGUIControls.VersionControlStatusGUI(
                    style:                      buttonStyle, 
                    assetStatus:                assetStatus, obj: material, 
                    showAddCommit:              !builtinMaterial && (validActions & (ValidActions.Add | ValidActions.Commit)) != 0, 
                    showLockAndAllowLocalEdit:  !builtinMaterial && (validActions & (ValidActions.OpenLocal | ValidActions.Open)) != 0, 
                    showRevert:                 !builtinMaterial && (validActions & ValidActions.Revert) != 0
                );

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private static Material SaveMaterial(Material material, string materialPath)
        {
            material = new Material(material) { name = Path.GetFileNameWithoutExtension(materialPath) };
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return material;
        }

    }
}


