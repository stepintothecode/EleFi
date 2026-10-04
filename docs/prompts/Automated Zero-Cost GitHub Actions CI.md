ROLE

Act as a Principal DevOps Engineer and Mobile CI/CD Specialist. You specialize in zero-cost build pipelines, automated releases, and secure Android deployment workflows using GitHub Actions.

GOAL

Provide a complete, production-ready, zero-maintenance GitHub Actions workflow that automatically compiles the .NET MAUI Android project, signs the APK, generates release assets, and publishes them to GitHub Releases whenever a version tag is pushed.

CONTEXT

Project: EleFi (open-source personal finance app).

Framework: .NET MAUI Blazor Hybrid targeting Android.

Distribution Strategy: Direct sideloading via GitHub Releases and compatibility with tools like Obtainium and IzzyOnDroid. No Google Play Console involvement.

Budget: $0. Must run entirely within GitHub's free runners for public open-source repositories.

Key Needs: Automatic semantic release generation, Android keystore signing via repository secrets, and artifact attachment (EleFi-vX.X.X.apk).

ACTION

Deliver a turnkey setup guide and complete workflow YAML divided into the following sections:

Keystore Generation & GitHub Secrets Setup:

Provide the exact keytool terminal command to generate a production Android keystore file.

Explain how to convert the keystore into a Base64 string for safe storage in GitHub Secrets.

List the exact secret keys to configure in GitHub Repository Settings (e.g., KEYSTORE_BASE64, KEYSTORE_PASSWORD, KEY_ALIAS, etc.).

GitHub Actions Workflow File (.github/workflows/release.yml):

Write the complete workflow that:

Triggers strictly on semantic version tags (e.g., v*.*.*).

Sets up the latest Ubuntu runner, Java (JDK 17), and .NET SDK with the Android MAUI workload.

Decodes the signing keystore on the fly.

Builds and packages a release-ready, signed .apk file using dotnet publish.

Extracts the tag name, creates an official GitHub Release, and attaches the .apk directly as a downloadable release asset.

Securely cleans up the decoded keystore file after the build step.

Testing & Verification Steps:

Provide a 3-step checklist to test the pipeline by tagging a commit locally and verifying that the resulting release installs cleanly on a physical Android device.