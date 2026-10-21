---
status: accepted
date: 2026-10-20
---

# Receipts are shared into EleFi, read on the device, and kept in the encrypted database

A transaction can have **receipts** attached: a payment app's transaction screen (GPay,
PhonePe, Paytm and the like) or a shop's printed receipt. A receipt arrives by sharing it to
EleFi from another app, or by picking a photo from the transaction itself. EleFi reads it on
the device with ML Kit's bundled text recognition, finds the transaction it belongs to,
attaches it, and flags a **discrepancy** when the receipt and the transaction disagree on the
amount, the payee or the account. A shop receipt's line items can become the transaction's
items ([ADR-0017](0017-itemised-transactions.md)).

The photo is kept, compressed, inside the SQLCipher database, in a table of its own.

## Why

The user asked, on 2026-10-20:

> each transaction can have one or more reciepts attached to it also now. [...] From a GPay
> transaction's detail screen you share it to EleFi. [...] On-device text recognition (ML
> Kit, offline) reads the payee, note and UPI reference and fills in the matching
> transaction. [...] Any shop/counter receipt... Reading the total, date and shop name
> on-device. Pulling out individual line items. [...] If one reciept's data is not matching
> the amount or payee or account as in the transaction, then the attachment should be flagged
> as a descrepency to solve.

They chose, when asked: keep the photo; a receipt that matches nothing is recorded as a new
transaction flagged Needs Review, the way an alert is ([ADR-0015](0015-record-alerts-as-needs-review.md)).

## Considered options

- **Cloud OCR.** Better on crumpled paper. Rejected outright: it sends a picture of the user's
  spending to a server, which is the egress this app exists to avoid.
- **ML Kit, unbundled** (the model downloaded by Google Play services). A smaller APK.
  Rejected: the first receipt would wait on a download, and it fails on a phone without Play
  services or offline.
- **ML Kit, bundled Latin model.** About 4 MB per CPU architecture, works offline from the
  first launch. Chosen.
- **Photos as files in app storage.** Simple, cheap to read. Rejected: app files are not
  encrypted, and a receipt shows what was bought, where, and often a card's last digits.
- **Photos as blobs in the encrypted database, in their own table.** Chosen. Encrypted with
  everything else, and the backup already carries blobs (base64), so receipts are backed up and
  restored with no new mechanism.
- **Keep only what was read.** Smallest. Offered and declined: the user wants to look at the
  receipt again.

## Consequences

- **New tables**: `Receipts` (what was read, which transaction, when a discrepancy was settled)
  and `ReceiptImages` (the JPEG and a small thumbnail, keyed by receipt). Transaction queries
  never touch `ReceiptImages`; the edit screen loads thumbnails, and the full photo loads only
  when opened. Invariants `RC1` to `RC6` in the
  [domain model](../requirements/domain-model.md#receipts-adr-0018).
- **Discrepancies are derived, not stored.** `ReceiptCheck` compares a receipt with its
  transaction every time it is shown, so correcting the transaction clears the flag by itself.
  Only "keep it as it is" is stored, as the moment it was settled.
- **Attaching a receipt that disagrees flags the transaction Needs Review**, so it surfaces
  where every other thing to check does, and Ele says what differs.
- **Reading is heuristic, and says so.** Payment app screens follow a few known shapes; shop
  receipts are read for a total, a date, a name at the top, and lines that end in an amount.
  Every read value is shown for correction before anything is attached, and a shop's items are
  offered ticked, never applied unseen. The rules live in `ReceiptReader` with a corpus test
  per shape, like parse rules.
- **No new egress.** ML Kit can send anonymous usage metrics to Google through its data
  transport. The Android manifest removes that transport, so nothing leaves the phone. No
  image or text is ever sent anywhere.
- **The APK grows** by the bundled model.
- **Photos grow the backup.** Each is scaled to at most 1600 px and compressed (around 150 KB),
  so a few hundred receipts add tens of megabytes to the JSON backup.
- **Ele learns from it later.** Corrections to what was read are the training signal the
  "Ele learns locally" plan will use; nothing here learns yet.
