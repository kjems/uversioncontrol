// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VersionControl.UserInterface
{
    [InitializeOnLoad]
    internal class VCRendererInspector
    {
        static VCRendererInspector()
        {
            RendererInspectorManager.AddInspector(SubscribeToInspector, -1);
        }

        private static void SubscribeToInspector(Object[] targets)
        {
            if (!VCSettings.MaterialGUI && targets.Length == 0) return;

            var renderer = targets[0] as Renderer;
            var sharedMaterials = renderer.sharedMaterials;

            for (int i = 0; i < renderer.sharedMaterials.Length; ++i)
            {
                if (renderer.sharedMaterials[i] == null) continue;
                var material = sharedMaterials[i];
                string assetPath = material.GetAssetPath();
                GUIStyle buttonStyle = EditorStyles.toolbarButton;
                bool builtinMaterial = assetPath == "";
                var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);

                EditorGUILayout.BeginVertical(VCGUIControls.GetVCBox(assetStatus));
                string lockDescription = builtinMaterial ? "" : VCGUIControls.GetLockStatusMessage(assetStatus);
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
                        savePath = Path.GetDirectoryName(assetPath);
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
                            VCCommands.Instance.RequestStatus(new[] { newMaterialName }, StatusLevel.Previous);
                        }
                    });
                }

                VCGUIControls.VersionControlStatusGUI(buttonStyle, assetStatus, renderer.sharedMaterials[i], !builtinMaterial, !builtinMaterial, !builtinMaterial && VCUtility.HaveAssetControl(assetStatus));

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


