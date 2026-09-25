# Starter solution

```
starter/
├── PayFlowTask.sln
├── docker-compose.yml
├── postman/
│   └── PayFlow.postman_collection.json
├── src/
│   ├── PayFlow.MockServer/        the PayFlow sandbox — do not modify
│   ├── Integration.Domain/        our payment model
│   ├── Integration.Application/   ports and use cases
│   ├── Integration.Infrastructure/ the PayFlow client, repository
│   └── Integration.Api/           endpoints, including the webhook receiver
└── tests/
    └── Integration.Tests/
```

## Running

Sandbox on `:8080`:

```bash
docker compose up
# or
dotnet run --project src/PayFlow.MockServer
```

Your integration on `:5099`:

```bash
dotnet run --project src/Integration.Api
```

Tests:

```bash
dotnet test
```

## Notes

**`src/PayFlow.MockServer` stands in for PayFlow.** Treat it as a third party: you can read it,
call it and observe it, but you would not be able to change it in real life, so changes there do
not count as solving anything. Everything else is yours to restructure.

The Clean Architecture layering matches our house layout. You are not obliged to keep it — if you
think a different structure suits a four-hour vertical slice better, do that and say why in
`DECISIONS.md`.

`Integration.Api` currently answers `501` on all three endpoints. That is the work.

## Postman

`postman/PayFlow.postman_collection.json` covers the sandbox endpoints. Importing it is the
fastest way to poke at the API by hand. Extending it as you go is a house standard here — if you
add requests, include the updated file in your submission.
