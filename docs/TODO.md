# EleFi - TODO

Everything not yet built, in one place, so nothing is forgotten. The roadmap
([roadmap.md](roadmap.md)) says why and in what order; this file is the checklist. Tick an
item in the same commit that finishes it, and move it to **Done** at the bottom.

Priorities as of 2026-10-12: multi-currency is **low** priority. Last updated 2026-10-12,
after the Ele, undo and Planner session; the build with everything below ticked is on the
test phone.

## Next up, in order

1. [ ] **Check on the phone, with real use** (needs the user, nothing to build):
   - a plan due in a few minutes: the reminder fires, Mark done opens it, Snooze moves it
   - each of Ele's three moods and sounds (steps in
     [functional.md, Ele's moods](requirements/functional.md#eles-moods))
   - the next real GPay, PhonePe, Paytm, Amazon Pay, PayZapp and CRED payment is recorded
     (CRED's notifications are off in Android settings on the test phone: turn them on)
   - back on Quick with the keyboard up and down; the date and time picker buttons in light
     and dark; picking a phone contact
2. [ ] **Encrypted Google Drive backup (S12)**: the one v1.0 item left that protects the data
3. [ ] **Settings > Safety copies**: list the copies taken before each update, and restore one
4. [ ] **Goals, the rest of S14**: goal choice in quick capture, goals in the audit trail
5. [ ] **Ele reacts to alerts recorded in the background**, the next time the app is opened
6. [ ] **One source for the app version**: the project file says 0.1.0, About and Settings 0.3.0
7. [ ] **Real payment-app samples in the corpus tests**, from the notifications in step 1

## Later, not ordered

- [ ] **Planner leftovers**: an amount tolerance for matching (exact only today); a notice in
      Notifications when a plan falls due
- [ ] **Steady ledger leftovers (S42)**: the optional monthly balance check (S20); Ele asking
      for the tidy at the start of a week
- [ ] **Undo leftovers**: undo is per session (it does not survive closing the app); linking
      a phone contact and creating a contact have no undo
- [ ] **Notifications, the rest**: clear old read notices after a while
- [ ] Note suggestions in quick capture's finish step
- [ ] **Contacts, the rest (S16)**: a contact's own history on its page (the triggers already
      record it); call, message and email buttons on a detail; remove an alias; refresh a
      phone-linked contact's number; "Contact" from the payee field on the capture form
- [ ] Merge duplicate Apps the way contacts merge (S16)
- [ ] Home's period tabs: a range's times are applied but not shown in the tab ("10-12 Aug");
      consider showing them in the sheet's title

## v1.0 - daily driver

- [ ] **Encrypted Google Drive backup (S12)**, Next up step 2: OAuth with `drive.appdata` only,
      passphrase, Argon2id, recovery code, AES-256-GCM, WorkManager upload, retention, restore
- [ ] **Security and settings (S11)**: biometric lock, auto-lock delay,
      home currency, cycle start day, theme choice
- [ ] **Dashboard (S9)**: drill-down from a label to its transactions, income versus spend
- [ ] **Design system and motion (S10)**: shared motion module, shared-element transitions,
      haptics, reduced-motion pass, real mascot art (Rive)
- [ ] **Onboarding and hardening (S13)**: first-run flow, empty and error states,
      accessibility pass, 50,000-row benchmarks, OEM-device matrix, signed GitHub releases
- [ ] **Foundation leftovers (S1)**: measure cold start on the reference device; first real
      run of the release pipeline

## SMS and payment apps

- [ ] Verify the SMS receiver on a real SMS (payment apps: see Next up, step 1)
- [ ] Built-in rules for credit-card spend SMS ("spent on Card xx1234 at ...") for the
      major issuers
- [ ] **Parse Rule management (S39)**: edit a taught rule; enable or disable built-in rules
- [ ] The `SM1` analyzer that makes a message body unable to leave the receiver
- [ ] NFR-10.8: test visible failure on an OEM-skinned device that kills background work
- [ ] Notification capture with inline reply and top labels (S22)

## v1.1 - tidy

- [ ] **Merge and tidy (S16)**: maturity and card due-date reminders (contacts done 2026-10-11)

## v1.2 - reporting (multi-currency is low priority)

- [ ] **Richer reporting (S19)**
- [ ] **Reconciliation (S20)**: enter a real balance, see the difference, balance it
- [ ] FX rates (S17), low priority
- [ ] Multi-currency UI (S18), low priority

## v2.0 and later

- [ ] Smart defaults and a swipeable review queue (S21)
- [ ] Month in review (S25)
- [ ] Web build (S26)
- [ ] Floating bubble (S27), iOS (S28), multi-device sync (S29), Play distribution (S30)
- [ ] Transaction status, pending and scheduled (S31)
- [ ] Budgets per label (S33), attachments (S34), voice capture (S35), statement import (S36)

## Done recently

- [x] **2026-10-12, Ele and undo**: Ele is the only messenger (toasts and inline error banners
      gone); three moods, each with its own face, movement and sound (a short elephant
      trumpet, a low hum, a grumpy snort); a newer message pushes the old one down and out;
      Settings quiets the sound; every mood and what brings it is documented in functional.md
- [x] **2026-10-12, Undo and Redo** arrows in the top bar of every page that changes things,
      one history per kind of change; on Goals beside New goal, as on Plans
- [x] **2026-10-12, Planner (S41) complete**: chosen weekdays, last day of the month, end date
      or count, labels carried into the transaction, skip, history, reminders with Mark done
      and Snooze to tomorrow, rebuilt at start and after a restart
- [x] **2026-10-12, Home**: Today tab added left of This month (still shown first), four equal
      tabs with "Range"; Liquid, Locked and Owed list their containers; still planned this
      month; goals summary; the Steady ledger streak counted (S42) with freezes and milestones
- [x] **2026-10-12, Goals**: "Counts toward" on the capture form; projected finish date
- [x] **2026-10-12, Containers** listed once, on their tab; a row opens its sheet, with Archive
      and Delete inside; a paid-off card shows "nothing owed", never a red ₹0.00
- [x] **2026-10-12, Contacts**: possible duplicates on their own page behind a counted button,
      with Merge, Same and Different explained by an "i"; Different is calm
- [x] **2026-10-12, payment apps**: PayZapp read; rules widened to published wordings
- [x] Contacts (ADR-0016): list, add by name or from phone contacts, details, notes, links,
      likely duplicates, merge with aliases and undo, rename keeping the old name,
      transactions per contact; all in the backup, 2026-10-11
- [x] Ele pops up for a few seconds when a transaction is recorded, edited, deleted, restored
      or confirmed; taps go through; a cross and a Settings switch, 2026-10-11
- [x] History shows names, money, dates and words instead of ids and numbers, 2026-10-11
- [x] Alerts take their arrival time; teaching takes an account name when there are no last
      four digits, and the sender's bank picks the account otherwise (SM18, SM19), 2026-10-11
- [x] Fixed: back on Quick closed the app (the focused WebView handled back itself); date and
      time picker buttons invisible (Material draws them in colorPrimary); Mark all read did
      nothing (bulk update skipped the change tracker; the same bug in deleting a goal),
      2026-10-11
- [x] "Recorded" and "Updated" instead of "Saved"; In and Out on lines of their own, 2026-10-11

- [x] Goals (S14): both funding modes, progress against a straight line to the date, On
      track / Behind / Achieved, the monthly amount still needed, archive, over-allocation
      warning, attributing a transaction from the edit page or "Add money to it", 2026-10-05
- [x] The JSON backup holds everything: every table, row and column, deleted rows and the
      audit trail included, with a test that fails if a table is ever left out, 2026-10-05
- [x] A safety copy of the database, encrypted with the same key, before any update changes
      its schema, 2026-10-05
- [x] Back steps to the previous screen everywhere; no more crash after Android closes the
      app in the background, 2026-10-05
- [x] Hide in recent apps (part of S11), 2026-10-05
- [x] Label colour picker: 24 ready-made colours, hue, shade and vividness sliders, hex,
      2026-10-05
- [x] Note suggestions from what was written before with the same payee, 2026-10-05
- [x] Quick capture asks Done or Add other details after the last step, and keeps the
      moment it was started, 2026-10-05
- [x] "Suggestions" everywhere, in their own colour; readable date and time picker buttons,
      2026-10-05

- [x] Notifications page behind a bell on Home: full text, tap to mark read or unread, Mark
      all read or unread, 2026-10-05
- [x] Steady ledger chip on Home with its explanation (placeholder, S42), 2026-10-05
- [x] Undo straight after editing or deleting a transaction (S8), 2026-10-05
- [x] Planner, first cut (S41): plans with repeats, completion that links or records a
      transaction, automatic completion from SMS and payment apps, Completed list, 2026-10-05
- [x] Loading screen with Ele, 2026-10-05
- [x] Deleted transactions with Restore and Restore all, 2026-10-05
- [x] Alerts recorded as Needs Review, Teach EleFi a message (ADR-0015), 2026-10-05
- [x] Hide amounts (privacy mode, part of S16), 2026-10-05
- [x] SMS receiver and Payment App capture (S38, S40), 2026-10-04
