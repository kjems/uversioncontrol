// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.Linq;

internal class MultiColumnState<TD, TC>
{
    public class Column
    {
        public Column(TC headerElement, Func<TD, TC> getRowElementFunc)
        {
            this.headerElement = headerElement;
            this.getRowElementFunc = getRowElementFunc;
        }

        public TC GetContent(TD data)
        {
            return getRowElementFunc(data);
        }

        public TC GetHeader()
        {
            return headerElement;
        }

        readonly TC headerElement;
        readonly Func<TD, TC> getRowElementFunc;
    }

    public class Row
    {
        internal Row(ref TD data, IEnumerable<Column> cells)
        {
            this.cells = cells;
            this.data = data;
        }

        public IEnumerable<TC> Cells
        {
            get { return (from cell in cells select cell.GetContent(data)); }
        }

        public IEnumerable<Column> Columns
        {
            get { return cells; }
        }

        public readonly TD data;
        public bool selected;
        readonly IEnumerable<Column> cells;
    }

    public MultiColumnState()
    {
        Refresh(new List<TD>());
    }

    public MultiColumnState(IEnumerable<TD> domainDatas)
    {
        Refresh(domainDatas);
    }

    public void Refresh(IEnumerable<TD> domainDatas)
    {
        rows = domainDatas.Select((d, index) => new Row(ref d, columns)).ToList();
        SortByColumn();
    }

    public IEnumerable<Row> GetRows()
    {
        return rows;
    }

    public int GetRowCount() { return rows.Count(); }

    public IEnumerable<Column> GetColumns()
    {
        return columns;
    }

    public IEnumerable<TD> GetSelected()
    {
        return rows.Where(r => r.selected).Select(r => r.data);
    }

    public void RemoveColumn(Column column)
    {
        columns.Remove(column);
    }

    public void AddColumn(Column column)
    {
        columns.Add(column);
    }

    public bool ExistColumn(Column column)
    {
        return columns.Any(c => c == column);
    }

    public int CountColumns()
    {
        return columns.Count;
    }

    private void SortByColumn()
    {
        if (sortByColumn != null && Comparer != null)
            rows.Sort((l, r) => Comparer(l, r, sortByColumn) * (Accending ? 1 : -1));
    }

    public void SetSortByColumn(Column column)
    {
        sortByColumn = column;
        SortByColumn();
    }

    public void PerformActionOnSelected(Action<TD> action)
    {
        foreach (var selected in GetSelected())
            action(selected);
    }

    public Func<Row, Row, Column, int> Comparer { private get; set; }
    public bool Accending { get; set; }

    List<Row> rows;
    Column sortByColumn;
    readonly List<Column> columns = new List<Column>();
}
