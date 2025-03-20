using System.Collections.Generic;
using VTMLEditor.GuiElements.Vanilla;

namespace VTMLEditor.GuiElements.Vanilla
{
    public class TextDelta
    {
        public int StartIndex { get; }
        public string RemovedText { get; }
        public string AddedText { get; }

        public TextDelta(int startIndex, string removedText, string addedText)
        {
            StartIndex = startIndex;
            RemovedText = removedText;
            AddedText = addedText;
        }
    }
}
public class UndoRedoManager
{
    private const int MaxStackSize = 100;
    private readonly Stack<TextDelta> _undoStack = new();
    private readonly Stack<TextDelta> _redoStack = new();

    public void SaveDelta(TextDelta delta)
    {
        if (_undoStack.Count >= MaxStackSize)
        {
            _undoStack.Pop();
        }
        _undoStack.Push(delta);
        _redoStack.Clear();
    }

    public TextDelta? Undo()
    {
        if (_undoStack.Count == 0) return null;
        var delta = _undoStack.Pop();
        _redoStack.Push(delta);
        return delta;
    }

    public TextDelta? Redo()
    {
        if (_redoStack.Count == 0) return null;
        var delta = _redoStack.Pop();
        _undoStack.Push(delta);
        return delta;
    }

    public static TextDelta ComputeDelta(string oldText, string newText)
    {
        // Find first index where texts differ.
        int prefixIndex = 0;
        while (prefixIndex < oldText.Length && prefixIndex < newText.Length &&
               oldText[prefixIndex] == newText[prefixIndex])
        {
            prefixIndex++;
        }

        // If texts are identical.
        if (prefixIndex == oldText.Length && prefixIndex == newText.Length)
        {
            return new TextDelta(0, "", "");
        }

        // Find last index where texts still match.
        int oldSuffix = oldText.Length - 1;
        int newSuffix = newText.Length - 1;
        while (oldSuffix >= prefixIndex && newSuffix >= prefixIndex &&
               oldText[oldSuffix] == newText[newSuffix])
        {
            oldSuffix--;
            newSuffix--;
        }

        string removed = oldText.Substring(prefixIndex, oldSuffix - prefixIndex + 1);
        string added = newText.Substring(prefixIndex, newSuffix - prefixIndex + 1);

        return new TextDelta(prefixIndex, removed, added);
    }

    public void Dispose()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}