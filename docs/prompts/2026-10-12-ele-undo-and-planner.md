# Ele as the only messenger, undo everywhere, and the rest of the Planner - brief and decision log

Session of 2026-10-11 to 2026-10-12, a second batch of thirteen improvements after
[the contacts session](2026-10-11-contacts-ele-and-fixes.md). Produced changes to
[CONTEXT.md](../CONTEXT.md) (Undo, Ele, Skip, Reminder, Freeze, Pace, Projected Finish),
[functional.md](../requirements/functional.md) (FR-14, and FR-4.15, FR-5, FR-6, FR-13 rows),
[domain-model.md](../requirements/domain-model.md) (Plan, `P1`-`P5`, `S1`-`S3`),
[roadmap.md](../roadmap.md) (S41 complete, S42 built) and [TODO.md](../TODO.md).

## 1. The original brief (verbatim, unedited)

> Improve:
> 1. In Contacts, The "Possible duplicates" should be a new page, not on the top of that same contacts list page. So, once Contacts page is open, then have a "Possible Duplicates" button on top neat "New" button... maybe the button can be some icon instead of the text "Possible Duplicates" but it should open a new page, with the list of duplicates.
> 2. In that duplicates list, each flagged duplciate should have 3 buttons simpler- "Merge", "Same", and "Different". And have a small "i" button on top in this page, which should open a new popup modal with x button of its own, which explains what all the 3 buttons mean, and how they will exactly impact the contacts.
> 3. Why are things now two times notified. e.g. Ele pops up on screen to say, "Recorded" or "Updated", but then why is the older label also coming on the bottom saying, "Transaction updated" or whatever with an elephant emoji. That is bad. Only have Ele popup animation only. And for the "Undo" button, dont have it there but now keep the Undo button (and a Redo button), on the very top near "Filters" button & near the "Close" button when a transaction is opened, with their arrow icons... they should be clickable if an undo or redo roparation is possible, else, the buttons should be non clickable. Not only the transactions page, have this undo/redo button thing in almost every single page, where it makes sense.
> 4. Also have Ele for everything now, even for contacts getting linked or merged. Or whatever. Have only the Ele popup animations with text, and nothing else. Do not have the popup label thing to notify any data records now. Remove them in every page and every feature,  Replace with Ele animations only. For example, when a requried field is not given, it comes in red right now as a popup asking to fill that field, but make Ele come and tell what the problem is.
> 5. And if Ele is already there, but some button was clicked meanwhile such that Ele pops up again, then the preivious Ele should drag a little downwards and fade away, while the new Ele animation comes in that place.
> 6. Dont make Ele's face angry when a transaction is deleted. Keep it calm, not happy either. Just calm. But for cases like required fields not filled, or any other critical places, Ele can have the angry face, like it currently does for deleting a transaction.
> 7. Introduce a sound for Ele... just a simple tone or music for now... just liike Duolingo does. Something like an elephant's trumpet maybe, or something very calming.
> 8. Yes the home needs a little clean up, for example, I dont know why but the same list of containers can be found in Home as well as in the Containers page. I want this- exactly how it is dispolayed currently on the Home page, should be the way the list of containers is displuaed now in the Containers page. And only when clicked on one, I can edit that container... no need for an "Edit" or "Delete" button at all. Keep Delete button also inside the Edit popup modal page.
> 9. Home page should be having the "This month", blah blah the summary dhasboard, which is good already. But then, the goals maybe. And when clicked on "Liquid" or "Locked" or "Owed" I should see the details on why that amount in that. This can be anotehr popup modal view just for shoewing what list of accounts were used to calcuate that amount.
> 10. Implement- checking real GPay, PhonePe, Paytm, Amazon Pay, Payzapp, and CRED notifications
> 11. Impleemnt- Goals: choosing a goal from capture,, a projected finish date
> 12. Implement- Steady ledger streak
> 13. Implement- Everything remaining in Planner

## 2. Decision log

`⤵` = full text in §3.

