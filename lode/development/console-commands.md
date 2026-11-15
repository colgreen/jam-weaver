# Console command parsing

`ConsoleCommand` is the console's parsing boundary. It trims the input line once,
ignores blank lines, and derives the command token, positional tokens, and
free-text argument from that same text. Command names are case-insensitive.
Whitespace, including tabs, separates positional tokens. Leading and trailing
whitespace is ignored; internal whitespace in free-text names is preserved.
There is no quoting or escape syntax.

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
source selection, output selection, save, load, and variation; MIDI ownership,
pattern validation, and persistence remain in their existing owners.

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

Adding a command or changing its syntax requires updating both the explicit
dispatch and the parser's count rule. Value and domain validation still belong
to the relevant operation; count validation does not promise that every failure
in a compound operation rolls back all earlier effects.

Command-loop regression tests use fake ports and temporary pattern libraries.
They cover indented bare commands, named and numbered load, preserved internal
spaces, optional syntax, and rejected counts without MIDI, candidate, control,
persistence, or random-seed effects.

Related: [project structure](../architecture/project-structure.md),
[pattern library](../persistence/pattern-library.md), and
[clock and transport](../midi/clock-transport.md).
