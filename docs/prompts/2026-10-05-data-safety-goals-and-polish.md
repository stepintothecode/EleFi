# EleFi - Data safety, Goals, and polish

Build session, 2026-10-05. Follows [the planner thread](2026-10-05-planner-and-consistency.md).
Everything open afterwards is in [TODO.md](../TODO.md).

## 1. The briefs (verbatim, unedited)

> A couple of improvements...
> 1. For labels, Instead of an existing list of colors, I want a nice color picker, so that it gives me choices more than currently available.
> 2. If I am in Labels page or Deleted Transactions page, and press back, it closes the app... it should simply go back to settings page. So, overall in the app, whereever I have a page, pressing back should take me to its parent or earlier page, instead of closing the whole app.
> 3. In the Date pciker box and also Time picker box, while editing or capturing a transaction, the 3 buttons are not visible at all due to color (blue) they are in, keep them white maybe.
> 4. Also just like we are suggesting for "bought through" and "Paid with" textboxes, similarly also now suggest exactly simialrly, using history of what was entered for the particular "Paid to"... e.g. if "Paid to" is "Mom", and earlier, I had a transaction with her and notes was "Gift", then that note should be suggested.
> 5. the backup json export should contain every single thing! Plans, Goals, everything!. Even the trained messages as well. Make sure, nothing that the user is adding, is lost by export and restrore!  And while installing, do not lose my data as well! My data is very important, whatever is filled so far.
> 6. How does the "Teach Elefi a message" work? I mean, what happens exactly when I teach it something? If something is not programmed in, then how is it parsed after learnign?
> 7. Implement Goals page also.

> also implement on more feature btw- hiding amounts or not showing anything when in recent apps view...

> even if I am on "Quick" page, pressing back, closes the app, while it should go to the page, where it previously was

> Also do these improvements, once the crash issue is fixed, and make sure non of my data is lost!
> further improvements-
> 1. In "Quick" page, why is there a "Add other details" button on "Amount" and in "Paid From" steps? Not needed there, just have it in the last "Paid to" step. And it should be right after I press next in last step... ie. 3rd step... then it should just ask me "Add other details"... and a "Done" button, if clicked on "Done", then just record it with "needs review" tag... and if chosen former button then open the add transaction page with the already filled details so far. Use date and time, that was current while filling the transaction details.
> 2. Use the word "Suggestions" instead of "Matches" or "Most Used" everywhere, in all pages, where we are displaying or giving suggestions to choose from.
> 3. Also highlight the textboxes with a different color than dark purple border... for the "suggestions" boxes, those boxes should have a different but still aesthetic color border, and even filling inside too

## 2. What was found

| Problem | Cause |
|---|---|
| Back left the app from Labels, Deleted, Quick | The BlazorWebView's own back handler stepped the browser history while ours stepped as well: one gesture, two moves. One step from the dashboard, the second found nothing and left |
| App crashed on every launch mid-session | A stale incremental Android build: resource ids out of step with code after new resources. A clean build fixed it. Separately, restoring MAUI's saved fragment state after Android kills the app in the background crashes, so the activity now starts without it |
| Picker buttons unreadable | The theme's `colorAccent` was still the template's dark indigo |
| Backup missing data | It listed a few fields by hand: no plans, notices, taught rules, alert links, audit trail, deleted rows or timestamps |

## 3. Decisions

No questions were asked; these are the defaults taken, open to change.

| # | Question | Decision |
|---|---|---|
| 1 | How is "nothing lost" guaranteed? | Format 2 backup is written from the EF model: every table, row and column, deleted rows and the audit trail included. Enums by name, instants as ISO text. A test fills every table, exports to text, restores onto a fresh database, and compares every table; it fails if a table has no test data |
| 2 | Restore safety | One transaction: validate the whole file first (an unknown table or column means a newer app, refused), defer foreign keys, pause audit triggers, replace every table, commit. Any failure leaves the device as it was. Format 1 files still restore |
| 3 | Data across app updates | A Safety Copy before any schema change: SQLite's backup API through the keyed connection, same key, app-private storage, newest three kept. The Goals migration only adds tables and indexes; `Transactions.GoalId` gets an index rather than a foreign key, because a key would rebuild the ledger table |
| 4 | Goal progress sign | A Debit attributed to a goal takes progress down; a Self Transfer or Credit adds to it |
| 5 | Where goals live | The Goals half of the Plans tab. Contributions are attributed on the edit page, or recorded through "Add money to it", which opens the capture form as a transfer and attributes the result |
| 6 | Recent apps | "Hide in recent apps", on by default. Android 13 and later blank the card and keep screenshots working; older versions mark the window secure as it leaves the screen |
| 7 | Quick capture | Next on step 3 leads to Done (records, Needs Review) or Add other details (opens the full form filled in, saves nothing). Both use the moment the amount was first typed |
| 8 | Suggestions | Always captioned "Suggestions", in their own aqua-teal, distinct from the lavender chrome and the green of money in |
| 9 | Note suggestions | Offered once "Paid to" exactly names a known payee, from notes used with them either way the money went. Shown when the note is empty, without "will be added as new" |
| 10 | Colour picker | 24 ready-made colours, then hue, shade and vividness sliders, and a hex field, with a live preview of the label |

## 4. How Teach EleFi works (answer to brief item 6)

The user pastes a message and copies the values out of it: amount, and optionally who,
last four digits, date and note. EleFi finds each value in the message, keeps the bank's
fixed wording around them, and turns each value into a slot: the amount becomes "a number
like 1,234.56", the last four "four digits", the date "this shape of date", who and the note
"text up to the next fixed word". The result is a Parse Rule, a row of data (sender pattern
plus body pattern), never code. Before saving, the rule must read the pasted message back to
the same values. From then on every message from that sender is tried against it first,
ahead of the built-ins, under the same gates (the in-app switch, the sender, the OTP check).
The message itself is never stored (`SM1`); only the pattern is.

## 5. Final shape

Built, tested and committed. Follow-ups are under **Now** in [TODO.md](../TODO.md).
