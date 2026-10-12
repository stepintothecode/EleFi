# EleFi - Contacts, Ele's reactions, and ten fixes

Build session, 2026-10-11. Follows [data safety, goals and polish](2026-10-05-data-safety-goals-and-polish.md).
Produced [ADR-0016](../adr/0016-contacts-merge-with-aliases.md), FR-13 and FR-8.10, FR-11.33-34 in
[functional.md](../requirements/functional.md), `CT1`-`CT4`, `SM18`, `SM19` in
[domain-model.md](../requirements/domain-model.md). Open work is in [TODO.md](../TODO.md).

## 1. The briefs (verbatim, unedited)

> Few more improvements /fixes now:
> 1. A new feature: Contacts list... Even merging or linking contacts... Or cleaning up the duplicates. So just like a list of all deleted transactions can be accessed, also have a list of all the contacts so far. Even link a "Contact" from phone's contact if possible. Or if not, then just add phone numbers, email, address, etc. details, and notes about a person. Also linking between contacts, for example, one contact "is same as" some other contact, or just another account of same entity.
> 2. Press back on quick, closes the app
> 3. In history, it shows ids instead of the content
> 4. When teaching a message, if last 4 digits not present then parse atleast as per the name
> 5. Color scheme for the Date and Time buttons like Set, Clear etc... the buttons are not at all visible rn
> 6. Use current time from sms parsing, right now no time is recorded
> 7. "Saved" after saving a transaction is misleading as it confuses with "saving that amount" as in saving or debiting while it means "saved this transaction"
> 8. In all transactions page, the total In and total Out can be in new lines instead of one line
> 9. "Mark all read" button in notifications is not working
> 10. Add some good Ele animations for example when a transaction is added/removed/updated. But do not keep Ele on top of everything, let Ele come and show emotion and speak, while floating/bouncing, but all the app should be usable while it happens. Also, there should be an 'x' button to close/remove Ele from screen even though the animation would be about 3-5 seconds max.
>
> Follow good coding guidelines, design patterns, principles, and follow correct a TDD approach, have tests for every feature and its scenarios. Update the docs nicely.
> And after fixing all the above items, tell me what todos and next feature lists or bugs are remaning in the app now.

> Btw, my phone is also plugged in, so install and test... Make sure to not delete any data in the app on my phone, as all of it is really important and production data. And ensure the backup and also restore features take care of all new data like contacts as well. Everything a user fills, should be backed up and restored

## 2. What was found

| Item | Cause |
|---|---|
| 2. Back on Quick left the app | The BlazorWebView handles the back *key* in `OnKeyDown` whenever it has focus. Quick focuses its amount field, so one press stepped the WebView's history to the dashboard and then reached our back handler, which found nothing behind the dashboard. The 2026-10-05 fix only covered presses that reach the activity |
| 3. Ids in history | The triggers store foreign keys and enum numbers; the screen printed them as they were |
| 5. Picker buttons invisible | Material draws framework dialog buttons in `colorPrimary`, which this app uses as the window ground. The earlier `colorAccent` fix never reached them |
| 9. Mark all read | `ExecuteUpdate` skips the change tracker; the page re-listed through the same context and got the stale instances back. Deleting a goal had the same bug |

## 3. Decisions

No questions were asked; these are the defaults taken, open to change.

| # | Question | Decision |
|---|---|---|
| 1 | What is a contact? | The external Party transactions already name, not a new entity ([ADR-0016](../adr/0016-contacts-merge-with-aliases.md)) |
| 2 | Merge or link? | Both. Merge folds one into the other and keeps its name as an **alias**, so alerts stop recreating it; Undo straight after. Link keeps both: Same as, Another account of. A third answer, Not the same, silences a duplicate suggestion |
| 3 | Phone contacts | The system picker for one phone number, no `READ_CONTACTS`. Copied in, not kept in step; "Open" shows it in the phone's Contacts |
| 4 | Duplicates | Suggested, never automatic: same name written differently, one letter out (a swap counts as one), one name inside another |
| 5 | Rename | Can keep the old name as an alias, on by default |
| 6 | Item 4, "as per the name" | Read as the account's name. Teaching takes "Account name" when there are no digits; failing that, the sender ID's bank. Exactly one container must fit (`SM18`) |
| 7 | Item 6, which time | Arrival time, for SMS too, except when the message dates the payment to another day: then no time rather than an invented one (`SM19`). Reverses the 2026-10-04 choice not to keep an SMS's time |
| 8 | Item 7, wording | "Recorded" for new transactions (buttons too: "Record", "Record and add another"); "Transaction updated", "Plan updated", "Goal updated" for edits |
| 9 | Item 10, Ele | Arrives at the right above the tab bar, hops for good news, shakes for a delete, bobs while speaking, leaves after 4 s. Taps go through everywhere except its cross. Above the page, below toasts and sheets. A Settings switch turns it off |
| 10 | Installing on the phone | Update in place only, never uninstall or clear. Built the same way as the installed copy (Release, local debug key, certificate checked first). The app's own safety copy is taken before the migration |
| 11 | Backup of contacts | Already covered: the backup is written from the database model. The round-trip test now fills contacts, details, links, notes and aliases |

## 4. What changed mid-session

| Before | After | Why |
|---|---|---|
| An SMS's time was not kept (2026-10-04: "only when the bank noticed") | Arrival time kept unless the message names another day | Item 6 |
| `container_id` only from last four digits (`SM11`) | Also from the account name or the sender's bank, when exactly one fits (`SM18`) | Item 4 |
| P4 covered live parties | P4 covers aliases too | A merge has to stick |

## 5. Final shape

All ten items built with tests. Not built: a contact's own history, call or email buttons,
merging Apps, and anything needing a person at the phone to confirm (see TODO.md).
