# Simplify candidate workflow

## Goal

Give the performer one short-term pattern history and one explicit safe point,
without a second invisible accepted-pattern history.

## Resulting model

- Candidate is the pattern currently selected for audition.
- Accepted is the single in-memory safe point and the only pattern eligible for
  saving.
- Candidate history retains the eight most recent generated, transformed,
  varied, or loaded patterns for `back` and `forward` browsing.
- `keep` moves the safe point to the audible candidate.
- `revert` selects the safe point again without changing candidate history.
- `back` and `forward` select recent patterns without changing the safe point.
- `load <name>` loads a library entry as a candidate; `load #<number>`
  disambiguates duplicate names. Loading does not keep the candidate.
- `save [name]` persists the safe point.

The former safe point has no privileged second slot after acceptance. It can be
selected again while it remains in the bounded candidate history, or recovered
from the saved library if it was persisted. This limitation keeps the state
model visible and predictable.

## Changes

1. Remove `PreviousAccepted` and `Undo` from `CandidateSession`.
2. Remove the console `undo` command immediately; no compatibility alias is
   needed for the current single-user application.
3. Rename the console `recall` command to `load`, with no compatibility alias.
4. Update command output and help to use plain descriptions of recent patterns
   and the accepted safe point.
5. Update deterministic tests for the simplified session contract.
6. Update current-state Lode documents while retaining physical-device
   validation facts where the underlying behavior remains relevant.

## Verification

- Run the full automated test suite.
- Run `dotnet build`.
- Hardware verification is not required because this change removes session
  state and renames a console command without changing MIDI message handling.
