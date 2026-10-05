# EleFi - TODO

Everything not yet built, in one place, so nothing is forgotten. The roadmap
([roadmap.md](roadmap.md)) says why and in what order; this file is the checklist. Tick an
item in the same commit that finishes it, and move it to **Done** at the bottom.

Priorities as of 2026-10-05: multi-currency is **low** priority.

## Now

- [ ] **Planner, the rest of S41**
  - [ ] Weekly on chosen days, last day of the month, an end date or a count for repeats
  - [ ] Reminder notification on the due date with "Mark done" and "Snooze to tomorrow"
  - [ ] Labels on a plan, carried into the transaction it records
  - [ ] "Still planned this month" on the dashboard
  - [ ] Plans in the audit trail
  - [ ] Skip an occurrence of a repeating plan
- [ ] **Steady ledger streak (S42)**: count clean weeks, freezes, milestones, Ele's mood. The
      chip and its explanation exist; nothing is counted yet
- [ ] **Goals, the rest of S14**: goal choice on the capture form and quick capture, goal
      progress on the dashboard (FR-5.8), projected completion date (FR-6.13), goals in the
      audit trail
- [ ] **Undo, the rest of S8**: undo for "Looks right", label changes from the Labels page,
      and container edits
- [ ] **Notifications, the rest**: clear old read notices after a while, a notice for a plan
      that falls due
- [ ] One source for the app version: the project file says 0.1.0, About and Settings 0.3.0
- [ ] Settings > Safety copies: list the copies taken before each update, and restore one
- [ ] Note suggestions in quick capture's finish step

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
      PhonePe, Paytm, Amazon Pay and CRED payment
- [ ] Add real notification samples from each Payment App to the corpus tests
- [ ] Built-in rules for credit-card spend SMS ("spent on Card xx1234 at ...") for the
      major issuers
- [ ] **Parse Rule management (S39)**: edit a taught rule; enable or disable built-in rules
- [ ] The `SM1` analyzer that makes a message body unable to leave the receiver
- [ ] NFR-10.8: test visible failure on an OEM-skinned device that kills background work
- [ ] Notification capture with inline reply and top labels (S22)

## v1.1 - tidy

- [ ] **Merge and tidy (S16)**: merge duplicate parties and apps; maturity and card due-date
      reminders

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
