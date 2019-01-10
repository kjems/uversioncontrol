// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEngine;

namespace UVC.UserInterface
{
    internal static class GUIControls
    {
        private static readonly int labelHash = "GUI.Label".GetHashCode();

        public static bool Label(Rect rect, bool selected, GUIContent content, GUIStyle style)
        {
            var param = new ControlParameters(labelHash, rect, content, style)
                            {
                                bOn = selected,
                                onMouseDown = () => selected = true
                            };
            Control(param);
            return selected;
        }

        private static readonly int dragButtonHash = "GUI.DragButton".GetHashCode();

        public static Rect DragButton(Rect rect, GUIContent content, GUIStyle style)
        {
            var param = new ControlParameters(dragButtonHash, rect, content, style)
            {
                onMouseDrag = delta =>
                    {
                        rect.x += delta.x;
                        rect.y += delta.y;
                    }
            };
            Control(param);
            return rect;
        }

        public struct ControlParameters
        {
            public int hash;
            public Rect rect;
            public GUIContent content;
            public GUIStyle style;

            public int mouseButton;
            public bool bOn;

            public Action onMouseDown;
            public Action onMouseUp;
            public Action<Vector2> onMouseDrag;
            public Action onContextClick;

            public ControlParameters(int hash, Rect rect, GUIContent content, GUIStyle style)
            {
                this.hash = hash;
                this.rect = rect;
                this.content = content;
                this.style = style;

                mouseButton = default(int);
                bOn = default(bool);
                onMouseDown = default(Action);
                onMouseUp = default(Action);
                onMouseDrag = default(Action<Vector2>);
                onContextClick = default(Action);
            }
        }

        public static void Control(ControlParameters param)
        {
            int id = GUIUtility.GetControlID(param.hash, FocusType.Passive);
            Event e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    MouseDown(id, e, param);
                    break;
                case EventType.MouseUp:
                    MouseUp(id, e, param);
                    break;
                case EventType.MouseDrag:
                    MouseDrag(id, e, param);
                    break;
                case EventType.Repaint:
                    Repaint(id, e, param);
                    break;
            }
        }

        private static void MouseDown(int id, Event e, ControlParameters param)
        {
            if (param.rect.Contains(e.mousePosition) && e.button == param.mouseButton)
            {
                if (param.onMouseDown != null)
                    param.onMouseDown();

                GUIUtility.hotControl = id;
                Event.current.Use();
            }
        }

        private static void MouseUp(int id, Event e, ControlParameters param)
        {
            if (GUIUtility.hotControl == id && e.button == param.mouseButton)
            {
                if (param.onMouseUp != null)
                    param.onMouseUp();

                GUIUtility.hotControl = 0;
                Event.current.Use();
            }
        }

        private static void MouseDrag(int id, Event e, ControlParameters param)
        {
            if (GUIUtility.hotControl == id)
            {
                if (param.onMouseDrag != null)
                    param.onMouseDrag(Event.current.delta);

                Event.current.Use();
            }
        }

        private static void Repaint(int id, Event e, ControlParameters param)
        {
            if (param.style != null)
            {
                bool bHover = param.rect.Contains(e.mousePosition);
                bool bActive = GUIUtility.hotControl == id;
                bool bKeyboardFocus = GUIUtility.keyboardControl == id;
                param.style.Draw(param.rect, param.content, bHover, bActive, param.bOn, bKeyboardFocus);
            }
        }
    }

    internal static class GUILayoutHelper
    {

        public static PushState<Color> Color(Color color)
        {
            return new PushState<Color>(GUI.color, GUI.color = color, c => GUI.color = c);
        }

        public static PushState<Color> BackgroundColor(Color color)
        {
            return new PushState<Color>(GUI.backgroundColor, GUI.backgroundColor = color, c => GUI.backgroundColor = c);
        }

        public static PushState<Color> ContentColor(Color color)
        {
            return new PushState<Color>(GUI.contentColor, GUI.contentColor = color, c => GUI.contentColor = c);
        }

        public static PushState<bool> Enabled(bool bEnabled)
        {
            return Enabled(bEnabled, false);
        }

        public static PushState<bool> Enabled(bool bEnabled, bool bForce)
        {
            return new PushState<bool>(GUI.enabled, GUI.enabled = bEnabled && (GUI.enabled || bForce), b => GUI.enabled = b);
        }

        public static PushState Vertical(params GUILayoutOption[] options)
        {
            return new PushState(() => GUILayout.BeginVertical(options), GUILayout.EndVertical);
        }

        public static PushState Horizontal(params GUILayoutOption[] options)
        {
            return new PushState(() => GUILayout.BeginHorizontal(options), GUILayout.EndHorizontal);
        }

        public static PushState HorizontalCentered(params GUILayoutOption[] options)
        {
            return new PushState(() =>
            {
                GUILayout.BeginVertical(options);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal(options);
                GUILayout.FlexibleSpace();
            }, () =>
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
            );
        }

        public static PushState VerticalCentered(params GUILayoutOption[] options)
        {
            return new PushState(() =>
            {
                GUILayout.BeginHorizontal(options);
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical(options);
                GUILayout.FlexibleSpace();
            }, () =>
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            );
        }

        public static PushState VerticalIdented(float space, params GUILayoutOption[] options)
        {
            return new PushState(() =>
            {
                GUILayout.BeginHorizontal(options);
                GUILayout.Space(space);
                GUILayout.BeginVertical(options);
            }, () =>
            {
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            );
        }
    }
}
