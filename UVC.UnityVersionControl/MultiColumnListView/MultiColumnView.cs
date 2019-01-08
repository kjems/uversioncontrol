// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

using TC = UnityEngine.GUIContent;


internal static class MultiColumnView
{
    public class MultiColumnViewOption<TD>
    {
        public GUIStyle headerStyle;
        public GUIStyle rowStyle;
        public Func<MultiColumnState<TD,TC>.Column, GenericMenu> headerRightClickMenu;
        public Func<MultiColumnState<TD, TC>.Row, MultiColumnState<TD, TC>.Column, GenericMenu> rowRightClickMenu;
        public Func<MultiColumnState<TD, TC>.Row, MultiColumnState<TD, TC>.Column, bool> cellClickAction;
        public float[] widths;
        public Vector2 scrollbarPos;
        public readonly Dictionary<string, float> widthTable = new Dictionary<string, float>();
        public Action<TD> doubleClickAction;
    }

    static bool InBetween(int n, int start, int end) { return ((n >= start && n <= end) || (n <= start && n >= end)); }
    static readonly int listViewHash = "MultiColumnView.ListView".GetHashCode();
    static int selectedIdx = -1;

    public static void ListView<TD>(Rect rect, MultiColumnState<TD, TC> multiColumnState, MultiColumnViewOption<TD> mvcOption)
    {
        UVC.ProfilerUtilities.BeginSample("MultiColumnView::ListView");
        bool controlModifier = ((Application.platform == RuntimePlatform.OSXEditor) ? Event.current.command : Event.current.control);

        GUI.BeginGroup(rect);
        float headerHeight = mvcOption.headerStyle.lineHeight + mvcOption.headerStyle.margin.vertical + mvcOption.headerStyle.padding.vertical;
        float rowHeight = mvcOption.rowStyle.lineHeight + mvcOption.rowStyle.margin.vertical;

        float scrollbarWidth = 0.0f;
        float total = multiColumnState.GetRowCount();
        int size = Mathf.RoundToInt((rect.height - headerHeight) / rowHeight);
        if (total > size)
        {
            scrollbarWidth = 16.0f;
            mvcOption.scrollbarPos.y = GUI.VerticalScrollbar(new Rect(rect.width - scrollbarWidth, 0, rect.width, rect.height), mvcOption.scrollbarPos.y, size, 0, total);
            if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
            {
                mvcOption.scrollbarPos.y += Mathf.Sign(Event.current.delta.y) * 3.0f;
                Event.current.Use();
            }
        }

        GUI.BeginGroup(new Rect(0, 0, rect.width - scrollbarWidth, rect.height));
        var headers = multiColumnState.GetColumns().Select(c => c.GetHeader());
        var widths = headers.Select(c => mvcOption.widthTable[c.text]).ToArray();
        float maxWidth = widths.Sum();

        var headerRect = new Rect(0, 0, maxWidth, headerHeight);
        ListViewHeader(headerRect, c => { multiColumnState.Accending = !multiColumnState.Accending; multiColumnState.SetSortByColumn(c); }, () => false, multiColumnState.GetColumns(), mvcOption);

        int lowIdx = Mathf.RoundToInt(mvcOption.scrollbarPos.y);
        int highIdx = lowIdx + size;
        var totalRows = multiColumnState.GetRows();
        var rows = totalRows.Where((_, idx) => InBetween(idx, lowIdx, highIdx));

        int currentIdx = lowIdx;
        float rowHeighStart = headerHeight;
        foreach (var rowIt in rows)
        {
            //D.Log("C# null: " + ((rowIt.data==null)?"true":"false") + ", Unity: " + rowIt.data + ", Type: " + rowIt.data.GetType());
            Action selectAction = () =>
                {
                    if (!controlModifier)
                        foreach (var r in totalRows)
                            r.selected = false;

                    if ((Event.current.modifiers & EventModifiers.Shift) > 0)
                    {
                        var selection = totalRows.Where((_, idx) => InBetween(idx, selectedIdx, currentIdx));
                        foreach (var e in selection)
                            e.selected = true;
                    }
                    else
                    {
                        selectedIdx = currentIdx;
                        rowIt.selected = !rowIt.selected;
                    }
                    if (Event.current.clickCount > 1)
                    {
                        var selection = totalRows.Where(idx => idx.selected);
                        foreach (var e in selection)
                            mvcOption.doubleClickAction(e.data);
                    }
                };

            var rowRect = new Rect(0, rowHeighStart, maxWidth, rowHeight);
            ListViewRow(rowRect, selectAction, () => rowIt.selected, rowIt, widths, mvcOption);

            rowHeighStart += rowHeight;
            currentIdx++;
        }
        GUI.EndGroup();
        GUI.EndGroup();


        int id = GUIUtility.GetControlID(listViewHash, FocusType.Passive);
        Event ev = Event.current;
        EventType evt = ev.GetTypeForControl(id);
        if (rect.Contains(ev.mousePosition) && evt == EventType.MouseDown && ev.button == 0)
        {
            Event.current.Use();
            foreach (var r in totalRows)
                r.selected = false;
        }


        if (controlModifier && Event.current.keyCode == KeyCode.A)
        {
            foreach (var r in totalRows)
                r.selected = true;
            Event.current.Use();
        }
        UVC.ProfilerUtilities.EndSample();
    }



