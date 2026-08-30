---
status: accepted
date: 2026-08-29
---

# Free forever, MIT licensed, funded only by voluntary support

Every feature of EleFi is free. There is no paid tier, no unlock, no trial, no supporter
build, and no feature that is withheld pending payment. The app is MIT licensed. The only
money path is a link to a shared support page that gives nothing in return.

This supersedes the free-versus-paid split that FR-7.13 described, in which filtered CSV
export was to be the paid feature.

We chose this because the product's entire claim is that the user owns their data and the
app is not working against them. A money tracker that withholds a way of getting your own
numbers out contradicts that claim in the one place it is most visible, and no amount of
privacy engineering elsewhere compensates for it. Charging for the *filtering* was the worst
version of the idea: it is precisely the thing that makes the export useful.

The wider reason is that a paid tier is not free to have. It brings payment infrastructure,
GST registration and invoicing in India, refund handling, store billing rules if the app is
ever listed, and a support obligation to the people who paid. For a single-developer project
whose largest risk is stalling before daily use, that is a large tax on a small revenue.

## Considered options

- **Paid filtered export** (the FR-7.13 position). Rejected above.
- **Free core, paid convenience features later** - cloud sync, attachments, richer reports.
  Defensible, and it avoids the portability objection. Rejected because it still drags in
  every obligation listed above, and because it distorts the roadmap: features get chosen
  for whether they can be sold rather than whether they are needed.
- **Donation with perks** - a supporter badge, priority requests, early builds. Rejected,
  and this is the sharpest line here. The moment a payment buys anything it stops being a
  tip and becomes a sale, which is what pulls in GST, store payment rules, and a duty to
  the payer. The perk is never worth what it costs to have created an obligation.
- **Ads, or any analytics-funded model.** Rejected outright. It would require an egress path
  for user data or attention, which NFR-5.1 forbids and which ADR-0001 exists to prevent.

## How the support link works

It follows the pattern already used across the other projects, and deliberately does not
invent a second one:

- The link points at `https://stepintothecode.github.io/support/`, always with a `?from=`
  naming the surface it sits on. Payment providers change; that URL does not, so switching
  provider never means shipping a new build.
- One `from` per **surface**, not per project, so it stays visible which link people
  actually press. EleFi uses `elefi-app` for the in-app link and `elefi-repo` for the
  README.
- A matching entry goes in `assets/projects.js` in the support repository, or the page
  falls back to generic wording.
- `.github/FUNDING.yml` gets the native Sponsor button on the repository page at no cost.

## Consequences

- **FR-7.13 is superseded by FR-7.17**, kept struck through in place per the requirement
  ID-stability rule. Filtered CSV export ships in v1.0, free.
- **Roadmap slice S30 loses its billing half** and becomes optional Play distribution work.
- **NFR-11.6 (GST registration before charging) becomes inapplicable** while this ADR
  stands. It is kept rather than deleted, because reversing this decision would make it live
  again, and a reader needs to see that consequence attached to the reversal.
- **The in-app link must open the system browser, not the app's own WebView.** Under Blazor
  Hybrid a plain `<a href>` navigates the host `BlazorWebView` and strands the user inside
  the app. It goes through `Browser.OpenAsync(url, BrowserLaunchMode.External)`, and Android
  needs the `https` intent query declared in the manifest or the call fails silently. This
  is also the rule that keeps a future store listing out of trouble, since a payment page
  inside an app web view reads as billing that skipped the store.
- **The wording next to the link is load-bearing, not decoration.** It says plainly that the
  payment buys nothing. That sentence is what makes the "no perks" rule true in writing
  rather than just in intent.
- **No analytics on the link.** `?from=` is read by the support page and discarded. EleFi
  learns nothing about who clicked, which is consistent with NFR-5.8 and is the only
  behaviour compatible with having no egress path.
- **MIT licensing means someone can fork and sell it.** Accepted without concern: the value
  here is the running habit and the data on the device, neither of which forks.
