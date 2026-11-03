# Restaurant AQA framework

> **Homework: Direction 1, group pre-order (levels 1, 2 and 3).** Tests of my own deployed application: UI scenarios for the
> main flows plus API scenarios for authorisation, validation and test data. Architecture: **BDD + Screenplay** (Reqnroll
> Gherkin, TUnit, Playwright for .NET), as in the framework this repository started from. Details in
> "[Group pre-order tests](#group-pre-order-tests-direction-1)" below.

End-to-end tests for the Green & Tasty restaurant application: Reqnroll (Gherkin) + TUnit + Playwright for .NET, with the Screenplay pattern.

Guidance for extending the suite (writing Gherkin, steps, Screenplay, test data, working with an AI assistant) is in [CLAUDE.md](CLAUDE.md).

## Run the tests

```bash
cp .env.example .env        # then fill in your own values (see "Configuration")
dotnet build
dotnet run --no-build -- --report-trx                          # everything
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=smoke]"   # by tag
```

Everything a run produces lands in `TestResults/`: the TUnit report and TRX, the Reqnroll report in `reqnroll/`, and Playwright traces in `traces/` (open one with `npx playwright show-trace <file>.zip`).

Some scenarios are expected to fail (`@known-defect`, bugs in the application) or are skipped (`@wip`, no admin account configured). That is intentional.

## Group pre-order tests (Direction 1)

The owner of a reservation invites guests, every guest builds their own cart, and the owner sees everything together and
sends one consolidated pre-order to the restaurant. I tested my own backend and frontend (the group pre-order page in the
storefront), deployed on AWS (API Gateway + Lambda, S3 + CloudFront).

### What is covered

| Level | Feature file | Scenarios |
|---|---|---|
| 1, viewing the participants | `Features/GroupPreOrderOverview.feature` (API) | owner and joined guests listed, per-guest dishes and totals, group total = sum of participants and follows cart changes, a guest who has not confirmed or has an empty cart, a group nobody joined, a group never opened (`participants: []`, `total: 0`), group status, and who may look: another customer, a waiter, a guest, nobody signed in, a group that does not exist |
| 2, submitting | `Features/GroupPreOrderSubmission.feature` (API) | confirmed carts merged and summed, unconfirmed carts left out, order marked `SubmittedByCustomer`, group marked `Submitted`, the waiter sees the dishes; each refusal as its own scenario (nothing confirmed, nobody joined, no own order, missing or unknown reservation, submitting twice, not the owner, nobody signed in); guest cart rules and a full group |
| 3, removing a participant | `Features/GroupPreOrderParticipants.feature` (API) | one of several removed and total recalculated, one who already confirmed, the last guest, removed guest loses access, owner cannot be removed, unknown guest → 404, unknown group → 404, a guest of another group → 400, not the owner, end-to-end up to what the waiter sees |
| UI | `Features/GroupPreOrderScreen.feature` | owner sees guests, dishes, status chips and totals on the page; empty group; another customer is not shown the group; owner removes a guest on the page; owner submits on the page; end-to-end: guests choose → owner removes one and submits → waiter sees the rest |

The test pyramid follows the assignment: the UI carries the main flows, the API carries authorisation and validation and
prepares the data (guests, their carts and the owner's own pre-order are always created through the API, because guests
join from their own devices).

### Layers

- `Api/GroupOrdersApi.cs`, `Api/Dtos/GroupOrders/` - the client and DTOs for the invite, guest sessions, carts, group snapshot, status, submit and removal.
- `Screenplay/Tasks/` - `RegisterCustomer`, `BookTableForGroup`, `JoinTheGroupPreOrder`, `PutDishInTheCart`, `SubmitTheCart`, `PlaceTheOwnersOrder`, `ViewTheGroupPreOrder`, `SubmitTheGroupPreOrder`, `RemoveParticipant`, … and the on-screen ones (`OpenTheGroupPreOrder`, `RemoveTheGuestOnScreen`, `SubmitTheGroupPreOrderOnScreen`, `OpenThePreOrderAsWaiter`).
- `Screenplay/Questions/` - `TheGroupPreOrderOnScreen`, `TheGroupPreOrderIsShown`, `TheWaiterPreOrderOnScreen`; the API answers reuse `TheOutcome` / `TheResponseAs<T>`.
- `Ui/GroupOrderPage.cs`, `Ui/WaiterPreOrderPage.cs`, `Ui/Components/ParticipantCardComponent.cs` - page objects (locators and actions only).
- `StepDefinitions/GroupPreOrder*StepDefinitions.cs` - thin glue; business wording only, so no status codes or ids in feature files.

Dishes are referred to by number on the scenario's own menu ("dish 1"), people by name. Everything a scenario creates (dishes,
reservation, own pre-order, guest sessions, and a location and table when no test table is configured) is removed afterwards
through the scenario's `CleanupScope`; customers get a unique e-mail and the `AQA-Sample-` prefix. Bookings use a random slot in the
next year, so parallel scenarios on one table do not collide, and the pre-order is still editable (the backend locks it 30
minutes before the visit).

