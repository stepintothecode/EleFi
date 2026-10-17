# Ele's new look and Ele's guide - brief and decision log

Session of 2026-10-17. Produced the new Ele drawing (`src/EleFi.App/wwwroot/js/elefi-ele-shape.js`,
drawn live by `elefi-mascot.js` and written into every still picture by
`docs/brand/generate.mjs`), Ele's guide (`EleGuide`, `EleGuideOverlay`), and changes to
[CONTEXT.md](../CONTEXT.md) (Ele, Ele's guide) and [functional.md](../requirements/functional.md)
(what Ele looks like, every face, FR-14.10).

## 1. The original brief (verbatim, unedited)

> 2 improvements-
> 1. Face of Ele- why is there a mouth? Looks less of an elephant and more of some human-elephant mix. Fix it to make it look more realistic, have the trunk, and maybe tusk... And update everywhere like all the pages.. even in about.
> 2. For new users, have Ele guide them through the whole app... And have a button in Settings page for a quick guide by Ele. Do it all with minimal words but all steps should have Ele describe the feature and 2 buttons- skip the whole guide button somewhere on top, and previous or next buttons near Ele throughout the whole guide. It should look like an overlay on top of the app display, and Ele should be above guiding through each major feature. For example, adding containers is the most important step for any user.

## 2. Decision log

`†` = superseded later. `⤵` = full text in §3.

| # | Question | Decision |
|---|---|---|
| 1 | Where is Ele drawn? | In one place: a shape module of path data. The live canvas Ele draws from it, and a generator writes the app icon, splash, start-up vector and brand icon from it, so every Ele is the same elephant |
| 2 | First redesign | A realistic elephant, no mouth: domed head, long trunk with wrinkles, tusks, big lobed ears † |
| 3 | Second | Cute: big eyes with lashes, blush, pink ears † ⤵ "looks girlish" |
| 4 | Third | Duo-style big white eyes, no brows, lids carry the mood, muted lavender † ⤵ "dull and sad" |
| 5 | Final look | Bright violet, round head, big close-set eyes with large pupils and two sparkles, puffed cheeks, round ears held out, short chubby trunk curling up at rest, small tusks, no mouth, no brows |
| 6 | Pleased trunk | After four rounds of options: raised up the right side of the face to eye level, lighter ridged underside along the inner edge, the end turned to face the front with both nostrils ⤵ |
| 7 | Hiding amounts | Option M: both big ears flop down over the top of the face from its edges, full size ⤵ |
| 8 | Hiding motion | A timed, eased 0.6 s fold; the open ears hand over to the folding ones early, and a tap mid-way reverses from where they are ⤵ |
| 9 | Pleased extra | A burst of confetti around Ele's head, skipped when the phone asks for reduced motion ⤵ |
| 10 | Process | Show images and ask before building or installing anything ⤵ |
| 11 | The guide | Eight steps over the real screens: hello, containers first, record, Quick, the list, plans and goals, automatic capture, Home. One sentence each. Skip at the top, Previous and Next under Ele, back steps back |
| 12 | When it starts | On its own once, for a new user with no containers; Settings > Help > Quick guide by Ele any time. The owner's phone has containers, so it never interrupts |
| 13 | Icon size | Ele drawn about 15% larger in the icon only, since the build scales the foreground to 85% |

## 3. Verbatim answers you typed

> Oh, and even the app's logo and images.. whereever you can find Ele

> wait a minute, Ele should look friendly and cute... make her eyes bigger maybe... redo the faces

> even the phone is connected now btw... Just make sure to not lose any data

> And wait a minute, Ele's face is still looking wierd. It looks girlish... For reference, take Duolingo, how they have an owl as mascot Duo but look at his face, Duo is so cute yet nice. You dont have to be relaistic, just make Ele cute and relaistic with facial featues placement, that's all I said

> Yeah, first generate images of Ele, confirm from me, if they are good or not, and only then proceed

> No, this looks ugly. Try again... Looks dull and sad Idk why

> This is better. But for the Hiding amounts one, have proper big ears over the eyes, instead of current small dark purple circles. And also for the please one, can the trunk be raised maybe? Just give me an updated one and ask me as a questionare to decide

Answers to the questions that followed:

> Curl up more in front, like a real elephant would do

> F is the closest because the ears start from the edges of the face, but still can be made more bigger since the open ears do look big

> Can you please curl the trunk right in front, like just curl it upwards and Ele should still face in front... let the trunk's bottom be visible in a lighter shade of trunk's color

> M: F, but bigger

> Pleased needs work. The trunk is now facing front, which is awesome but make it a little higher.. Near to the eyes, or maybe even above the eyes, near to forehead. Give me options to select from. Others are perfect

> All look very awkward. Here is what I want now.. make the trunk go a little left or right... just a little, not too much like earlier.  and let it be over the eye(s). Now give me options.

> The trunk is too straight like a cucumber. Have some curve. Option 1 is fine but the trunk's curve should be shown

> You really misunderstood what I meant by curve the trunk. It now looks like a dick. I want the trunk to go from bottom to top on one side, while showing a little part of its lighter shade below. Just draw nicely

> Option 1 but just also have the trunk's holes shown, and the trunk's end facing front now. That's it

> The animation when the "hide" button is toggled, should be smooth, currently it is not smooth enough.
> And also, have some confetti coming up near Ele for the pleased emotion

> 1. In the quick guide, Skipp button should be not transparent, it is overlapping with the text below.
> Also, have Ele sounds in the quick guide...
> No need to mention "This guide is in the Settings" in last step.
> And say "Tap me on home page to hide amounts" in last step.
> For every place when there is a full stop, start next line on a new line, instead of directly after that full stop. I am talking about the quick guide steps.
>
> 2. Streak ledger has so much of text, clean it up, be to the point and also not let the user have any confusion. Explain the concept in minimal words.
>
> 3. If hide mode is on, and I switch from any other page to home page, the animation of Ele hiding with ears happens again, why? If hide mode is on, Ele should be already hiding, right?
>
> Make all these changes, then ammend in last commit and push force

## 4. What changed mid-session

| Before | After | Why |
|---|---|---|
| Realistic elephant, long hanging trunk | Cute, big eyes, lashes, blush | Asked for friendly and cute |
| Lashes, blush, pink ears | Duo-style eyes, no brows | Looked girlish |
| Muted lavender, drooping ears, hanging trunk | Bright violet, perky ears, trunk curling up | Looked dull and sad |
| Pleased: trunk curled in front, then rolled up in front | Raised up one side, underside showing, end facing front | Rolled shapes read badly; the user described the pose |
| Hiding: small ears over each eye | Big ears flopped over from the face's edges | Asked for proper big ears |
| Hiding: exponential ease, ears shrink while new ones appear | Timed eased fold, one motion | Not smooth enough |
| Every page opening Ele replayed her mood, so Home with amounts hidden covered up again | The mood a viewer opens in is a starting pose; only later changes animate | Asked: hiding should already be hiding |
| Guide Skip see-through, last step pointed at Settings, sentences run together, silent | Solid Skip, "Tap me on the home page to hide amounts", one sentence per line, Ele's sound per step | Asked |
| Streak sheet: three paragraphs | The rule in one line, this week, a legend, freezes and the next milestone | Too much text |

## 5. Final shape

Settled: one drawing for every Ele, the bright cute design, every face as in functional.md,
the guide. Not done here: Ele's guide in Quick capture's own layout, and real art in Rive (S10).
