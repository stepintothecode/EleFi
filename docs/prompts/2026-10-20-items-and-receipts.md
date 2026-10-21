# EleFi - items, receipts and a smarter Ele, brief and decision log

Session of 2026-10-20 and 2026-10-21, after the six fixes
([2026-10-20-six-fixes.md](2026-10-20-six-fixes.md)). Decisions recorded in
[ADR-0017](../adr/0017-itemised-transactions.md) (items) and
[ADR-0018](../adr/0018-receipts-read-on-device.md) (receipts); requirements FR-2.22 to FR-2.26.

## 1. The original brief (verbatim, unedited)

> A bigger 2 features, which I want now-
> 1. itemisation
>  Itemising one transaction
>
> How it would work: a transaction could optionally hold items, each with an amount (integer minor units), labels and a note. The items must add up to the transaction's amount.
> Labels: with no items, labels stay on the whole transaction as today. With items, the spend breakdown uses each item's labels. Your ₹500 would go in as ₹200 Food and ₹300 Groceries, with no double counting.
> What doesn't change: balances, the transaction's kind, the Source and Destination, and the three-tap capture. Itemising would be an optional step when editing.
> Cost: the breakdown query and export have to understand items, and it needs a new ADR that supersedes part of 0013.
>
> So labels selection should be either for whole transaction (multiple labels chosable), and then, if any item is added, then labels (multiple chosable) for each item instead of the whole transaction. Under itemzation section for a transaction, there should be one item added if clicked on "Add item", and once an item is added, user should be able to give its name and its amount... And whenever any one item is added whose amount is lesser than the full transaction amount, then by default an item named "unnamed" should be added by default with the difference amount from the whole transaction amount and the total of added items. If the sum of all added items is equal to the whole transaction amount, then perfect, the unnamed item should then not exist in that list. And in case sum of all items exceeds the whole transaction's amount, then Ele should complain, and not let that item be even added with that amount.
>
> 2. Share a receipt to EleFi (one at a time)...
> So basically, each transaction can have one or more reciepts attached to it also now.
> This receipt can be of the following kinds-
> 2.1. GPay or any Payment App transaction details. From a GPay transaction's detail screen you share it to EleFi. For example, from the "Share" button on any transaction I should be able to press share and then select EleFi... On-device text recognition (ML Kit, offline) reads the payee, note and UPI reference and fills in the matching transaction.
> 2.2. Any shop/counter receipt... Reading the total, date and shop name on-device. Pulling out individual line items. OCR fills in suggested items, you correct them.
>
> Also, If one reciept's data is not matching the amount or payee or account as in the transaction, then the attachment should be flagged as a descrepency to solve.
>
> -------
>
> For all of the above changes, make sure that you take care of 3 things-
> 1. Code quality... i want good coding principles and guidelines to be followed. Nothing should be bloating this codebase. No god method or god class. Ensure everything is well tested for all kinds of scenarios. Code is very readable and congitive incomplex and optimized.
> 2. Performance should not degrade. time wise, this app should be fast. Data storage and query also. Everything should be optimal and optimized.
> 3. UI and UX... users should see this app as a nice aesthetic professional app, with everything clean.

## 2. Decision log

`⤵` = full text in §3.

