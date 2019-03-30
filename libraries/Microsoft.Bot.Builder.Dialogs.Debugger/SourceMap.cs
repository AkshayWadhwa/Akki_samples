﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Bot.Builder.Dialogs.Declarative;

namespace Microsoft.Bot.Builder.Dialogs.Debugger
{
    public interface IBreakpoints
    {
        bool IsBreakPoint(object item);
        object ItemFor(Protocol.Breakpoint breakpoint);
        IReadOnlyList<Protocol.Breakpoint> SetBreakpoints(Protocol.Source source, IReadOnlyList<Protocol.SourceBreakpoint> sourceBreakpoints);
        IReadOnlyList<Protocol.Breakpoint> ApplyUpdates();
    }

    public sealed class SourceMap : Source.IRegistry, IBreakpoints
    {
        private readonly object gate = new object();
        private readonly Dictionary<object, Source.Range> sourceByItem = new Dictionary<object, Source.Range>(ReferenceEquality<object>.Instance);
        private bool dirty = true;

        void Source.IRegistry.Add(object item, Source.Range range)
        {
            lock (gate)
            {
                sourceByItem[item] = range;
                dirty = true;
            }
        }

        bool Source.IRegistry.TryGetValue(object item, out Source.Range range)
        {
            lock (gate)
            {
                if (item != null)
                {
                    return sourceByItem.TryGetValue(item, out range);
                }
                else
                {
                    range = default(Source.Range);
                    return false;
                }
            }
        }

        public sealed class Row
        {
            public Row(Protocol.Source source, Protocol.SourceBreakpoint sourceBreakpoint, Protocol.Breakpoint breakpoint)
            {
                Source = source;
                SourceBreakpoint = sourceBreakpoint;
                Breakpoint = breakpoint;
            }
            public Protocol.Source Source { get; }
            public Protocol.SourceBreakpoint SourceBreakpoint { get; }
            public Protocol.Breakpoint Breakpoint { get; }
            public object item { get; set; }
        }

        private readonly Identifier<Row> rows = new Identifier<Row>();
        private readonly HashSet<object> items = new HashSet<object>(ReferenceEquality<object>.Instance);

        private IReadOnlyList<Protocol.Breakpoint> Update()
        {
            lock (gate)
            {
                items.Clear();

                var changes = new List<Protocol.Breakpoint>();
                foreach (var kv in rows)
                {
                    var row = kv.Value;

                    var options = from sourceItem in sourceByItem
                                  let source = sourceItem.Value
                                  where source.Path == row.Source.path
                                  where source.Start.LineIndex >= row.SourceBreakpoint.line
                                  let distance = Math.Abs(source.Start.LineIndex - row.SourceBreakpoint.line)
                                  orderby distance
                                  select sourceItem;

                    options = options.ToArray();

                    var best = options.FirstOrDefault();
                    var itemNew = best.Key;
                    var verifiedNew = itemNew != null;
                    var lineNew = verifiedNew
                        ? best.Value.Start.LineIndex
                        : row.SourceBreakpoint.line;

                    var itemOld = row.item;
                    var verifiedOld = row.Breakpoint.verified;
                    var lineOld = row.Breakpoint.line;

                    var changed = itemNew != itemOld
                        || verifiedNew != verifiedOld
                        || lineNew != lineOld;

                    if (changed)
                    {
                        changes.Add(row.Breakpoint);
                        row.item = itemNew;
                        row.Breakpoint.verified = verifiedNew;
                        row.Breakpoint.line = lineNew;
                    }

                    if (itemNew != null)
                    {
                        items.Add(itemNew);
                    }
                }

                return changes;
            }
        }

        IReadOnlyList<Protocol.Breakpoint> IBreakpoints.SetBreakpoints(Protocol.Source source, IReadOnlyList<Protocol.SourceBreakpoint> sourceBreakpoints)
        {
            lock (gate)
            {
                var path = source.path;
                foreach (var kv in rows)
                {
                    var row = kv.Value;
                    if (row.Source.path == path)
                    {
                        rows.Remove(row);
                    }
                }

                foreach (var sourceBreakpoint in sourceBreakpoints)
                {
                    var breakpoint = new Protocol.Breakpoint() { source = source };
                    var row = new Row(source, sourceBreakpoint, breakpoint);
                    breakpoint.id = this.rows.Add(row);
                }

                return Update();
            }
        }

        IReadOnlyList<Protocol.Breakpoint> IBreakpoints.ApplyUpdates()
        {
            lock (gate)
            {
                IReadOnlyList<Protocol.Breakpoint> updates = Array.Empty<Protocol.Breakpoint>();
                if (dirty)
                {
                    updates = Update();
                    dirty = false;
                }

                return updates;
            }
        }

        bool IBreakpoints.IsBreakPoint(object item)
        {
            lock (gate)
            {
                return this.items.Contains(item);
            }
        }

        object IBreakpoints.ItemFor(Protocol.Breakpoint breakpoint)
        {
            lock (gate)
            {
                return this.rows[breakpoint.id].item;
            }
        }

    }
}