### Run

```bash
cp .env.example .env     # fill in UI_URL, API_URL, WAITER_*, TESTTABLE_ID, TESTTABLE_LOCATION_ID
dotnet build
dotnet run --no-build -- --report-trx --results-directory TestResults
```

`TESTTABLE_ID` and `TESTTABLE_LOCATION_ID` must point to a table with 4 or more seats at a location where `WAITER_USERNAME` is
the waiter: a reservation is handed to a waiter of its location and a waiter cannot be created through the API. Without them
the scenarios fall back to creating their own location and table, which only works if the backend can find a waiter for it.

A subset, by tag:

| What | Filter |
|---|---|
| all group pre-order scenarios | `--treenode-filter "/*/*/*/*[Category=group-preorder]"` |
| a level | `--treenode-filter "/*/*/*/*[Category=level1]"` (`level2`, `level3`) |
| UI only / API only | `--treenode-filter "/*/*/*/*[Category=ui]"` / `[Category=api]` |
| smoke, end-to-end, negative | `[Category=smoke]`, `[Category=e2e]`, `[Category=negative]` |

Scenarios run in parallel and are independent of each other.

### Reports

- Locally: `TestResults/` has the TUnit HTML report and TRX, the Reqnroll report in `TestResults/reqnroll/reqnroll_report.html`, and a Playwright trace (DOM, network, screenshots) for every failed `@ui` scenario in `TestResults/traces/` (`npx playwright show-trace <file>.zip`).
- In CI (`.github/workflows/aqa-tests.yml`, Actions → AQA Tests → Run workflow, also nightly): the same files are uploaded as artifacts, a job summary lists the failed scenarios, and a failing test does not stop the pipeline - reports and traces are published first and the job turns red at the end.
- **Report Portal:** when `RP_ENDPOINT` and `RP_PROJECT` (repository variables) and `RP_API_TOKEN` (secret) are set, the workflow converts the TRX to JUnit (`tools/trx_to_junit.py`) and imports it as a launch. Without them the step is skipped.

### Behaviour the assignment leaves open (what the tests expect)

| Situation | Expected |
|---|---|
| Submitting a group that is already submitted | 400, "Group reservation was submitted." |
| Anyone but the owner views, submits or removes (another customer, a waiter) | 403, "Only the reservation owner can perform this action." |
| Not signed in, or a guest's session token on an owner endpoint | 401 |
| Group, reservation or participant that does not exist | 404 |
| Participant that belongs to another group | 400, "Guest session does not belong to this group reservation." |
| Removing the last guest | allowed; only the owner is left, total 0 |
| Removing the owner's own session | 400, "The owner session cannot be removed from the group." |
| A group nobody joined and the owner never opened | `participants: []`, `total: 0`; once the owner opens it, the owner is the only participant (empty cart) |
| Submitting with no confirmed cart, or without the owner's own pre-order | 400 |
| A guest joining or adding dishes after the group was submitted | 400 / 409, "Group reservation was submitted." |
| A guest joining a full group | 400, "Guest limit exceeded" |

### Known limits

