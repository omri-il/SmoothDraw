using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;

namespace SmoothDraw.Services;

/// <summary>One undoable change.</summary>
public interface IStep
{
    void Undo();
    void Redo();
}

/// <summary>
/// Undo / redo for the overlay: commands, not snapshots. Each step keeps the REAL strokes
/// and elements it added or removed, never copies: laser strokes share the ink canvas and
/// LaserService holds references to them, and the Select tool's arrow pairs are keyed by
/// the elements themselves, so a copy put back would be a stranger to both.
///
/// Laser strokes are never recorded (they fade on their own). Positions are indices at
/// the time of the change; since laser strokes can sit in between, putting something back
/// uses Math.Min(index, Count), which at worst puts it a laser stroke off in the z-order.
/// </summary>
public sealed class DrawingHistory
{
    public const int MaxSteps = 100;

    private readonly LinkedList<IStep> _undo = new();
    private readonly Stack<IStep> _redo = new();
    private GroupStep? _group;

    /// <summary>After every change of what can be undone or redone (the toolbar dims its buttons).</summary>
    public event Action? Changed;

    public int UndoCount => _undo.Count + (_group != null ? 1 : 0);
    public int RedoCount => _redo.Count;

    /// <summary>A finished change. Anything that was undone can no longer be redone.</summary>
    public void Push(IStep step)
    {
        FlushGroup();
        _redo.Clear();
        Record(step);
    }

    /// <summary>Starts collecting steps into one (an eraser drag erases stroke by stroke,
    /// but is undone in one go).</summary>
    public void BeginGroup()
    {
        FlushGroup();
        _group = new GroupStep();
    }

    /// <summary>Adds to the open group, opening one if there is none.</summary>
    public void AddToGroup(IStep step)
    {
        if (_group == null) BeginGroup();
        _redo.Clear();
        _group!.Steps.Add(step);
        Changed?.Invoke();
    }

    /// <summary>Closes the open group: from now on it is one step.</summary>
    public void FlushGroup()
    {
        var group = _group;
        _group = null;
        if (group is { Steps.Count: > 0 }) Record(group);
    }

    public bool Undo()
    {
        FlushGroup();
        if (_undo.Last is not { } last) return false;
        _undo.RemoveLast();
        last.Value.Undo();
        _redo.Push(last.Value);
        Changed?.Invoke();
        return true;
    }

    public bool Redo()
    {
        FlushGroup();
        if (_redo.Count == 0) return false;
        var step = _redo.Pop();
        step.Redo();
        Record(step);
        return true;
    }

    private void Record(IStep step)
    {
        _undo.AddLast(step);
        if (_undo.Count > MaxSteps) _undo.RemoveFirst();
        Changed?.Invoke();
    }
}

// --- The steps ---

/// <summary>Taking things out and putting them back where they were. Removal goes by
/// descending index and putting back by ascending index, so each lands at its old place.</summary>
internal static class ZOrder
{
    public static void Remove(StrokeCollection strokes, IEnumerable<(Stroke Stroke, int Index)> items)
    {
        foreach (var (stroke, _) in items.OrderByDescending(i => i.Index))
            if (strokes.Contains(stroke)) strokes.Remove(stroke);
    }

    public static void Insert(StrokeCollection strokes, IEnumerable<(Stroke Stroke, int Index)> items)
    {
        foreach (var (stroke, index) in items.OrderBy(i => i.Index))
            if (!strokes.Contains(stroke)) strokes.Insert(Math.Min(index, strokes.Count), stroke);
    }

    public static void Remove(UIElementCollection children, IEnumerable<(UIElement Element, int Index)> items)
    {
        foreach (var (element, _) in items.OrderByDescending(i => i.Index))
            if (children.Contains(element)) children.Remove(element);
    }

    public static void Insert(UIElementCollection children, IEnumerable<(UIElement Element, int Index)> items)
    {
        foreach (var (element, index) in items.OrderBy(i => i.Index))
            if (!children.Contains(element)) children.Insert(Math.Min(index, children.Count), element);
    }
}

/// <summary>A pen stroke with its shadow, or a highlighter stroke.</summary>
public sealed class InkAdded(StrokeCollection strokes, List<(Stroke Stroke, int Index)> items) : IStep
{
    public void Undo() => ZOrder.Remove(strokes, items);
    public void Redo() => ZOrder.Insert(strokes, items);
}

/// <summary>One stroke the eraser took (grouped per drag).</summary>
public sealed class InkRemoved(StrokeCollection strokes, Stroke stroke, int index) : IStep
{
    public void Undo() => ZOrder.Insert(strokes, new[] { (stroke, index) });
    public void Redo() => ZOrder.Remove(strokes, new[] { (stroke, index) });
}

/// <summary>Selected ink moved or resized: each stroke's points before and after. Applied
/// as clones, so the saved points are never the ones the canvas goes on changing.</summary>
public sealed class InkTransformed(List<(Stroke Stroke, StylusPointCollection Before, StylusPointCollection After)> items) : IStep
{
    public void Undo()
    {
        foreach (var (stroke, before, _) in items) stroke.StylusPoints = before.Clone();
    }

    public void Redo()
    {
        foreach (var (stroke, _, after) in items) stroke.StylusPoints = after.Clone();
    }
}

/// <summary>A shape (an arrow is its line + head), a text or a pasted image.</summary>
public sealed class ElementsAdded(UIElementCollection children, List<(UIElement Element, int Index)> items) : IStep
{
    public void Undo() => ZOrder.Remove(children, items);
    public void Redo() => ZOrder.Insert(children, items);
}

/// <summary>A shape dragged with the Select tool (with its arrow partner).</summary>
public sealed class ElementsMoved(List<UIElement> elements, double dx, double dy, Action<UIElement, double, double> move) : IStep
{
    public void Undo()
    {
        foreach (var e in elements) move(e, -dx, -dy);
    }

    public void Redo()
    {
        foreach (var e in elements) move(e, dx, dy);
    }
}

/// <summary>Clear all (toolbar, F10, pipe): every stroke but the laser's, and every shape.</summary>
public sealed class Cleared(
    StrokeCollection strokes, List<(Stroke Stroke, int Index)> strokeItems,
    UIElementCollection children, List<(UIElement Element, int Index)> elementItems) : IStep
{
    public void Undo()
    {
        ZOrder.Insert(strokes, strokeItems);
        ZOrder.Insert(children, elementItems);
    }

    public void Redo()
    {
        ZOrder.Remove(strokes, strokeItems);
        ZOrder.Remove(children, elementItems);
    }
}

/// <summary>Several steps undone and redone as one.</summary>
public sealed class GroupStep : IStep
{
    public List<IStep> Steps { get; } = new();

    public void Undo()
    {
        for (int i = Steps.Count - 1; i >= 0; i--) Steps[i].Undo();
    }

    public void Redo()
    {
        foreach (var step in Steps) step.Redo();
    }
}
