# EleFi - capture usability pass and payment alert capture

`/mattpocock-skills:implement` session, 2026-10-04. Produced
[ADR-0014](../adr/0014-payment-app-notification-ingest.md) and updates to
[CONTEXT.md](../CONTEXT.md), [domain-model.md](../requirements/domain-model.md) §11
(`SM12`-`SM16`), [functional.md](../requirements/functional.md) FR-11.25 and FR-11.27-11.29,
and [roadmap.md](../roadmap.md) (S38, S40).

## 1. The original brief (verbatim, unedited)

> Now continuing the implementation of the Elefi app...
> I want first few improvements-
>
> 1. When I capture a transaction, in the "Paid from", since it is a drop down, it is fine but I would prefer a organized drop down instead of an alphatbetically sorted list, e.g. all credit cards, then bank accounts, then others.
> 2. When I capture a transaction, in the "Paid to", the drop down is terrible right now... After adding more than 5 transactions, this list has become so huge that I am unable to even type, it just lays over my keyboard, and acroos the whoile screen, which is annoying. So I would prefer this- I get top 3 most frequent "paid to" suggested above the text box, with a highlighting around the suggestion list (so that it differs from the rest UI behind), and then once I start typing, it should go through the entire history and keep suggesting suggestions (atleast top3). Currently it does not give a list of entire history, Idk what its logic is tbh, but I would like suggestions based on whatever I am typing.
> 3. When I capture a transaction, in the "Labels", I would prefer a left to right scrollable list, instead of all cluttering the view  top to bottom. This also, sort as per the top freuently used in history.
> 4. When I capture a transaction, in the "Bought through" and "Paid with" textboxes, the long explnation text can be little  improved. Maybe with an "i" info button on top of both the lines "Bought through" and on "Paid with", which should open a small modal explaining what it means. Let's keep minimal text on the main pages.
> 5. In the Containers page, The "edit" button and "Add" button open a form in the same page on top, while it could just open a popup modal with a close button on top of it for the same.
> 6. In the "quick" page, The "From" and "To" pages, have agaain everything wierdly placed, keep the "From" organized just like the "Capture" page, and "To" should have a top 3 suggestions and scraping through entire history just like "Capture" page.
> 7. In the "quick" page, also there should be a button "Add other details", which should save the so far filled data, and also immediately open the "Capture"/"edit" page of that transaction
> 8. Back button in general... I would appreciate if the back button can just go to the previous state of app, instead of closing the whole app. For example, if I am on "Quick" page, and I press back, it should just take me back to home for example.
>
> And then also implement this big feature-
> 1. SMS parsing, so that whatever SMS is being recieved on this device, my app should parse it and record a "To be reviewed" transaction.
> 2. SMS may not have all kinds of info and data, so When I pay on a payment app like GPay, or Amazon Pay, or CRED, then immediately that transaction should get recorded into my app, with the exact details as the original transaction. And then should be marked as "To be reviewed". If something comes from SMS parsing then complete its data/info (like name of "Paid to" and even the notes) from the particular app that was used to pay it.
>
> Implement some really good solution!
>
> --------
>
> Do all this while maintaining good code quality, following SOLID principles, and testing every unit testable component. Having good test suite. Keeping performance and nice UI and UX designs.

## 2. Decision log

| # | Question asked | Your answer |
|---|---|---|
| 1 | The brief asks SMS to "record a To be reviewed transaction", but `SM2` (ADR-0010, a CLAUDE.md non-negotiable) forbids a parsed SMS becoming a transaction without explicit confirmation. Keep `SM2` (a review inbox of Capture Suggestions, one tap to add) or supersede it (auto-book as Needs Review)? | **Keep `SM2`: review inbox.** Detected payments land in an inbox plus a notification; nothing touches balances until confirmed |
| 2 | GPay, Amazon Pay and CRED have no public API; the only on-device route is a `NotificationListenerService` (roadmap S40, needs its own ADR). Proceed with an allow-listed listener? | **Yes, notification listener + ADR.** Allow-list GPay, PhonePe, Paytm, Amazon Pay, CRED; its data enriches a matching SMS suggestion or creates its own |

### Decisions taken without asking, with the reason

| # | Decision | Why |
|---|---|---|
| 3 | The inbox is titled **To confirm**, not "To be reviewed" | "Review" is already the Needs Review flag on real transactions; reusing the word would blur the exact line answer 1 kept. Recorded in CONTEXT.md as *Suggestion Inbox* |
| 4 | Merge an SMS and an app alert only on identical amount, currency and direction, from different channels, within 15 minutes; at most one alert per channel | Two genuine ₹20 payments must never collapse into one. See `SM14` |
| 5 | On a merge, the container comes only from the SMS `last4`; the app's payee name replaces the SMS's | `SM11` forbids inferring a container; the app's name is the one the user saw |
| 6 | Both capture switches off by default; payment-app switch opens the system notification-access screen | A reading feature has to be an offer (FR-11.24); Android has no runtime prompt for notification access |
| 7 | Receivers parse in-process (`goAsync`), not via WorkManager as S38 planned | WorkManager would persist the message text, which `SM1` forbids |
| 8 | Prompt notifications offer Review and Dismiss, not Add | Adding needs the container and labels checked in the inbox |
| 9 | Quick capture "Add other details" saves and opens the editor only when both ends exist; otherwise opens the capture form pre-filled, saving nothing | A transaction cannot exist without a counterparty, and inventing "Unknown" would be a guess |
| 10 | Back: sheets and quick-capture steps close first, then the previous screen, then Home; only Home leaves the app (backgrounded, not finished). Reaching Home clears the history | Matches Android convention; otherwise back from Home would reopen a just-cancelled screen |
| 11 | Within a container group the user's own sort order is kept; groups are Credit cards, Bank accounts, Cash and wallets, Savings and investments | The request was grouping, not a new sort |
| 12 | Typeahead ranks by closeness of match first (exact, prefix, word prefix, contains, letters-in-order for 3+ characters), then usage, then recency; idle shows the 3 most used, typing shows up to 5 | "At least top 3"; usage must not outrank the name being typed |
| 13 | The same typeahead replaces every `<datalist>`, including Bought through and Paid with (which show nothing until typing starts) | The datalist overlay was the bug in item 2, and it existed on those fields too |
| 14 | After review: a credit-card bill paid through a Payment App becomes a **Self Transfer** suggestion, and the inbox asks which card was paid | Read as a Debit it would double-count the card's purchases, breaking the Self Transfer non-negotiable (`SM16`) |
| 15 | After review: notifications saying failed, pending, declined, reversed, refunded or requested produce nothing; a late alert for a payment already added is absorbed, not re-offered | A wrong suggestion costs trust, and offering a confirmed payment again invites a duplicate |
| 16 | After review: an app alert's time of day is kept on the transaction; an SMS's is not | The app's notification is raised as the payment completes; an SMS's timestamp is when the bank noticed |

## 5. Final shape

Settled: all eight usability items; SMS and Payment App alerts become Capture Suggestions in
a "To confirm" inbox, merged per payment, with `SM2` intact. Payment App wording is best
effort and needs real-device samples. Out of scope this session: the `SM1` analyzer, the
NFR-10.8 OEM-device test, Parse Rule management UI (S39), and one-tap Add from the
notification.
