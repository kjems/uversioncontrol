// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEditor;
using UnityEngine;

namespace UVC
{
    [InitializeOnLoad]
    internal sealed class VCRefreshEditable
    {
        private static int previousSelectionHash = 0;
        private static Object[] previousSelection;

        static VCRefreshEditable()
        {
            EditorApplication.update += RefreshEditable;
            VCCommands.Instance.StatusCompleted += RefreshGUI;
            VCSettings.SettingChanged += RefreshGUI;
            Undo.postprocessModifications += mod =>
            {
                RefreshGUI();
                return mod;
            };
        }

        static void RefreshGUI()
        {
            previousSelectionHash = 0;
            RefreshEditable();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void MakeEditable(Object obj)
        {
            EditableManager.SetEditable(obj, true);
            GameObject go = obj as GameObject;
            if(go)
            {
                foreach (var componentIt in go.GetComponents<Component>())
                {
                    EditableManager.SetEditable(componentIt, true);
                }
            }
        }

        private static int GetSelectionHash(ref Object[] selection)
        {
            int hash = 17;
            for (int i = 0, count = selection.Length; i < count; ++i)
            {
                if (selection[i])
                    hash = 31 * hash + selection[i].GetHashCode();
            }
            return hash;
        }

        private static void MakePreviousEditable()
        {
            // Make previous selection editable so objects are never left in readonly state
            if (previousSelection != null && previousSelection.Length > 0)
            {
                foreach (var selectionIt in previousSelection)
                {
                    MakeEditable(selectionIt);
                }
            }
        }

        private static void RefreshEditable()
        {
            Object[] selection = Selection.objects;
            if (selection == null || selection.Length == 0)
            {
                previousSelectionHash = 0;
            }
            else if (previousSelectionHash != GetSelectionHash(ref selection))
            {
                MakePreviousEditable();
                foreach (var selectionIt in selection)
                {
                    if (selectionIt is GameObject)
                    {
                        EditableManager.RefreshEditableObject(selectionIt as GameObject);
                    }
                }

                previousSelection = selection;
                previousSelectionHash = GetSelectionHash(ref selection);
            }
        }
    }
}
