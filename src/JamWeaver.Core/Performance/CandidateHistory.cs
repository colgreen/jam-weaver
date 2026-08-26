namespace JamWeaver.Core.Performance;

public sealed class CandidateHistory
{
    private readonly int _capacity;
    private readonly List<Pattern> _items = [];
    private int _index = -1;

    public CandidateHistory(int capacity = 8)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count => _items.Count;
    public int Position => _index < 0 ? 0 : _index + 1;
    public bool CanPrevious => _index > 0;
    public bool CanNext => _index >= 0 && _index < _items.Count - 1;

    public void Add(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (CanNext) _items.RemoveRange(_index + 1, _items.Count - _index - 1);
        _items.Add(pattern);
        if (_items.Count > _capacity) _items.RemoveAt(0);
        _index = _items.Count - 1;
    }

    public Pattern Previous()
    {
        if (!CanPrevious) throw new InvalidOperationException("There is no previous audition candidate.");
        return _items[--_index];
    }

    public Pattern Next()
    {
        if (!CanNext) throw new InvalidOperationException("There is no next audition candidate.");
        return _items[++_index];
    }
}
