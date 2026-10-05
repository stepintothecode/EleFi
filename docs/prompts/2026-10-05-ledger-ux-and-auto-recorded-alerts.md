# EleFi - ledger usability and auto-recorded alerts

Session of 2026-10-05, continuing
[2026-10-04](2026-10-04-capture-ux-and-payment-alerts.md). Produced
[ADR-0015](../adr/0015-record-alerts-as-needs-review.md), which supersedes `SM2`, and updates to
[CLAUDE.md](../../.claude/CLAUDE.md), [CONTEXT.md](../CONTEXT.md),
[domain-model.md](../requirements/domain-model.md) §11 (`SM17`),
[functional.md](../requirements/functional.md) FR-11.29-11.32, and [roadmap.md](../roadmap.md).

## 1. The original brief (verbatim, unedited)

> Next up:
>
> 1. In settings, the "Labels" list for editing them, should actually be a whole new page.. In settings, I want a "Edit Labels" button, which should open this page.
> 2. In "all transactions" page, I want that "Filters" opens a new popup page, doing things in same page, is not such a nice idea.
> 3. In "all transactions" page, in the "Filters" page, I want to see dates options like range also, like start and end date choices, (and even time selections for start and end of range). Also, just like existing options like "This month", "Last month", "This FY", "All time", also add "Today" and "This week". The container list in the filters page, should also be sorted similarly (category wise) instead of alphabetical... no need to mention the category names, just sort like that. And keep a horizontal line between each option for example between "Container" list and "Label".
> 3. In "all transactions" page, I also want that I should see the total... in the very end of the list, a hortizontal line, and then the total of whatever is in the list (where + and - are properly added, and self transfers are ignored) for example, by default show the total of all the transactions so far in the list. And when a filter is applied, then show the total of that much list, down below.
> 4. In "all transactions" page, if I click on any transaction, it opens it, but when I click back, i get back to "all transactions" page top portion, and even the filter is lost. So ensure that filters are not lost so drastically, and when back is pressed, the same page should open, exactly where I left... maybe even highlighting the transaction I came back from.
> 5. In "Home" page as well as in the "Containers" page, also sort the containers not in alphabetical order but in categorial order. Just like other pages.
> 6. In "Home" page, the "This month" text, make it a tab like button, and next to it, should be "Last month", if I click on it, then I should be taken to last month tab.. Third button should be "Select a range", and a popup should open asking similar date and time, to and from range just like "All transactions" page filter option. And once selected, that's total should be shown there.
> 7. In home page, and in containers page and in "All" page also... I want a toggleable "hide the amount" button. So here is what I want- a human eye like button, or elefi mascot's face, on the home page or settings page, which when clicked to toggle, should hide and unhide the amounts from every single page.. something like elefi mascot hiding its eyes with its ears, and then not hiding and being normal with eyes open, whenever this button is toggled. And then all the pages, get amounts hidden and unhidden with this.
> 8. The blue buttons with white text look weird, do a better and consistent color scheme throughout the app.. e.g. purple then stick ot it. But dark purple may not be so nice, go with lavendar type aesthetic color then.
> 9. Show the bank name and last 4 digits, (which were asked for certain containers) in the name (below the names, as very small text) this gives more clarity and practicality than just relying on the container names.
> 10. In settings page, "Your data" and "Restore from a file" have so much of text, just keep the buttons, and have an i button here also for the info details, which should open the popup modal.
> 11. I dont like the idea of payments to confirm in the settings page. just add the payment in the all transactions page, with "needs a review" tag, remove this whole "To confirm" kind of page. But, instead do this- keep a "train Elefi top parse certain messages", wher a user can copy paste a bank message for example, and then even tell the pattern to match to get the "To" "From" date, amoutn etc. details, if somehow elefi is not able to capture.
> 12. Always record the SMS parsed payments and event listened payments of GPay and other payment apps via notifications, record them under transactions with "needs a review" tag. Also, tell me what happens if I delete a transaction? Can I recover it? Because everything I mean every change is recorded in JIRA style... how about also giving an option to recover a deleted transaction? But a deleted transaction should not appear in the "All" page at all, it should go under "Settings"/"Deleted" page. Where a restore button next to each transaction should be present, and once restored, the transaction should be visible under "All" page again, with a "Needs a review" tag. And finally, a "restore all" button as well in this page, clicking which should restore all the deleted transactions back to "All" page with "needs a review" tag.
> 13. If a trnsaction is marked "needs review", then while editing it, the top part says so much of text, again just give me a button with "Looks right, clear the flag" button having an i info button on top which should open a popup modal with the explanation.

## 2. Decision log

`†` = superseded later.

| # | Question asked | Your answer |
|---|---|---|
| 1 | (From 2026-10-04) Keep `SM2`, a review inbox, or record alerts straight away? | † Keep `SM2`. Superseded by items 11 and 12 of this brief: record them as Needs Review and remove the inbox |
| 2 | What happens when a transaction is deleted? | Answered, not asked: it is a soft delete. The row stays, leaves every balance and list, and the audit trail records it. It could not be restored from the UI until this session |

### Decisions taken without asking, with the reason

| # | Decision | Why |
|---|---|---|
| 3 | `SM2` is superseded by a new ADR-0015, and CLAUDE.md's non-negotiable is rewritten to "recorded as Needs Review, never silently" | The brief reverses a documented non-negotiable; that needs a record, not a quiet code change |
| 4 | An alert with no matched container is recorded against: the app's default container, else the one last used with that app, else the first in picker order (a bank, not a card, for a card bill) | A transaction needs a Source; the Needs Review flag carries the doubt |
| 5 | An alert naming nobody is recorded against the payment app's name, or "Unknown payee" | Always recorded, as asked, rather than dropped |
| 6 | A second alert for a payment changes the transaction only while it still needs review | Once the user has reviewed it, their answer must not be overwritten by a late SMS |
| 7 | The notification says "recorded" and offers Open and Delete; Delete is soft | The payment is already in the ledger; Delete is the escape hatch for a message that was not really a payment |
| 8 | "Teach EleFi a message" learns a Parse Rule from one annotated example and checks it reads the example back before saving | The user asked to "tell the pattern"; asking for regex would not be usable, copying values out of the message is |
| 9 | A restored transaction is flagged Needs Review, one by one or all at once | As asked |
| 10 | Times in a range cut only the first and last day, and a transaction with no time is never cut | A transaction must not vanish from its own day for lack of a time |
| 11 | The list total is over everything the filter selects, not the rows loaded so far | Otherwise the total would change while scrolling |
| 12 | Hide amounts blurs amounts app-wide via one class on the shell, remembered between launches; toasts stay readable; tapping Ele toggles it and Ele covers its eyes | One switch no page can forget; a blurred error message would be worse than a brief amount |
| 13 | Lavender theme: lavender fills with dark ink text for main buttons; a deeper lavender for text, links and the active tab | Kept contrast readable in light and dark |
| 14 | The dashboard's banner now counts transactions needing review and links to the list filtered to them | Replaces the removed "to confirm" count |
| 15 | The Drive backup card also moved its text behind an "i" | Consistency with item 10 |

## 5. Final shape

Settled: all thirteen items. Alerts are recorded at once, flagged Needs Review, merged per
payment, with card bills as Self Transfers. Out of scope: the `SM1` analyzer, editing a
taught rule (forget and teach again), and device verification of the new notification text.
