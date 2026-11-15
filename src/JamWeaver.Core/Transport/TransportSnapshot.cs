namespace JamWeaver.Core.Transport;

/// <summary>Captures source, state, next pulse, and accepted Start count in one consistent read.</summary>
public readonly record struct TransportSnapshot(
    ClockSource Source, TransportState State, TransportPosition Position, ulong StartCount);
