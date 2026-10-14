# Catholic Companion API

ASP.NET Core 8 API serving daily Mass readings and a seven-day US liturgical calendar from USCCB.

## Run locally

```powershell
dotnet restore --configfile NuGet.Config
dotnet run --launch-profile http
```

API: http://localhost:5067. Swagger: http://localhost:5067/swagger. Health: GET `/health` (process health, not an upstream USCCB check). Run the sibling UI with `npm start`; its development proxy removes `/api` before forwarding requests.

## Endpoints

Both existing POST routes accept `{ "date": "2026-10-14" }`. Existing ISO timestamp clients remain supported; the supplied calendar day is retained. Missing, malformed, or unsupported dates produce an HTTP 400 validation response. Supported request years are 1900 through 2100; upstream readings may not exist for all dates.

- `/DailyUpdates/DailyReadings`: first reading, responsorial psalm, optional second reading, Gospel acclamation, Gospel, source URL, and optional error.
- `/DailyUpdates/LiturgicalDate`: seven ordered celebration names and matching calendar dates, with an optional error when some days are unavailable.

The existing response fields and production origin remain compatible. Source outages return safe error messages and links rather than exception details. Calendar failures use a placeholder in the affected position so subsequent days stay aligned.

## Implementation

`UsccbClient` uses a typed HttpClient with a 15-second timeout and a one-hour in-memory cache of successful HTML responses. Each parse uses a fresh document. Reading sections are identified by headings rather than position; HTML is converted to plain text, decoded, and rendered without bypassing Angular sanitization. Empty primary reading pages can fall back to a published vigil page.

Production retains HTTPS redirection. The HTTP development profile works through the UI proxy without trusting a development certificate. No confession or intention storage endpoints exist.

## Tests

Stop the running API before rebuilding on Windows, then run:

```powershell
dotnet restore Tests/CatholicCompanion.Api.Tests.csproj --configfile NuGet.Config
dotnet test Tests/CatholicCompanion.Api.Tests.csproj --no-restore
```

The xUnit suite covers request validation, calendar-day preservation, reordered/missing reading sections, text extraction, successful caching, source failures, and calendar alignment. Build outputs are ignored rather than versioned.
