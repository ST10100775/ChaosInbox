# Chaos Inbox

> A nerdy email-to-ticket triage system that assumes the internet, email formats, and AI outputs can all misbehave.

Chaos Inbox reads emails, converts each email into a persistent ticket, summarizes the requested action, assigns a priority, extracts a deadline when one is present, sorts the work queue, and lets the user move the ticket through `New`, `InProgress`, `Waiting`, `Completed`, and `Archived`.

It supports three inputs:

1. **Demo mail** — zero setup and safe for a reviewer.
2. **`.eml` files** — parses real RFC/MIME email files using MimeKit.
3. **Gmail** — optional read-only OAuth integration.

AI is also optional. When Ollama is available, Chaos Inbox asks a local model for structured ticket data. If the model is unavailable, times out, or returns invalid output, a deterministic fallback analyzer keeps the product working.

## Falcorp Build Week mapping

| House rule | Chaos Inbox evidence |
|---|---|
| New to me | Local model integration, structured LLM output, Gmail OAuth, failure injection |
| Use AI | Ollama triage, with deterministic validation/fallback |
| Touch something I don't control | Gmail API and `.eml`/MIME format |
| Remember something | SQLite database in `ChaosInbox/data/chaosinbox.db` |
| More than one person | The dashboard accepts a local user ID and segregates each user's tickets |
| Run without me | Clean clone works in Visual Studio; Docker path also provided |
| Automated test | `ChaosInbox.Tests` tests urgent, deadline, and garbage-input paths |
| Free | .NET, SQLite, Gmail API, Docker and local Ollama |

---

# Fastest way to run it in Visual Studio

## Requirements

- Windows 10/11
- Visual Studio 2022 with **ASP.NET and web development** workload
- .NET 8 SDK
- Internet connection for the first NuGet restore
- Docker Desktop is optional
- Ollama is optional
- Gmail configuration is optional

## 1. Open the solution

Open:

```text
ChaosInbox.sln
```

Visual Studio should show two projects:

```text
ChaosInbox
ChaosInbox.Tests
```

Right-click **ChaosInbox** and choose **Set as Startup Project** if necessary.

## 2. Restore packages

Visual Studio normally restores automatically. If not:

```powershell
dotnet restore
```

The main packages are:

```text
Microsoft.EntityFrameworkCore.Sqlite
Google.Apis.Gmail.v1
Google.Apis.Auth
MimeKit
```

## 3. Start the web app

Choose the `https` profile and press:

```text
Ctrl + F5
```

The application should open at roughly:

```text
https://localhost:7088
```

If Visual Studio asks you to trust the local development certificate, accept it.

## 4. Prove the project works before configuring anything else

Click:

```text
+ Import demo mail
```

You should get four tickets. They are sorted so unfinished work comes first, then higher priority, then nearest deadline.

Try changing a ticket from:

```text
New → InProgress → Completed
```

Refresh the browser. The status should still be there because SQLite persisted it.

Stop Visual Studio completely, start the app again, and refresh. The ticket should still be there.

---

# What a ticket contains

Each persisted email ticket stores:

```text
Sender
Subject
Original body
AI/fallback summary
Priority
Deadline
Status
Received time
AI confidence
Whether Ollama or fallback handled it
Chaos score
Last processing warning/error
```

The queue is sorted by:

```text
1. unfinished before completed/archived
2. priority: Critical → High → Medium → Low
3. tickets with deadlines before tickets without deadlines
4. nearest deadline first
5. newest received email first
```

---

# Enable local AI with Ollama

The app does **not** require Ollama. Without it, the deterministic analyzer runs automatically.

If Ollama is installed on Windows and listening on the normal port, pull the model configured in `appsettings.json`:

```powershell
ollama pull gemma4:26b
```

Then run Chaos Inbox in Visual Studio again.

The ticket will show the badge:

```text
OLLAMA
```

instead of:

```text
FALLBACK
```

The Ollama endpoint configured by default is:

```text
http://localhost:11434/api/chat
```

The model is instructed to return structured JSON containing:

```json
{
  "summary": "...",
  "priority": "High",
  "deadlineUtc": "2026-09-14T15:00:00Z",
  "confidence": 0.87
}
```