- The grand total and the consolidated list on the page count only guests who confirmed their cart (the same rule the backend applies on submit), so UI scenarios that check totals confirm the carts first.
- The waiter's page is opened directly (`/waiter-pre-order`, handing over the reservation the way the reservation card does) instead of clicking through the waiter dashboard, whose date filter would make the test depend on the day it runs. The API scenarios "The waiter sees the dishes the owner submitted" and the end-to-end one cover the same fact through the order the waiter's page loads (`GET /orders/{reservationId}` with the waiter's token).
- The waiter checks assume the waiter in `WAITER_*` is the one assigned to the reservation (that is why a test table with that waiter is required).
- Removal of a participant and the 30-minute lock are tested on the API; the UI covers removal through the page only for the happy path.

### Defects the tests found

A full run against the deployed `develop` environment (137 scenarios) ends with **6 failures, and all of them are
defects of the application itself, none are test problems**. They are kept failing on purpose, so the report shows them.

| Scenario | Expected | Actual |
|---|---|---|
| A guest sees the capacity the manager entered | the location's capacity as entered (80) | 0 |
| The form explains what is wrong with a detail (last name, empty / too long) | "Last name is required" / "Last name must be up to 50 characters" | the message is not shown (2 scenarios) |
| An invalid location is not created (empty address / negative capacity) | 400 | 201, the invalid location is created (2 scenarios) |
| Someone who never registered is refused the same way | 401 | 500 Internal Server Error |

Two more observations from running the suite, which are not failures but explain why some calls are slow:

- Adding a dish to a guest's cart reads the location's dishes with a table scan (`DishRepository.GetDishesByLocationIdAsync`).
  On the small shared `develop` stack this sometimes answers with 504 or does not see a just-created dish for a while, so the
  tests retry that one call for up to a minute. A query by an index instead of a scan would remove the delays.
- For the same reason CI runs two tests at a time with a 60 second timeout (`MAX_PARALLEL_TESTS`, `DEFAULT_TIMEOUT_MS`).

## Quality gates

| Where | What runs |
|---|---|
| Git hooks (local) | `pre-commit`: `dotnet format` check and Gherkin lint on staged files · `commit-msg`: Conventional Commits · `pre-push`: build with analyzers |
| `aqa-quality.yml` | on every push and pull request: no generated files tracked, format, build (warnings are errors), Gherkin lint, test discovery |
| `aqa-tests.yml` | the scenarios themselves, nightly and on demand (Actions → AQA Tests → Run workflow) |

Enable the hooks once per clone, from the repository root:

```bash
dotnet tool restore --tool-manifest aqa/restaurant-aqa-framework/.config/dotnet-tools.json
git config core.hooksPath aqa/restaurant-aqa-framework/.husky
```

The hooks only act on commits and pushes that touch `aqa/restaurant-aqa-framework`. Gherkin lint needs Node.js.

## Configuration

There is no shared environment and no committed `.env`: copy `.env.example` to `.env` and fill it in with the data of **your own** deployment (see "Testing your own deployment"). `.env` is git-ignored; never commit it.

Settings come from `.env` or real environment variables (variables win):

| Setting | Meaning |
|---|---|
| `UI_URL` | where the frontend is served |
| `API_URL` | base URL of the backend API, including the stage (for example `…/develop`) |
| `WAITER_USERNAME`, `WAITER_PASSWORD` | a waiter account that already exists in the environment under test |
| `TESTTABLE_ID`, `TESTTABLE_LOCATION_ID`, `TESTTABLE_WAITER_*` | a table, its location and the waiter assigned to it (the group pre-order scenarios need them) |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD` | an admin account; without it the admin scenarios are skipped |

The full list, with defaults, is in `.env.example`.

## Testing your own deployment

### If you deployed the backend / frontend / DevOps homework

1. `cp .env.example .env` and set `UI_URL` and `API_URL` to your deployment (API URL includes the stage, for example `…/develop`).
2. Waiters cannot be created by signing up, so create a waiter (and, for the admin scenarios, an admin) in *your* environment and put its credentials in `WAITER_*` / `ADMIN_*`. Optionally add a table and its assigned waiter (`TESTTABLE_*`).
3. Run `dotnet build` and a small slice first (`--treenode-filter "/*/*/*/*[Category=smoke]"`).

### If you have not deployed your own homework

If you deployed the original application, test that: set `UI_URL` / `API_URL` as above. Otherwise point the framework at a comparable public site (Booking, Airbnb, OpenTable, …) and write all the layers for it yourself: Gherkin features, step definitions, Screenplay tasks and questions, page objects with locators, and API clients where the site has a usable API. Use only flows you are allowed to automate (no purchases, no real bookings, respect the site's terms), and set `UI_URL` to that site.

1. **Generate a skeleton with Playwright codegen** (see "Presetup instructions"): record one flow, for example search → open a result → check details. Save the generated code to a scratch file, it is only raw material.
2. **Split it across the layers**: locators and small actions go to `Ui/` page objects, the flow becomes a task in `Screenplay/Tasks/`, the read-backs become questions in `Screenplay/Questions/`, the business wording goes to `Features/` and thin glue to `StepDefinitions/`.
3. **Replace weak locators.** Codegen often emits positional or text-fragile selectors. Prefer role + accessible name, label, test id, then CSS (see [CLAUDE.md](CLAUDE.md)); check them in the Playwright inspector.
4. Tag scenarios `@ui` / `@api`, keep them independent, and run `npx gherkin-lint -c .gherkin-lintrc "Features/*.feature"`.

### CI on your own public repository

Publish your work in your own public Git repository (the link is the deliverable). Workflows are disabled in a fresh fork until you enable them. Add the same values under Settings → Secrets and variables → Actions:

- **Variables:** `AQA_UI_URL`, `AQA_API_URL`, `AQA_TESTTABLE_ID`, `AQA_TESTTABLE_LOCATION_ID` (and `RP_ENDPOINT`, `RP_PROJECT` for Report Portal)
- **Secrets:** `AQA_WAITER_USERNAME`, `AQA_WAITER_PASSWORD`, `AQA_ADMIN_USERNAME`, `AQA_ADMIN_PASSWORD`, `AQA_TESTTABLE_WAITER_USERNAME`, `AQA_TESTTABLE_WAITER_PASSWORD` (and `RP_API_TOKEN`)

`aqa-quality.yml` runs on every push and pull request; `aqa-tests.yml` (nightly and on demand) tests whatever those variables point at. Never put credentials in the repository, in `.env.example` or in test code.

## Presetup instructions

This framework contains PlaywriteInstaller script so you don't need to setup it manually.
For full BDD highlighting in your IDE you may need to install Reqnroll extension https://docs.reqnroll.net/latest/ide-integrations/index.html
To start the Playwright code generator open Powershell in the framework directory and enter the line:
```bash
bin/Debug/net10.0/playwright.ps1 codegen "<your UI_URL or the public site you test>"
```
Build first so `playwright.ps1` exists. The URL is optional.
