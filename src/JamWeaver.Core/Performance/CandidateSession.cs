using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Performance;

public sealed class CandidateSession
{
    private readonly object _sync = new();
    private readonly PatternPlayer _player;
    private Pattern? _accepted;
    private Pattern? _candidate;
    private Pattern? _previousAccepted;

    public CandidateSession(PatternPlayer player) =>
        _player = player ?? throw new ArgumentNullException(nameof(player));

    public Pattern? Accepted { get { lock (_sync) return _accepted; } }
    public Pattern? Candidate { get { lock (_sync) return _candidate; } }
    public Pattern? PreviousAccepted { get { lock (_sync) return _previousAccepted; } }

    public void SetCandidate(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        lock (_sync)
        {
            if (_accepted is null) _accepted = pattern;
            _candidate = pattern;
            _player.Queue(pattern);
        }
    }

    public void Accept()
    {
        lock (_sync)
        {
            var candidate = RequireCandidate();
            if (_player.PendingPattern is not null || _player.CurrentPattern?.Id != candidate.Id)
                throw new InvalidOperationException("The candidate must become audible before it can be accepted.");
            if (_accepted?.Id == candidate.Id) return;
            _previousAccepted = _accepted;
            _accepted = candidate;
        }
    }

    public void Reject()
    {
        lock (_sync)
        {
            if (_accepted is null) throw new InvalidOperationException("There is no accepted pattern.");
            _candidate = _accepted;
            _player.Queue(_accepted);
        }
    }

    public void Undo()
    {
        lock (_sync)
        {
            if (_previousAccepted is null) throw new InvalidOperationException("There is no previous accepted pattern to restore.");
            var displaced = _accepted;
            _accepted = _previousAccepted;
            _previousAccepted = displaced;
            _candidate = _accepted;
            _player.Queue(_accepted);
        }
    }

    public void RenameAccepted(Pattern renamed)
    {
        ArgumentNullException.ThrowIfNull(renamed);
        lock (_sync)
        {
            if (_accepted is null || _accepted.Id != renamed.Id)
                throw new InvalidOperationException("The renamed pattern is not the accepted pattern.");
            _accepted = renamed;
            if (_candidate?.Id == renamed.Id) _candidate = renamed;
            if (_previousAccepted?.Id == renamed.Id) _previousAccepted = renamed;
            _player.ReplaceMetadata(renamed);
        }
    }

    private Pattern RequireCandidate() =>
        _candidate ?? throw new InvalidOperationException("Generate a candidate pattern first.");
}