The application validates this output. Invalid JSON, a bad priority, a timeout, an offline model, or a non-success HTTP response causes an automatic fallback rather than a crash.

---

# Read actual Gmail emails

Gmail mode is optional. **Do not commit your Google credential file or OAuth tokens.** Both locations are already ignored by `.gitignore`.

## A. Create a Google Cloud project

In Google Cloud Console:

1. Create/select a project.
2. Enable **Gmail API**.
3. Configure the Google Auth consent screen.
4. For a personal test app, make it an External app if required and add your Google account as a test user while the app remains in testing.
5. Create an OAuth client.
6. Choose **Desktop app** for the OAuth client type.
7. Download the JSON credential file.

## B. Put the credentials into the project

Rename the downloaded file:

```text
credentials.json
```

Put it here:

```text
ChaosInbox/credentials/credentials.json
```

The repository should contain only:

```text
ChaosInbox/credentials/README.txt
```

and **not** the real `credentials.json`.

## C. Run from Visual Studio

For the current Build Week version, Gmail OAuth is designed to be used while running the application natively from Visual Studio, not inside the Docker web container.

Start the app and click:

```text
↻ Sync Gmail
```

On the first run, Google authorization should open in a browser. Choose your Google account and grant the requested **read-only Gmail** permission.

Tokens are stored under:

```text
ChaosInbox/.tokens/<your-user-id>/
```

Those token files are ignored by Git.

The app searches the inbox with:

```text
in:inbox -category:promotions
```

and imports up to 20 recent messages by default.

## Important privacy choice

Chaos Inbox only requests Gmail read access. It does not send, delete, archive, label, or modify the user's Gmail messages. Ticket status changes happen only in the local Chaos Inbox SQLite database.

---

# Import `.eml` email files

This is the easiest reviewer-safe real-email path.

Two sample files are included:

```text
ChaosInbox/samples/urgent-security.eml
ChaosInbox/samples/normal-task.eml
```

In the dashboard click:

```text
↑ Import .eml
```

Select either file.

MimeKit parses the MIME message, and the resulting email flows through exactly the same ticket processor as Gmail.

---

# Chaos Mode

Every ticket has buttons that deliberately make the system worse:

```text
☢ API failure
☢ Bad deadline
☢ Break summary
```

These are controlled failure injections.

For example, **API failure** does not actually delete the record. It records:

```text
Simulated upstream API failure: HTTP 503.
```

and increases the ticket's chaos score.

This lets the demo answer a useful question:

> What happens after the API or AI misbehaves?

The answer should be: the ticket remains in SQLite, the user can still see it, and the failure is visible rather than silently swallowed.

---

# Run the automated tests

In Visual Studio:

```text
Test → Test Explorer → Run All
```

Or:

```powershell
dotnet test
```

The tests cover:

```text
Urgent language → Critical
"tomorrow" → extracted deadline
Garbage input → no crash and valid result
```

The garbage-input test is intentionally an ugly-path test rather than a trivial happy-path test.

---

# Docker

The Visual Studio/native run is the easiest path for Gmail OAuth.

Docker is useful for proving that the core application can run from a clean machine and that SQLite can survive container replacement through a volume.

From the solution folder:

```powershell
docker compose up --build -d
```

Then open:

```text
http://localhost:8080
```

The web container talks to the Ollama container at:

```text
http://ollama:11434
```

Pull the configured model into the Ollama container once:

```powershell
docker compose exec ollama ollama pull gemma4:26b
```

Refresh and import a new email.

To stop:

```powershell
docker compose down
```

The SQLite data survives because it is in the named volume:

```text
chaos-data
```

If you deliberately want to erase Docker data too:

```powershell
docker compose down -v
```

Do not run `-v` during a persistence demonstration.

---



# Known intentional limitation

The `X-User-Id` dashboard identity is **not authentication**. It is only lightweight tenant separation for the Build Week requirement that the product not be welded to one user's records.

If this became a real product, the first upgrade would be proper authentication plus server-side OAuth account linking. For this week, adding a complete identity platform would create more surface area than product value.
