# Architecture — Chaos Inbox

## What the system does

Chaos Inbox normalizes multiple email sources into a common `EmailEnvelope`, converts that envelope into an `EmailTicket`, persists it, and lets a human manage completion status.

```text
                             ┌─────────────────────┐
                             │      Gmail API      │
                             └──────────┬──────────┘
                                        │
┌─────────────────┐                     │
│ uploaded .eml   │──────┐              │
└─────────────────┘      │              │
                         ▼              ▼
┌─────────────────┐   ┌─────────────────────┐
│ demo email feed │──▶│    EmailEnvelope    │
└─────────────────┘   │ normalized input    │
                      └──────────┬──────────┘
                                 │
                                 ▼
                      ┌─────────────────────┐
                      │   TicketProcessor   │
                      └──────────┬──────────┘
                                 │
                     ┌───────────┴───────────┐
                     ▼                       ▼
            ┌────────────────┐      ┌──────────────────┐
            │ Ollama local   │      │ deterministic    │
            │ structured AI  │      │ fallback rules   │
            └───────┬────────┘      └─────────┬────────┘
                    │ valid JSON?             │
                    └───────────┬─────────────┘
                                ▼
                      ┌─────────────────────┐
                      │    EmailTicket      │
                      │ summary             │
                      │ priority            │
                      │ deadline            │
                      │ status              │
                      └──────────┬──────────┘
                                 │
                                 ▼
                         ┌──────────────┐
                         │    SQLite    │
                         └──────┬───────┘
                                │
                                ▼
                      ┌─────────────────────┐
                      │ browser dashboard   │
                      │ sort / status /     │
                      │ chaos injection     │
                      └─────────────────────┘
```

## Main decisions

### Normalize before analysing

Gmail, `.eml`, and demo data all become the same `EmailEnvelope`. The AI layer therefore knows nothing about Gmail APIs or MIME parsing.

### AI is optional, not foundational

Ollama is attempted first. A timeout, HTTP error, malformed JSON, unexpected priority, or other model failure falls back to deterministic rules. The user still receives a ticket.

### SQLite over a hosted database

The workload is intentionally small and the brief requires persistence rather than scale. SQLite removes deployment credentials and infrastructure while still providing real durable state.

### Local user key instead of full authentication

The browser sends an `X-User-Id` header. Records are filtered by that value. This satisfies multi-user behavior for a prototype but is explicitly not a security boundary.

## Data model

`EmailTicket` is the central durable record. A composite unique index on:

```text
UserId + Source + ExternalMessageId
```

prevents the same Gmail/EML/demo message from being turned into duplicate tickets for the same user.

## Failure handling

| Failure | Behavior |
|---|---|
| Ollama offline | deterministic fallback |
| Ollama timeout | deterministic fallback |
| Ollama malformed JSON | deterministic fallback |
| email missing body | subject/body fallback summary |
| duplicate message | existing ticket returned, no duplicate insert |
| Gmail OAuth credentials absent | clear 400 response; demo/EML paths remain usable |
| Gmail API exception | 502 response; existing tickets remain untouched |
| injected 503 | error persisted on ticket; record remains usable |

## Decision I am least confident about

I used a local `X-User-Id` as lightweight user separation instead of building authentication and per-user OAuth account linking.

I would change my mind if the product moved from a build-week prototype to a shared deployment. At that point the threat model changes: a user-provided header is not identity, and Gmail OAuth tokens need to be encrypted and associated with authenticated accounts. I would introduce proper identity first, then move Gmail authorization to a server-side web OAuth flow.
