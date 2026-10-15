# Catholic Companion API

ASP.NET Core 10 API serving daily Mass readings and a seven-day US liturgical calendar from USCCB.

## Run locally

Install the .NET 10 SDK before running the commands below.

```powershell
dotnet restore --configfile NuGet.Config
dotnet run --launch-profile http
```

API: http://localhost:5067. Swagger: http://localhost:5067/swagger. Health: GET `/health` (process health, not an upstream USCCB check). Run the sibling UI with `npm start`; its development proxy removes `/api` before forwarding requests.

## Endpoints

Both existing POST routes accept `{ "date": "2026-10-14" }`. Existing ISO timestamp clients remain supported; the supplied calendar day is retained. Missing, malformed, or unsupported dates produce an HTTP 400 validation response. Supported request years are 1900 through 2100; upstream readings may not exist for all dates.

- `/DailyUpdates/DailyReadings`: compatible first-reading, psalm, second-reading, acclamation, and Gospel fields, plus `readingSets`. Each set has celebration, title, lectionary, source note, source URL, ordered sections, and reading options with references and plain text. Same-day linked Mass pages are included; a failed set does not discard working sets.
- `/DailyUpdates/LiturgicalDate`: seven ordered celebration names and matching calendar dates, with an optional error when some days are unavailable.
- `/DailyUpdates/SaintOfTheDay`: date, saints listed by Vatican News, optional short summaries, source links, and an optional error. Uses the supplied calendar date, with successful month/day source data cached for six hours. Entries without biographies retain their names and source links. Source failures do not produce invented entries.

The existing response fields and production origin remain compatible. Source outages return safe error messages and links rather than exception details. Calendar failures use a placeholder in the affected position so subsequent days stay aligned.

## Implementation

`UsccbClient` uses a typed HttpClient with a 15-second timeout and a 24-hour in-memory cache of successful HTML responses. Each parse uses a fresh document. A shared request gate coalesces concurrent requests for the same page and spaces uncached source requests at least one second apart. After a USCCB 403 or 429, new source requests pause for at least one minute, respecting a longer `Retry-After` header; cached dates remain available. Failed responses are not cached. Leaving a date cancels its pending readings request. The cache and pause reset when the API restarts. These measures reduce source traffic but cannot guarantee availability when USCCB blocks access.

Reading sections are identified by headings rather than position; HTML is converted to plain text, decoded, and rendered without bypassing Angular sanitization. “Or” sections stay attached to their preceding reading or psalm. Distinct numbered vigil readings, repeated psalms, and sequences retain source order. Linked Mass pages are restricted to HTTPS USCCB reading URLs for the requested date; unrelated links are ignored. Empty primary pages without linked sets can fall back to a published vigil page. A USCCB 403 response is reported as blocked access rather than missing readings.

Production retains HTTPS redirection. The HTTP development profile works through the UI proxy without trusting a development certificate. No confession or intention storage endpoints exist.

## Tests

Stop the running API before rebuilding on Windows, then run:

```powershell
dotnet restore Tests/CatholicCompanion.Api.Tests.csproj --configfile NuGet.Config
dotnet test Tests/CatholicCompanion.Api.Tests.csproj --no-restore
```

The xUnit suite covers request validation, calendar-day preservation, reordered/missing reading sections, text extraction, successful caching, source failures, and calendar alignment. Build outputs are ignored rather than versioned.

## Automatic Azure deployments

The `Deploy API to Azure` GitHub Actions workflow restores from the repository's NuGet.Config, runs the API tests, publishes a portable .NET 10 package, and deploys to the existing `CatholicCompanionApi` App Service whenever code reaches `main`. It can also be run manually from the Actions tab on `main`. Failed tests or builds prevent deployment; overlapping deployments run in order. The final check calls `/health` to verify process health, not upstream reading availability.

Authentication uses OpenID Connect with a dedicated user-assigned identity. Trust is limited to `CalebWalls/CatholicCompanionApi` on `main`, and its Website Contributor role is limited to this API App Service in the personal Azure subscription. The IDs in the workflow identify that identity and subscription; they are not passwords. No publish-profile secret is stored. Standard hosted runners are free for this public repository, and the existing Azure F1 plan is unchanged.

Check the Actions tab for deployment status. To deploy an older version, revert the relevant commit on `main` and push the revert. The workflow does not run on feature branches or pull requests.
