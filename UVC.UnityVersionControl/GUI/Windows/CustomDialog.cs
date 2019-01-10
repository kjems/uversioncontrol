// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

namespace UVC.UserInterface
{
    public class CustomDialog : EditorWindow
    {
        private static readonly Vector2 defaultSize = new Vector2(400, 200);
        private Action body;
        private List<Action> buttons;

        public static CustomDialog Create(string title)
        {
            var dialog = ScriptableObject.CreateInstance<CustomDialog>();
            dialog.titleContent = new GUIContent(title);
            dialog.buttons = new List<Action>();
            dialog.minSize = defaultSize;
            return dialog;
        }

        public CustomDialog SetBodyText(string bodyText)
        {
            this.body = () => GUILayout.Label(bodyText);
            return this;
        }

        public CustomDialog SetBodyGUI(Action bodyGUI)
        {
            this.body = bodyGUI;
            return this;
        }

        public CustomDialog AddButton(string name, Action buttonAction, params GUILayoutOption[] options)
        {
            buttons.Add(() => { if (GUILayout.Button(name, options)) { buttonAction(); } });
            return this;
        }

        public CustomDialog SetSize(Vector2 size)
        {
            this.minSize = size;
            this.maxSize = size;
            return this;
        }

        public CustomDialog SetPosition(Rect position)
        {
            this.position = position;
            return this;
        }

        public CustomDialog CenterOnScreen()
        {
            this.position = new Rect {
                xMin    = Screen.width * 0.5f - this.minSize.x,
                yMin    = Screen.height * 0.5f - this.minSize.y,
                width   = this.minSize.x,
                height  = this.minSize.y
            };
            return this;
        }

        void OnGUI()
        {
            if (body != null)
                body();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var button in buttons)
            {
                button();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    public class CustomDialogs
    {
        public static CustomDialog CreateMessageDialog(string title, string message, MessageType type = MessageType.None)
        {
            CustomDialog dialog = CustomDialog.Create(title);
            dialog
            .CenterOnScreen()
            .SetBodyGUI(() =>
            {
                EditorGUILayout.HelpBox(message, type);
                GUILayout.FlexibleSpace();
            });

            return dialog;
        }

        public static CustomDialog CreateExceptionDialog(string title, VCException e)
        {
            return CreateExceptionDialog(title, e.ErrorMessage, e);
        }
        public static CustomDialog CreateExceptionDialog(string title, string message, VCException e)
        {
            bool stackTraceToggle = false;
            bool innerStackTraceToggle = false;
            bool detailsToggle = false;
            bool isCritical = e is VCCriticalException;
            Vector2 scrollPos = Vector2.zero;
            CustomDialog dialog = CustomDialog.Create(title);
            dialog
            .CenterOnScreen()
            .SetBodyGUI(() =>
            {
                if (e != null)
                {
                    scrollPos = GUILayout.BeginScrollView(scrollPos);
                    if (!string.IsNullOrEmpty(message))
                    {
                        EditorGUILayout.HelpBox(message, isCritical ? MessageType.Error : MessageType.Warning);
                    }

                    if (!string.IsNullOrEmpty(e.ErrorDetails))
                    {
                        detailsToggle = GUILayout.Toggle(detailsToggle, "Details", EditorStyles.foldout);
                        if (detailsToggle)
                        {
                            using (GUILayoutHelper.VerticalIdented(14))
                            {
                                GUILayout.BeginVertical(GUI.skin.box);
                                GUILayout.TextField(e.ErrorDetails);
                                GUILayout.EndVertical();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(e.StackTrace))
                    {
                        stackTraceToggle = GUILayout.Toggle(stackTraceToggle, "Stacktrace", EditorStyles.foldout);
                        if (stackTraceToggle)
                        {
                            using (GUILayoutHelper.VerticalIdented(14))
                            {
                                GUILayout.BeginVertical(GUI.skin.box);
                                GUILayout.TextField(e.StackTrace);
                                GUILayout.EndVertical();
                            }
                        }
                    }

                    if (e.InnerException != null)
                    {
                        if (!string.IsNullOrEmpty(e.InnerException.StackTrace))
                        {
                            innerStackTraceToggle = GUILayout.Toggle(innerStackTraceToggle, "Inner Stacktrace", EditorStyles.foldout);
                            if (innerStackTraceToggle)
                            {
                                using (GUILayoutHelper.VerticalIdented(14))
                                {
                                    GUILayout.BeginVertical(GUI.skin.box);
                                    GUILayout.TextField(e.InnerException.StackTrace);
                                    GUILayout.EndVertical();
                                }
                            }
                        }
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndScrollView();
                }

            })
            .AddButton("OK", () => dialog.Close(), GUILayout.Width(60f))
            .AddButton("Copy To Clipboard", () =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("Title: {0}\r\n\r\n", title);
                if (!string.IsNullOrEmpty(message)) sb.AppendFormat("Message: {0}\r\n", message);
                if (!string.IsNullOrEmpty(e.ErrorDetails)) sb.AppendFormat("\r\nDetails:\r\n{0}\r\n", e.ErrorDetails);
                if (!string.IsNullOrEmpty(e.StackTrace)) sb.AppendFormat("\r\nStacktrace:\r\n{0}\r\n", e.StackTrace);
                if (e.InnerException != null && !string.IsNullOrEmpty(e.StackTrace)) sb.AppendFormat("\r\nInner Stacktrace:\r\n{0}\r\n", e.InnerException.StackTrace);

                EditorGUIUtility.systemCopyBuffer = sb.ToString();
            });

            return dialog;
        }
    }
}