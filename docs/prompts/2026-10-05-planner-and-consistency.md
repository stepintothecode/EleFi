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