| # | Question | Decision |
|---|---|---|
| 1 | Where do duplicates go? | Their own page, `settings/contacts/duplicates`, behind a two-person icon beside New that shows how many wait |
| 2 | What do the three answers do? | **Merge** folds one into the other, keeping the old name as an alias; **Same** links them and moves nothing; **Different** changes nothing and stops the pair being suggested. All explained in the "i" sheet, all undoable |
| 3 | Toasts, or Ele, or both? | Ele only. ToastService and ToastHost removed; inline red error banners in sheets removed too |
| 4 | Where does undo live? | Undo and Redo arrows in each page's top bar: beside Filters, beside Close on a transaction, beside Add or New elsewhere. One history per kind of change, thirty steps, for the session. Greyed out when there is nothing to do |
| 5 | How does an undo run? | Through the same service call as the change, so it is checked and audited; a failure is said by Ele and drops that step |
| 6 | Can Ele be switched off? | No: it is now the only messenger. The Settings switch became "Ele's sound" |
| 7 | Ele's moods | Pleased hops; **calm** (deletion, undo) only floats; **cross** shakes its head and is used for problems. A new message pushes the old one down and out; expiry and the cross leave the same way |
| 8 | The sound | Synthesised on the device with Web Audio, one per face: a short jolly elephant trumpet for pleased, a soft low hum for calm, a grumpy snort for cross. No audio file shipped ⤵ |
| 9 | Containers list | One list, on the Containers tab, in the style Home had. A row opens its sheet; Archive and Delete are inside it. Home no longer lists containers |
| 10 | Liquid, Locked, Owed | Each tile opens a sheet listing the containers that make it up, using one domain rule (`BalanceMath.PartOf`) shared with the totals |
| 11 | Real payment-app notifications | Android keeps no history of past notifications, so no real samples could be read from the phone. Rules widened to published wordings, PayZapp added to the apps read. CRED's notifications are turned off in the phone's settings, so EleFi cannot see them. Real-sample corpus tests remain a TODO |
| 12 | Goals | "Counts toward" on the capture form; a goal's **pace** is the last 90 days of contributions, or the planned monthly amount before any; the projected finish is shown on the goal and on Home |
| 13 | Steady ledger | Counted, derived on every read; freezes (one per four clean weeks, at most two); Ele celebrates 4, 12, 26 and 52 weeks once each |
| 14 | Planner | Chosen weekdays, last day of the month, end date or count, labels carried into the transaction, skip, history, still planned this month on Home, and reminders with Mark done and Snooze to tomorrow, rebuilt at start and after a restart |
| 15 | Goals tab undo arrows | Moved from the panel's intro line to the page's top bar beside New goal, the same place as on Plans ⤵ |

## 3. Verbatim answers you typed

Raised mid-session, after seeing the arrows on the Goals tab:

> Why undo/redo buttons below the top bar in case of "Goals" page, it should be same as Plans page, on top near "New" button

> Are the Undo/Redo in Capture and in ALl Transactions page, supposed to do the same? If yes, then I am fine, but if not, then fix it. For example, I clicked on UNdo in Capture page, and a transaction I just deleted got restrored, is this expected?

Answered: yes. Every transaction page shares one history, so Undo anywhere takes back the
latest change to any transaction. Splitting it per page was offered and not taken.

> For Ele's sound, Keep one of them, like the jolly one as an elephant's short trumpet maybe. And keep different sounds for different emotions/faces

> Hope the each mood appearance is documented well in some or other md files too.

Answered with the table of moods, triggers and test steps in [functional.md, Ele's moods](../requirements/functional.md#eles-moods).

> Wait, also in the home page, just like "This month", "Last month", and "select a range" options, also add "Today" tab button but on the very left of "Next month". And then keep "This month" as default opened just as it is currently.

Read as "left of This month", since there is no Next month tab.

> No, keep al;all 4 equal sized. Maybe keep the text as short as possible e.g.  "Range" instead of "Select a Range"

> Also, when I click on "Owed" and in that list of containers, the 0.00 Rs is still shown in red color

> update the TODOs also so far... and next remaining items. With all the changes so far.Keep tythe enxt item list up to date before commiting and pushing

## 4. What changed mid-session

| Before | After | Why |
|---|---|---|
| Ele could be switched off in Settings | Only Ele's sound can be switched off | With no toasts, switching Ele off would silence every confirmation and error |
| Ele left on a fixed CSS timer | Expiry, the cross and replacement all use the same slide-down fade | A problem stays five seconds, so a timer tied to four seconds hid it early |
| Goals' undo arrows in the panel | In the Plans page's top bar beside New goal | Asked for, to match Plans |
| A paid-off card showed a red ₹0.00 "owed" | Shows "nothing owed", not red | Seen on the phone while checking the new list |
| Pleased was two brassy notes, calm a bell, cross two falling notes | A real elephant trumpet, a low hum, a snort | Asked for: the jolly one as a trumpet, and a sound per face |
| Marking a duplicate pair Different made Ele pleased | Calm | Found while documenting every mood: Different changes nothing, so it is not a win |
| Home's period tabs sized to their words | Four equal tabs; "Select a range" became "Range", and a chosen range shows as "10-12 Aug" | Asked for equal sizes, with shorter words to fit |
| The Owed sheet coloured every card's amount | Only an amount actually owed is in the owed colour | ₹0.00 was still red there |
| TODO had one flat "Now" list | An ordered "Next up" list, then "Later, not ordered" | Asked for the next items to be kept current |

## 5. Final shape

Settled: Ele is the only voice, every changing page has undo and redo, the Planner and the
streak are complete, goals project a finish date. Out of scope here: undo across app
restarts, goal choice in quick capture, a matching tolerance for plans, and real-sample
tests for the payment apps.