| # | Question asked | Your answer |
|---|---|---|
| 1 | Do the three screens (edit with items, a GPay receipt with its match, a shop receipt's items) look right? | Yes, build these |
| 2 | Keep the receipt photo, or only what was read? | **Keep the photo**: compressed, inside the encrypted database, in every backup |
| 3 | A shared receipt that matches no transaction? | **Record it, Needs Review**, as an alert would be (ADR-0015) |
| 4 | Where do a transaction's labels go when its first item is added? | **Onto Unnamed**; removing the last item gives them back |
| 5 | The edit screen felt like one cluttered section ⤵. Card per section, or one card divided? | **S1, a card per section**: Amount, Who, Labels and items, When, Details, Receipts |
| 6 | Ele's face when something went wrong: sad, sad with a tear, or comforting? | **E3, comforting**: sorry eyes, a small kind smile, trunk a little raised |
| 7 | Where Ele lives when there is something to tell you: docked, in the middle, or both? | **B3, both**: in the middle for whole-screen moments, docked in flow, staying until you act |
| 8 | Commit and push this batch before the Ele presence work? | **Commit and push** |

Choices made without asking, against the brief:

| # | Question | Decision |
|---|---|---|
| 5a | Receipt opened from one transaction but plainly another's? | Say so, "This receipt looks like L V Iyengar Bakery", and offer to attach it there |
| 5b | Which tab is lit on the receipt screen? | All when opened from a transaction, Capture when shared in ⤵ |
| 5c | Does a matching receipt get a "Matches" tag? | No: agreeing is expected; only a disagreement is flagged ⤵ |
| 6 | Does removing an item need its own soft delete? | No: items are part of the transaction, so removing one is an edit, in the history and undoable. Recorded in ADR-0017 |
| 7 | A first item that takes the whole amount? | It takes the transaction's labels itself, since there is no Unnamed to hold them |
| 8 | Can a Self Transfer be itemised? | No (`T6`); changing an itemised transaction into one is refused until the items go |
| 9 | ML Kit, bundled or downloaded? | Bundled Latin model: offline from the first receipt, about 4 MB more per CPU |
| 10 | ML Kit's usage metrics to Google? | Removed in the manifest, so nothing leaves the phone, not even metrics |
| 11 | Where do photos live? | Their own table in the SQLCipher database, never app files, never read by lists |
| 12 | Is a discrepancy stored? | No, derived each time; only "kept as it is" is stored |
| 13 | How is a receipt matched? | Within three days: amount 4, payee 3, same day 2, time within half an hour 1; five or more matches |
| 14 | Receipt columns read as separate blocks? | Rejoined into printed rows by their positions before reading |

## 3. Verbatim answers you typed

Row 5:

> the items section and the above "paid to" section are too much cluterred, like it appears as same section rn. Can you create a proper separation between them, and even between each serction if posssible

Rows 5b and 5c, and two asks for next:

> Also, why is the "Capture" tab not highlighted as opened for all this? It looks like, I cannot tell which page I am in currently while attaching a reciept

> Also, if a receipt matches, then no need to mention "matches", it is obvious. Only if does not, then flag it...

> also give an  option to download the reciept offline if needed... so that I save or share that particular reciept whenever I want

> And again, always have Ele on screen guiding, flagging, helping, around. Instead of text inside the page most of the time. Those times, of course, Ele should not disappear after 2-3 seconds, such cases, Ele should stay until user does something like Trying again, rescanning or resolving conflict whatever. I want Ele to be there, always... Ele is the mascot, and should feel like Ele a living being is in the app, always helping the user. I will also in future have a "Talk to Ele" button and page... so that users can just ask anything to Ele and it will use its AI/ML brain to guide the user, or just to answer them even.

Sent during the work, on what Ele should become (recorded for the next plan, not built here):

> also see, my vision is that this app is overall smart... should be smart. That is, for duplicate items or trnasactions or Ele should flag them. For any mismatch, Ele should flag. there should be some sort of ML and AI used for sure in Ele. It should not be just a preprogrammed character Ele with certain rules inside it. Ele should be more, someone in the app who is learning locally as more and more data is coming in

> my phone will keep on getting off in 10 minutes screen timeout. So, keep it on, till you are done with installing and testing on phone..

## 4. What changed mid-session

| Before | After | Why |
|---|---|---|
| A matching receipt tagged "Matches" | No tag; only disagreements flagged | The user: agreeing is obvious |
| The receipt screen lit no tab | All or Capture, by where it was opened | The user could not tell which page they were on |

## 4b. Found testing on the phone with a real GPay receipt

- Sharing a link the app could not read crashed EleFi; it now says it could not read it.
- Text recognition drops the ₹ sign and writes the account as "HDFC Bank 8411"; the reader now
  takes a bare amount line, "Payment of 55", and a plain last four. That receipt is a corpus test.
- The recognition result needed a Java cast, and bitmaps were released across threads; both fixed.
- The picker on this phone asks for Done even for one photo; that is the phone, not EleFi.

## 5. Final shape

Settled: items on a transaction, adding up through an automatic Unnamed remainder; receipts
shared in or photographed, read on the device, matched, attached with their photo, and
discrepancies flagged to settle; the edit screen in section cards.

Next, promised: a plan for **Ele learning on the device** (labels predicted from history,
duplicate transactions flagged, unusual amounts), with its own ADR. Corrections made on the
receipt screen are the first training signal it will use.
