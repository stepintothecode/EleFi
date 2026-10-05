# EleFi - Planner and a consistency streak

Planning conversation, 2026-10-05. Produced roadmap entries
[S41 Planner and S42 Steady ledger](../roadmap.md#s41--planner).

## 1. The original brief (verbatim, unedited)

> list me all the features still remaining so far. And also add a new item to the feature list-
>
> - Planner... I want a tab/page dedicated to plannig... where I can add tasks just like Microsoft Tasks but with some money/amount involved... and this I want exactly like Microsoft's Tasks, i.e. even option to set something as recurring... And once marked done with a tick button on left corner of a task, it should be sent in the "Completed" list below the page. Also the items should be date wise sorted by default. The idea is I can have something like  a plan for a transaction already, suppose I have multiple mosnthly SIPs and somehow the SMS may or may not ber cpming, so I want to have once creatred the recurring task for each SIP such that it links to a transaction or even adds a new transaction once I mark it done, if SMS parsing faioled or no SMS came, maybe. Think about it properly, how it should be like, implement something practical and nice and helping... not exactly what I say.
>
> -------
>
> how can a streak like feature work in this app? I cannot imply users to adddd a transaction daily, that is not practical... and also just opening and closing is not so engaging... so what can I do enable some duolingo like consitency behavviour?

## 2. Decision log

| # | Question | Decision (proposed, pending the user's go-ahead) |
|---|---|---|
| 1 | Is a plan a transaction? | No. A Plan never touches a balance until done, so derived balances and the export are untouched |
| 2 | What does ticking a plan do? | Links a matching existing transaction (amount, container, within days of due), offers a choice when several match, or opens a pre-filled capture form when none do. "Done without a transaction" for non-money tasks |
| 3 | What if the SMS does arrive? | The alert's transaction ticks and links the matching open plan automatically |
| 4 | When does a repeat roll forward? | On completion, not on the calendar, so a missed month stays overdue. Skipping is explicit |
| 5 | Where does S32 (recurring transactions) go? | Absorbed into the Planner; nothing books itself on a schedule |
| 6 | What unit does a streak count? | A week, kept when nothing from that week still needs review and no plan due that week is open. Zero transactions still counts |
| 7 | Rewards? | Ele's mood and milestones only, with earned freezes. No unlocks (ADR-0012) |

## 5. Final shape

Both written into the roadmap as S41 (Planner) and S42 (Steady ledger, proposed). Neither
is built yet.

## 6. Second brief: build it (verbatim, unedited)

> multi currency things are nmot so high priority. Have all of the TODOs in some md file, so that nothing gets forgetted. I'd like to do these first- in the opening scren... Ele's happy face.. flaoting... greeting... Add the new page... "Goals" and "Planning"... Streak thne appears on the home page.. somewhere on top, clicking on which should open a new popup modla explaining about it. No need to implement everything bnut have the UI placeholders atleast. But it will be nice if you could implement the Planning thing... Audit and undo (S8): history and restore exist; undo straight after an edit doesn't.

> also add a notification bell button on top near the streak maybe. Should open a new dedicated page. To read all the notifications, full length, make them read or unread by tapping on one. And a "Mark all read/unread" button on top...

## 7. Decisions for the build

No questions were asked; these are the defaults taken, open to change.

| # | Question | Decision |
|---|---|---|
| 8 | Where do the leftovers live? | `docs/TODO.md`, one checklist, ticked in the commit that finishes an item. Multi-currency (S17, S18) marked low priority |
| 9 | Where does Plans sit? | A tab after Home. Goals is a second segment on the same page, a placeholder for now |
| 10 | Decisions 1 to 4 above | Taken as proposed and built: Plan is its own table, repeats roll forward on completion in one series, a match is the same amount within 4 days from the same container, and an alert's transaction ticks a matching open plan |
| 11 | Ticking with exactly one match | Linked at once, with Undo on the toast. Several matches or none open a sheet: pick one, record it now through the capture form, or done without a transaction |
| 12 | Streak | A chip on Home reading "0 weeks", tapping explains the rule in decision 6. Nothing counted until S42 |
| 13 | Undo | Saving, marking reviewed and deleting each show a toast with Undo for 6 seconds. Undo of an edit replays the snapshot through the same checked edit path. Undo of a delete restores without the review flag, since nothing happened in between |
| 14 | Notifications | A Notice table, one per transaction, rewritten and made unread when a second alert for the same payment arrives. Holds the prompt text only, never the message (`SM1`). The bell shows the unread count |
| 15 | Opening screen | Ele floats mid-screen with a greeting by time of day, before the app loads |

## 8. Final shape

Built and committed. What is still open is in [TODO.md](../TODO.md) under **Now**.
