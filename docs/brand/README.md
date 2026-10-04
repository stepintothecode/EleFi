# Brand assets

Ele, the elephant, and the colours around him.

## The source of truth is two SVGs

```
src/EleFi.App/Resources/AppIcon/appicon.svg      background layer, a flat purple field
src/EleFi.App/Resources/AppIcon/appiconfg.svg    foreground layer, Ele himself
src/EleFi.App/Resources/Splash/splash.svg        launch screen, Ele alone
```

**Edit those, not the PNGs.** MAUI rasterises every Android density at build time from the
SVGs, so changing one file changes every size at once. The PNGs in `android/` are exported
copies, kept here so the sizes are visible without a build.

## What a build generates

| Density | Icon size | Where it is used |
|---|---|---|
| mdpi | 48 px | Baseline, oldest and lowest-density devices |
| hdpi | 72 px | |
| xhdpi | 96 px | |
| xxhdpi | 144 px | Most current phones |
| xxxhdpi | 192 px | High-density flagships |

Four images per density: `appicon` (the composite most launchers show), `appicon_round`
(round-mask launchers), and `appicon_foreground` plus `appicon_background` (the two layers
Android's adaptive icons animate independently).

## The safe zone, and why Ele is shifted up

Android crops an adaptive icon's outer third: only the middle 66% is guaranteed visible,
and each launcher picks its own mask, circle or squircle or rounded square.

Ele is drawn head-first, so the shapes span roughly y 154 to 432 on the 512 grid and his
visual centre is y 293, not 256. Left alone, a circular mask crops around 256 and he sits
low with his trunk clipped. `appiconfg.svg` therefore lifts the drawing by 34 units so the
visual centre lands on the canvas centre.

`ForegroundScale="0.85"` in the csproj then sizes him to fill that safe zone without the
ears touching the crop edge.

## A single image, for anything that wants one

[`elefi-icon.svg`](elefi-icon.svg) is both layers combined, with a squircle clip. Rasterise
it at any size, including the 512 a store listing asks for:

```
inkscape -w 512 -h 512 elefi-icon.svg -o elefi-icon-512.png
magick -background none -density 600 elefi-icon.svg -resize 512x512 elefi-icon-512.png
```

It is not part of the build. If you change the two layers, change this too.

## Colours

| Token | Value | Used for |
|---|---|---|
| Ground, top | `#6E5A96` | Icon background gradient start |
| Ground, bottom | `#4A3E6B` | Icon background gradient end, and the splash colour |
| Hide, light | `#EFEAF6` | Ele's head and ears, top of the gradient |
| Hide, shadow | `#CFC6DF` | Bottom of the same gradient |
| Inner ear | `#B9A9D0` to `#9F8CBC` | The darker ear insets |
| Eye | `#3B3154` | Eyes and brow lines |

The in-app accent (`--ele-accent` in `app.css`) is a cooler, greyer blue than the icon
ground on purpose: an icon competes with a launcher full of saturated colour, a UI does not.

## Ele in the app

The mascot on the dashboard is not this artwork. It is drawn on a canvas by
[`elefi-mascot.js`](../../src/EleFi.App/wwwroot/js/elefi-mascot.js) so he can breathe,
blink, and react, behind `IMascotService`. Same character, different medium: this file is
for the launcher, that one is for the screen.
