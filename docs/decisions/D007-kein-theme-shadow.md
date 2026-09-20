# D007 — Kein ThemeShadow im Popup (nativer Startup-Crash)

## Context

F001-T5 sah schwebende Glass-Cards mit Schatten vor (`ThemeShadow` als Shared-Resource,
`Translation="0,0,16"` an den Cards, `ShellRoot` als Receiver via `Receivers.Add`).
Nach dem Einbau starb der Prozess deterministisch beim Start: viermal
`Application Error 1000`, Ausnahmecode `0xC000027B` in `Microsoft.UI.Xaml.dll`
(WindowsAppRuntime 2.5.1), ohne dass eine managed Exception unseren
Crash-Handler (`birdy-crash.log`) erreichte.

## Decision

Kein `ThemeShadow` im Popup. Die Karten schweben optisch über
`CardBackgroundFillColor` + `CardStroke` auf dem Acrylic-Backdrop
(T4-Shell), ohne projizierten Schatten.

## Why

Eliminierung in Scratch-Worktrees (je Build + 45 s Laufzeit-Check):

- Volles T5-Styling (Cards + `ThemeShadow` + Receiver): Crash beim Start.
- Gleicher Stand ohne Shadow-Kram (Cards + runde Balken bleiben): läuft stabil.
- Gleicher Stand mit Shadow, aber ohne `Receivers.Add`: läuft stabil.

Der Crash hängt also an der Receiver-Verdrahtung (`ShellRoot`, transparenter
Vorfahre der Schattenwerfer) — ein nativer FailFast im Compositor, der sich
weder fangen noch loggen lässt. Ein unsichtbarer Schatten (ohne Receiver)
wäre toter Code; ein Schatten, der den Prozess tötet, ist keine Option.

## Consequences

- Die Karten wirken schwebend durch Fill/Stroke/Rundung, aber ohne
  Schlagschatten — die manuelle Abnahme (#15) bewertet, ob das genügt.
- Ein echter Schatten (z. B. Composition-`DropShadow` pro Karte) kommt nur
  mit einem verifizierten, absturzfreien Ansatz zurück — dann wird diese
  Decision revidiert.