    static readonly int listViewCellHash = "MultiColumnView.ListViewCell".GetHashCode();
    const float dragResize = 10.0f;
    enum DragType { Normal, Resize }
    static DragType dragTypeControl = DragType.Normal;

    static void ListViewHeader<TD>(Rect rect, Action<MultiColumnState<TD, TC>.Column> action, Func<bool> selectedFunc, IEnumerable<MultiColumnState<TD, TC>.Column> columns, MultiColumnViewOption<TD> mvcOption)
    {

        float x = rect.x;
        foreach (var columnIt in columns)
        {
            var cell = columnIt.GetHeader();
            float width = mvcOption.widthTable[cell.text];
            var r = new Rect(x, rect.y, width, rect.height);
            bool bHover = r.Contains(Event.current.mousePosition);
            Action<Vector2> dragAction = v => { mvcOption.widthTable[cell.text] = Mathf.Max(mvcOption.widthTable[cell.text] + v.x, dragResize); };

            ListViewCell<TD>(r, () => action(columnIt), dragAction, selectedFunc, bHover, cell, mvcOption.headerStyle, () => mvcOption.headerRightClickMenu(columnIt), () => false);
            x += width;
        }
    }

    static void ListViewRow<TD>(Rect rect, Action action, Func<bool> selectedFunc, MultiColumnState<TD, TC>.Row row, float[] widths, MultiColumnViewOption<TD> mvcOption)
    {
        //int id = GUIUtility.GetControlID(listViewCellHash, FocusType.Native);
        var columns = row.Columns.ToArray();
        bool bHover = rect.Contains(Event.current.mousePosition);
        float x = rect.x;
                
        for (int i = 0; i < widths.Length && i < columns.Length; ++i)
        {
            var width = widths[i];
            var column = columns[i];

            var r = new Rect(x, rect.y, width, rect.height);
            ListViewCell<TD>(r, action, _ => { }, selectedFunc, bHover, column.GetContent(row.data), mvcOption.rowStyle, () => mvcOption.rowRightClickMenu(row, column), () => mvcOption.cellClickAction(row, column));
            x += width;
        }
    }

    static void ListViewCell<TD>(Rect rect, Action action, Action<Vector2> dragAction, Func<bool> selectedFunc, bool bHover, GUIContent content, GUIStyle style, Func<GenericMenu> contextMenu, Func<bool> cellClickAction)
    {
        int id = GUIUtility.GetControlID(listViewCellHash, FocusType.Passive);
        Event e = Event.current;
        switch (e.GetTypeForControl(id))
        {
            case EventType.ContextClick: ListViewCellContext(id, e, rect, contextMenu()); break;
            case EventType.MouseDown: ListViewCellMouseDown(id, e, rect, action, cellClickAction); break;
            case EventType.MouseUp: ListViewCellMouseUp(id, e, rect, action); break;
            case EventType.MouseDrag: ListViewCellMouseDrag(id, e, rect, dragAction); break;
            case EventType.Repaint: ListViewCellRepaint(id, e, rect, selectedFunc, bHover, content, style); break;
        }
    }

    static void ListViewCellContext(int id, Event e, Rect rect, GenericMenu contextMenu)
    {
        if (rect.Contains(e.mousePosition))
        {
            if (contextMenu != null) contextMenu.DropDown(new Rect(e.mousePosition.x, e.mousePosition.y, rect.width, rect.height));
            Event.current.Use();
            //GUIUtility.hotControl = id;
        }
    }

    static void ListViewCellMouseDown(int id, Event e, Rect rect, Action action, Func<bool> cellClickAction)
    {
        if (rect.Contains(e.mousePosition) && e.button == 0)
        {
            var r = new Rect(rect.xMax - dragResize, rect.y, dragResize, rect.height);
            if (r.Contains(e.mousePosition))
            {
                dragTypeControl = DragType.Resize;
            }
            else
            {
                dragTypeControl = DragType.Normal;
                if(!cellClickAction())
                    action();
            }
            GUIUtility.hotControl = id;
            Event.current.Use();
        }
    }

    static void ListViewCellMouseUp(int id, Event e, Rect rect, Action action)
    {
        if (GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            Event.current.Use();
        }
    }

    static void ListViewCellMouseDrag(int id, Event e, Rect rect, Action<Vector2> dragAction)
    {
        if (GUIUtility.hotControl == id && dragTypeControl == DragType.Resize)
        {
            dragAction(Event.current.delta);
            Event.current.Use();
        }
    }

    static void ListViewCellRepaint(int id, Event e, Rect rect, Func<bool> selectedFunc, bool bHover, GUIContent content, GUIStyle style)
    {
        var r = new Rect(rect.xMax - dragResize, rect.y, dragResize, rect.height);
        EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);

        bool bActive = GUIUtility.hotControl == id && dragTypeControl != DragType.Resize;
        bool bOn = selectedFunc();
        bool bKeyboardFocus = GUIUtility.keyboardControl == id;

        style.Draw(rect, content, bHover, bActive, bOn, bKeyboardFocus);
    }
}
