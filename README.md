# InCleanHome Reviews Service

> Reviews & Evaluation microservice — Review + Report + SuspensionAppeal.

Three aggregates in one bounded context (they're tightly related — all are
forms of platform moderation):
- **Review** — 1-5 star ratings + comments for completed bookings.
- **Report** — user complaints against other users, reviewed by admins.
- **SuspensionAppeal** — suspended users contest their suspension.

## Endpoints

### Reviews
| Method | Path | Purpose |
|---|---|---|
| POST | `/api/v1/reviews` | Client submits a review |
| GET | `/api/v1/reviews/booking/{bookingId}` | Get review for a booking |
| GET | `/api/v1/reviews/worker/{workerId}` | All reviews for a worker |
| GET | `/api/v1/reviews/client/{clientId}` | All reviews by a client |

### Reports
| Method | Path | Purpose |
|---|---|---|
| POST | `/api/v1/reports` | Any user submits a report |
| GET | `/api/v1/reports` | List reports (admin) |
| GET | `/api/v1/reports/user/{userId}` | Reports against a user (admin) |
| PATCH | `/api/v1/reports/{id}/confirm` | Admin confirms (auto-suspend on threshold) |
| PATCH | `/api/v1/reports/{id}/dismiss` | Admin dismisses |

### Suspension Appeals
| Method | Path | Purpose |
|---|---|---|
| POST | `/api/v1/suspension-appeals` | User submits appeal of own suspension |
| GET | `/api/v1/suspension-appeals/mine` | My appeals |
| GET | `/api/v1/suspension-appeals` | List appeals (admin) |
| PATCH | `/api/v1/suspension-appeals/{id}/accept` | Admin accepts |
| PATCH | `/api/v1/suspension-appeals/{id}/reject` | Admin rejects |

## Events

### Publishes (`incleanhome.reviews.events`)
- `ReviewSubmittedEvent` — Profile uses this to update worker stats; Communication notifies worker.
- `ReportSubmittedEvent` — Communication notifies the reporter.
- `ReportConfirmedEvent` — Communication notifies the reported user.
- `SuspensionAppealSubmittedEvent` — Communication notifies appeal received.

### Consumes
- `BookingCompletedEvent` (from Booking) — audit only.

## HTTP dependencies

| Target | Used for |
|---|---|
| Booking Service | Validate that the booking exists, belongs to the client, and is completed |
| IAM Service | Auto-suspend a user when their confirmed report count crosses the threshold |

## Configuration

In Consul KV at `config/reviews-service`:
- `Reviews.ReportThresholdToSuspend` (default 3)
- `Reviews.DefaultSuspensionDays` (default 30)

## Run

```bash
cd ../incleanhome-platform
docker compose up --build -d reviews-service
```

Direct: http://localhost:5006 · Swagger: http://localhost:5006/swagger
