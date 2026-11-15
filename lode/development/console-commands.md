# Console command parsing

`ConsoleCommand` is the console's parsing boundary. It trims the input line once,
ignores blank lines, and derives the command token, positional tokens, and
free-text argument from that same text. Command names are case-insensitive.
Whitespace, including tabs, separates positional tokens. Leading and trailing
whitespace is ignored; internal whitespace in free-text names is preserved.
There is no quoting or escape syntax.

`ConsoleArgumentParser` converts tokens to numeric, MIDI, enum, and named-choice
values. Numeric conversion uses invariant culture; core value types retain MIDI
channel/data and strength validation. The command loop retains dispatch, help
queries, library-name resolution, and command effects. `VariationArguments`
parses the complete optional variation grammar into `GeneratorVariationSettings`.

`help generator`, `generator help`, and `generator ?` show the same comparison
of generator purpose, bar count, active controls, and limitations without
changing the selected mode or candidate. Bare `generator` and generator
selection also describe the selected mode. Generation controls apply to the
next `new`; `vary` follows the current candidate's recipe lineage instead.
Selectable generators are `euclidean`, `turnaround`, `motif`, `phrase`, and
`groove`. Both `euclidean-phrase` and its former `euclidean2` alias report a
retirement error without changing selection or candidate. Their saved patterns
remain loadable and playable, and lineage dispatch still supports variation.

`JamWeaverConsole` retains an explicit command switch. Named operations handle
source selection, output selection, save, load, variation, comparison, history
browsing, and `keep`/`revert`. The console parses an optional comparison seed and
delegates to its internal `CandidateComparison`, which owns the prepared pair,
toggle selection, comparison history entries, candidate selection, and responses.
Callers do not inspect the pair or decide whether a bare command prepares or
toggles it. Argument counts are checked before dispatch, and operation failures
return to the loop's error handler. MIDI ownership, pattern validation,
persistence, playback activation, and accepted-safe-point policy remain in
their existing owners.

## Argument counts

The parser checks argument counts before dispatch, so malformed counts cannot
open ports, send MIDI, consume random seeds, mutate candidates or controls,
save files, or terminate the session. Errors include the command and a help
hint; the command loop reports them and continues reading. Unknown commands
retain the ordinary unknown-command response.

- No-argument commands reject all arguments, including `quit` and `exit`.
- `out`, `in`, `source`, `bpm`, `new`, `compare`, `generator`, `shape`, `ch`,
  `channel`, and `help` accept zero or one argument. Their existing bare forms
  list, query, or use defaults.
- `groove`, `similarity`, `length`, `activity`, `rhythm`, `movement`, `variation`,
  `turnaround`, `key`, and `role` require one argument.
- `pc` requires two; `on` and `cc` require three. `off` accepts two or three,
  and `note` accepts three or four.
- `vary` accepts up to four arguments. Its operation also validates the existing
  `[target] [strength] [seed <number>]` grammar before mutation.
- `save` and `load` consume the entire free-text argument. Bare `save` keeps the
  accepted name; bare `load` lists the library. Extra words are part of the name,
  rather than unexpected positional arguments.

Variation first requires a candidate, then parses the optional grammar
`[target] [strength] [seed <number>]` with case-insensitive target and `seed`
tokens. The default strength is 0.3. Defined `VariationTarget` numeric values
remain accepted as targets and take
precedence over strength: `vary 0` selects rhythm and `vary 1` selects notes.
Use `0.0` or `1.0` for untargeted boundary strengths. This is existing syntax
compatibility; revising it requires a separate behavior decision.

Malformed variation grammar consumes no fresh seed. Once the grammar is valid,
the seed is resolved before the core `NormalizedAmount` validates strength.
An omitted seed is therefore consumed even if the strength is subsequently
rejected as out of range or non-finite. Explicit seeds do not consume a fresh
seed. Candidate, history, controls, and playback remain unchanged on these
failures, and the command loop continues.

Adding a command or changing its syntax requires updating both the explicit
dispatch and the parser's count rule. Value and domain validation still belong
to the relevant operation; count validation does not promise that every failure
in a compound operation rolls back all earlier effects.

Command-loop regression tests use fake ports and temporary pattern libraries.
They cover indented bare commands, named and numbered load, preserved internal
spaces, optional syntax, and rejected counts without MIDI, candidate, control,
persistence, or random-seed effects.

Scripted candidate-workflow checkpoints also cover comparison creation and
reuse, toggling, history browsing and branching, failures, and `keep`/`revert`
safe-point behavior. Direct external-clock pulses verify running next-bar
activation and pending acceptance rejection without timing sleeps. Checkpoints
run between commands outside the command-error handler so assertion failures
cannot be reported as recoverable console errors.

Variation cases compare materialized output and recipes with direct generator
calls and protect defaults, targets, strengths, explicit/fresh seeds, malformed
grammar, and candidate/seed validation order. Additional command-loop cases
protect named choices, aliases, MIDI boundaries, and unchanged state after
value rejection.

Related: [project structure](../architecture/project-structure.md),
[pattern library](../persistence/pattern-library.md), and
[clock and transport](../midi/clock-transport.md).
