# EleFi - TODO

Everything not yet built, in one place, so nothing is forgotten. The roadmap
([roadmap.md](roadmap.md)) says why and in what order; this file is the checklist. Tick an
item in the same commit that finishes it, and move it to **Done** at the bottom.

Priorities as of 2026-10-05: multi-currency is **low** priority.

## Now

- [ ] **Goals, the rest of S14**: goal choice in quick capture, goals in the audit trail
- [ ] **Planner leftovers**: an amount tolerance for matching (exact only today); verify a
      reminder firing, Mark done and Snooze on the phone with a real plan
- [ ] **Steady ledger leftovers (S42)**: the optional monthly balance check (S20); Ele asking
      for the tidy at the start of a week
- [ ] **Undo leftovers**: undo is per session (it does not survive closing the app); linking
      a phone contact and creating a contact have no undo
- [ ] **Notifications, the rest**: clear old read notices after a while, a notice for a plan
      that falls due
- [ ] One source for the app version: the project file says 0.1.0, About and Settings 0.3.0
- [ ] Settings > Safety copies: list the copies taken before each update, and restore one
- [ ] Note suggestions in quick capture's finish step
- [ ] **Contacts, the rest (S16)**: a contact's own history on its page (the triggers already
      record it); call, message and email buttons on a detail; remove an alias; refresh a
      phone-linked contact's number; "Contact" from the payee field on the capture form
- [ ] Merge duplicate Apps the way contacts merge (S16)
- [ ] Ele reacts to alerts recorded in the background, the next time the app is opened
- [ ] Verify on the phone: back on Quick with the keyboard up and down, the date and time
      picker buttons in light and dark, picking a phone contact

## v1.0 - daily driver

- [ ] **Encrypted Google Drive backup (S12)**: OAuth with `drive.appdata` only, passphrase,
      Argon2id, recovery code, AES-256-GCM, WorkManager upload, retention, restore
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

- [ ] Verify the SMS receiver on a real SMS, and payment-app capture on a real GPay,
      PhonePe, Paytm, Amazon Pay, PayZapp and CRED payment. CRED's notifications are turned
      off in Android settings on the test phone, so EleFi cannot see them until they are on
- [ ] Add real notification samples from each Payment App to the corpus tests (the rules
      were widened on 2026-10-12 from published wordings, not from captured samples)
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

## Done 2026-10-12

- [x] Ele is the only messenger; toasts and inline error banners removed; tones, sound
- [x] Undo and Redo arrows on every page that changes things
- [x] Planner: chosen weekdays, last day, end date or count, labels, skip, history, reminders
      with Mark done and Snooze, rescheduled after a restart
- [x] Still planned this month, goals summary and the counted streak on Home
- [x] Goal choice on the capture form; projected finish date
- [x] Containers listed only on their tab, opened by tapping; Home's tiles list their containers
- [x] Possible duplicates on their own page with Merge, Same and Different
- [x] PayZapp added to the payment apps read
