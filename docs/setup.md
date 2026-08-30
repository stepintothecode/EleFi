# EleFi - Development setup

Everything needed to go from a clean clone to the app running on a phone. Anything that
cannot be scripted belongs here rather than in someone's memory
([software-development.md](requirements/software-development.md) §9).

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0 | Pinned by `global.json`; `dotnet --version` must match |
| MAUI workload | `maui-android` | `dotnet workload install maui-android` |
| JDK | 17 | Newer JDKs are not supported by the Android build tooling |
| Android SDK | API 28 minimum, current target | Easiest via Android Studio's SDK Manager |
| IDE | Visual Studio 2026, Rider, or VS Code + C# Dev Kit | Any of the three works |

A **physical mid-range Android device** is required, not optional. Cold start (NFR-1.1),
motion quality (NFR-2), OEM background-process behaviour (NFR-10.3), and the SMS receiver
all behave differently on an emulator, and in each case the emulator is more forgiving than
reality.

## First build

```
git clone git@github.com:menonkartikeya/EleFi.git
cd EleFi
dotnet restore
dotnet build
dotnet test
```

`dotnet test` passes on a clean clone **with no device attached**. Domain, Application,
Ui, Architecture, and Properties test projects all target plain `net10.0` and have no
platform dependency. That is deliberate: if it stops being true, something has leaked
across a layer boundary and `EleFi.Architecture.Tests` should have caught it.

Only `src/EleFi.App` targets `net10.0-android`, and only it needs the Android SDK.

## Running on a device

```
adb devices                                        confirm the phone is listed
dotnet build src/EleFi.App -t:Run -f net10.0-android
```

## Google Drive backup (optional locally)

Backup needs an OAuth client. Without one, everything except backup and restore works, so
this can be skipped until you need S12.

1. Create a project in the Google Cloud console.
2. Enable the **Google Drive API**.
3. Configure the OAuth consent screen. Request **only** the `drive.appdata` scope -
   broader Drive scopes are restricted by Google and forbidden by
   [ADR-0006](adr/0006-google-drive-encrypted-backup.md).
4. Create an **Android** OAuth client ID, using the app's package name and the SHA-1 of
   your debug signing certificate:
   ```
   keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
   ```
5. Put the client ID in `appsettings.Development.json`, which is gitignored.

The Android OAuth client has no secret, which is why the flow is authorization code with
PKCE. Nothing confidential is shipped in the app.

## Testing SMS parsing without a SIM

The parser is a pure function, so most work needs no device at all - write a corpus test in
`EleFi.Domain.Tests` and iterate in milliseconds.

To exercise the real receiver end to end:

```
adb emu sms send VM-HDFCBK "Rs.450.00 debited from a/c XX4417 on 29-Aug-26 to ZOMATO. Avl bal Rs.12,300.00"
```

On a physical device, use an SMS-sending app or a second phone; `adb emu` is emulator-only.

**Do not paste real bank messages into the test corpus.** Redact amounts and identifiers
first - the corpus is checked into a public repository, and a real alert carries an account
suffix and a balance.

## Seeding the performance dataset

Benchmarks are meaningless on twenty rows. The seeder builds the NFR-1 dataset: 50,000
transactions, 40 containers, 5,000 parties, 200 labels, 300 apps, across ten years.

```
dotnet run --project tools/EleFi.Seed -- --transactions 50000
```

## Adding a package

Versions live in `Directory.Packages.props`, never in a `.csproj`:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="Some.Package" Version="1.2.3" />
```

```xml
<!-- the .csproj that needs it -->
<PackageReference Include="Some.Package" />
```

A `Version` attribute on a `PackageReference` fails the build (NU1008), which is the point.

A package with **network access** needs an ADR first (NFR-5.11). The current list is one
entry long: `Google.Apis.Drive.v3`.

## Troubleshooting

**`SQLite Error 26: file is not a database`** - the SQLCipher key was not supplied before
the connection was used. An unkeyed connection against an encrypted file reports corruption
rather than a wrong password, so this error almost always means a key ordering bug, not a
damaged file.

**Migrations fail on startup after a restore** - the backup is from a newer schema version
than the installed app. This is refused deliberately (FR-9.12). Install the newer build.

**The SMS receiver stops firing after a few hours** - an OEM battery manager has killed it
(NFR-10.3). Reproduce it on the OEM device rather than working around it on a Pixel, and
make sure the failure is visible to the user (NFR-10.8).

**`dotnet workload` reports a manifest mismatch** - the SDK and the workload have drifted.
`dotnet workload update`, then confirm `dotnet --version` still matches `global.json`.

**`NU1010: does not define a corresponding PackageVersion`** - a `PackageReference` was
added without a matching `PackageVersion` in `Directory.Packages.props`. See above.

**`NU1902` or `NU1903` fails the build** - a dependency has a known vulnerability at high
severity or worse. Do not suppress it. Either update the package, or pin the offending
transitive dependency in `Directory.Packages.props` with a comment saying why and when the
pin can go.

**`CS0118: 'Application' is a namespace but is used like a type`** - inside `EleFi.App`,
the bare name `Application` binds to the `EleFi.Application` namespace rather than MAUI's
type. Write `Microsoft.Maui.Controls.Application` in full.

**`CS1591: Missing XML comment`** - a public member in `src/` has no doc comment. That is a
build error by design; the comment is where the invariant goes.
