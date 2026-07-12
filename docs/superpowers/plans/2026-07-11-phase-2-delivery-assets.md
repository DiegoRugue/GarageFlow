# GarageFlow Phase 2 Delivery Assets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce the executable API collection, rendered architecture, HPA load demonstration, complete Phase 2 README, evidence/runbooks, accessible video linkage, and visually verified final submission PDF.

**Architecture:** Version-controlled sources generate every repeatable artifact: Mermaid produces diagrams, Postman scripts reproduce authentication/webhook flows, k6 drives the live query load, and a validated evidence file drives the final document. Human-only actions—SNS confirmation, recording/upload, and repository collaborator access—remain explicit gates with verification evidence.

**Tech Stack:** Markdown, Mermaid CLI 11.16.0, Postman Collection v2.1/Newman, CryptoJS 4.2.0, Grafana k6, PowerShell/Bash, Python 3 with python-docx/ReportLab, Poppler PDF rendering, GitHub CLI, AWS CLI, kubectl.

## Global Constraints

- Begin repeatable documentation work after the first four checkpoints pass; capture live evidence only after a successful AWS deploy/HPA/destroy lifecycle.
- Write user-facing documentation in Brazilian Portuguese while keeping code symbols, API fields, commands, and configuration keys in English.
- Use only valid synthetic examples: CPF `52998224725`, normalized Brazilian phone `+5511999999999`, and generated UUIDs; never copy the design's illustrative invalid CPF into a successful request.
- Document both opening endpoints: `POST /work-orders` for existing IDs and `POST /work-orders/intake` for all-new nested registration.
- Map rubric terms exactly: `Received=Recebida`, `Diagnosing=Diagnóstico`, `WaitingApproval=Aguardando Aprovação`, `InProgress=Execução`, `Completed=Finalizada`, `Delivered=Entregue`.
- Keep secrets out of Git, screenshots, console transcripts, collection variables, PDFs, diagrams, and uploaded artifacts.
- Postman stores no token/password/HMAC value in the collection; users provide them through a local environment and generated response variables.
- Webhook examples calculate lowercase HMAC-SHA256 over exact `<timestamp>.<raw-body>` bytes and resend identical bytes for idempotent retries.
- k6 authenticates once per virtual user, has bounded stages, uses synthetic data, and defines `http_req_failed < 2%` and `p(95) < 1000ms` thresholds.
- The HPA demonstration begins at two ready pods, scales toward at most six, stops load, and visibly returns to two.
- The video is public/unlisted and accessible without the creator's authenticated session, lasts at most 15 minutes, and includes deploy, architecture, API, webhook/SNS, HPA, and destroy evidence.
- The final PDF includes repository URL, architecture, video URL, main evidence, and cleanup; links must be clickable and anonymously verified.
- Grant `soat-architecture` at least read access only after explicit user authorization for that external repository mutation, then verify access.
- Use the `documents:documents` and `pdf:pdf` skills during the final DOCX/PDF task and complete their render-and-visual-verification workflow.
- Do not declare the phase complete until every mandatory `AGENTS.md` build/test command passes with Docker available.

---

## File Structure

### Create — executable assets

- `docs/postman/GarageFlow.Phase2.postman_collection.json`
- `docs/postman/GarageFlow.Phase2.postman_environment.example.json`
- `scripts/load-test/work-orders.js`
- `scripts/render-phase2-diagrams.ps1`
- `scripts/validate-doc-links.ps1`

### Create — architecture source and images

- `docs/architecture/diagrams/mermaid-config.json`
- `docs/architecture/diagrams/source/application.mmd`
- `docs/architecture/diagrams/source/aws-academy.mmd`
- `docs/architecture/diagrams/source/kubernetes.mmd`
- `docs/architecture/diagrams/source/database-reliability.mmd`
- `docs/architecture/diagrams/source/deployment-flow.mmd`
- Matching `.svg` and `.png` files under `docs/architecture/diagrams/images/`.

### Create — delivery and final document

- `docs/delivery/demo-runbook.md`
- `docs/delivery/video-script.md`
- `docs/delivery/final-submission-checklist.md`
- `docs/delivery/collect-phase2-evidence.ps1`
- `docs/delivery/phase-2-evidence.schema.json`
- `docs/delivery/phase-2-live-evidence.json` — non-secret snapshot captured while AWS is still running.
- `docs/delivery/phase-2-evidence.json` — created only from actual verified inputs.
- `docs/entrega-final/build_phase_2_delivery.py`
- `docs/entrega-final/Tech-Challenge-Fase-2-GarageFlow.docx`
- `docs/entrega-final/Tech-Challenge-Fase-2-GarageFlow.pdf`
- `docs/entrega-final/rendered-phase2/*.png` — page-by-page visual QA.

### Modify

- `README.md` — replace Phase 1 framing with the complete Phase 2 delivery guide while retaining useful local/DDD/security history.
- `docs/ddd/diagrams/source/work-order-state-machine.mmd` and rendered image — remove obsolete `Created`, work-order `Approved`, and StartWork flow.
- `.github/workflows/quality-gate.yml` — JSON/Postman, Mermaid-source, k6 syntax, and documentation-link validation where deterministic.

---

### Task 1: Create and render Phase 2 architecture diagrams

**Files:**
- Create: Mermaid config, five source diagrams, render script, SVG/PNG outputs
- Modify: existing WorkOrder state-machine source/image

**Interfaces:**
- Produces: stable diagram filenames consumed by README and final PDF.

- [ ] **Step 1: Author five source diagrams with exact responsibilities**

Use quoted node labels and no secrets/account IDs:

1. `application.mmd` — Host composition root around API/Infrastructure adapters, Application, Domain, SharedKernel; arrows follow allowed dependencies only.
2. `aws-academy.mmd` — Internet → LoadBalancer → EKS two nodes/pods; private RDS; ECR; SNS email; Secrets Manager; S3 state; public/private subnets in two AZs; no NAT.
3. `kubernetes.mmd` — ConfigMap/Secret → migration Job → two-replica Deployment → Service; Metrics Server → HPA 2–6.
4. `database-reliability.mmd` — command transaction writes aggregates/inbox/outbox; leased worker publishes SNS and marks/retries.
5. `deployment-flow.mmd` — protected deploy preflight/Terraform/ECR/metrics/config/migration/rollout/smoke, demo, protected cleanup, Terraform destroy.

Update the existing state machine to exact Phase 2 statuses and rejection/cancellation branches.

- [ ] **Step 2: Pin visual configuration**

`mermaid-config.json` uses a light neutral theme, white background, `fontFamily: "Arial, sans-serif"`, `securityLevel: "strict"`, and generous flowchart spacing. Sources must include descriptive titles through `%%{init: ...}%%` or surrounding README captions.

- [ ] **Step 3: Implement deterministic rendering**

`scripts/render-phase2-diagrams.ps1` enumerates exactly the six source files, invokes:

```powershell
npx --yes --package @mermaid-js/mermaid-cli@11.16.0 mmdc `
  --configFile docs/architecture/diagrams/mermaid-config.json `
  --input $source `
  --output $svg `
  --backgroundColor white
```

Then renders PNG at width 2200 from the same source. The script stops on any nonzero exit and checks every output exists and is larger than 10 KB.

The PNG invocation is explicit:

```powershell
npx --yes --package @mermaid-js/mermaid-cli@11.16.0 mmdc `
  --configFile docs/architecture/diagrams/mermaid-config.json `
  --input $source `
  --output $png `
  --width 2200 `
  --backgroundColor white
```

- [ ] **Step 4: Render and visually inspect every image**

```powershell
./scripts/render-phase2-diagrams.ps1
```

Open all PNG outputs with image inspection. Confirm no clipped labels, crossing arrows that obscure meaning, obsolete statuses, secret-like strings, or unreadable text at README width.

- [ ] **Step 5: Commit source and generated images together**

```powershell
git add docs/architecture docs/ddd/diagrams scripts/render-phase2-diagrams.ps1
git commit -m "docs(architecture): render Phase 2 deployment diagrams"
```

---

### Task 2: Add an executable Postman Phase 2 collection

**Files:**
- Create: collection and example environment JSON
- Modify: CI JSON validation

**Interfaces:**
- Consumes: live/local API, staff credentials, webhook HMAC secret.
- Produces: captured JWT/work-order/estimate IDs and both opening/status/list/webhook journeys.

- [ ] **Step 1: Define collection variables and folders**

Collection variables contain only non-secret defaults:

```text
baseUrl=http://localhost:8080
token=
requestId=
customerId=
vehicleBrandId=
vehicleModelId=
vehicleColorId=
vehicleId=
serviceId=
workOrderId=
estimateId=
webhookEventId=
webhookSecret=
staffEmail=
staffPassword=
staffActivePassword=
```

Folders, in order:

```text
00 Health and OpenAPI
01 Authentication
02 Existing-ID Work Order Opening
03 Complete Intake
04 Status and Active Queue
05 Estimate Submission
06 Signed External Decision
07 Completion and Delivery
```

- [ ] **Step 2: Make authentication repeatable before the business journeys**

The first authentication request posts `staffEmail`/`staffPassword`. When it returns `200` with `mustChangePassword === true`, save that JWT temporarily, call `PUT /users/me/password` with `currentPassword = staffPassword` and `newPassword = staffActivePassword`, then log in again with `staffActivePassword` and require `mustChangePassword === false`. If the initial credentials return `401`, retry once with `staffActivePassword`; this makes a second Newman run work after the bootstrap password was already changed. Capture only the final active JWT in `token`, fail when either secret is empty, and never print either password or the token.

- [ ] **Step 3: Use valid complete-intake sample data and response capture**

At collection start, generate one run suffix and keep it unchanged for the whole execution. Generate two non-repeated-digit CPFs by creating nine digits and calculating both official check digits; generate unique valid `ABC1D23` plates, emails, phones, service descriptions, and optional inventory names from that suffix. Include a test that recalculates each CPF before any create request. This keeps every Newman run independent while documentation examples may continue to show valid CPF `52998224725` and phone `+5511999999999`.

The existing-ID folder creates and captures customer, brand, model, color, vehicle, and service IDs, then calls `POST /work-orders`. The complete-intake request uses its generated `requestId`/identity values, at least one service, and an optional `Part`. Resolve the first request body once after variable substitution and retain those exact bytes for the replay request. Post-response tests require `201`, validate `Location`, `status === "Received"`, nonempty IDs, then save `workOrderId`/`estimateId`. The byte-equivalent replay requires `200` with the same IDs; a conflict request preserves the request ID but changes one price and requires `409`.

Continue the same intake work order through `POST /work-orders/{id}/estimates/{estimateId}/submit`, signed webhook approval, detail lookup to capture its estimate service-line ID, service start, service completion, and delivery. Assert `WaitingApproval`, `InProgress`, `Completed`, and `Delivered` at their corresponding status/detail checks. Exercise the active queue before terminal delivery and prove the delivered order is excluded afterward.

- [ ] **Step 4: Sign the webhook from exact raw Postman body**

The pre-request script is:

```javascript
const CryptoJS = pm.require('npm:crypto-js@4.2.0');
const secret = pm.environment.get('webhookSecret');
if (!secret) {
  throw new Error('webhookSecret must be set in the local Postman environment');
}

const timestamp = Math.floor(Date.now() / 1000).toString();
const rawBody = pm.variables.replaceIn(pm.request.body.raw);
pm.request.body.update(rawBody);
const signature = CryptoJS
  .HmacSHA256(`${timestamp}.${rawBody}`, secret)
  .toString(CryptoJS.enc.Hex);

pm.request.headers.upsert({ key: 'X-GarageFlow-Timestamp', value: timestamp });
pm.request.headers.upsert({ key: 'X-GarageFlow-Signature', value: signature });
```

The body uses collection IDs and a generated event UUID; tests require `204`. The duplicate request reuses exact event ID and raw body.

- [ ] **Step 5: Keep secrets out of the example environment**

Use Postman environment v2.1 JSON with only `baseUrl` populated. `staffEmail`, `staffPassword`, `staffActivePassword`, and `webhookSecret` exist with empty values and `type: "secret"`; no exported current values, JWT, password, or HMAC secret is committed.

- [ ] **Step 6: Validate JSON and run against local Compose**

```powershell
Get-Content docs/postman/GarageFlow.Phase2.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
Get-Content docs/postman/GarageFlow.Phase2.postman_environment.example.json -Raw | ConvertFrom-Json | Out-Null
npx --yes newman@6.2.1 run docs/postman/GarageFlow.Phase2.postman_collection.json `
  --environment docs/postman/GarageFlow.Phase2.postman_environment.example.json `
  --env-var "staffEmail=$env:GARAGEFLOW_STAFF_EMAIL" `
  --env-var "staffPassword=$env:GARAGEFLOW_STAFF_PASSWORD" `
  --env-var "staffActivePassword=$env:GARAGEFLOW_STAFF_ACTIVE_PASSWORD" `
  --env-var "webhookSecret=$env:GARAGEFLOW_WEBHOOK_SECRET"
```

Expected: full local journey PASS; Newman output and environment export are not committed.

- [ ] **Step 7: Commit the collection**

```powershell
git add docs/postman .github/workflows/quality-gate.yml
git commit -m "docs(api): add executable Phase 2 Postman collection"
```

---

### Task 3: Add and demonstrate bounded k6 HPA load

**Files:**
- Create: `scripts/load-test/work-orders.js`
- Create: `docs/delivery/demo-runbook.md`

**Interfaces:**
- Consumes: `BASE_URL`, `STAFF_EMAIL`, `STAFF_PASSWORD`, `WORK_ORDER_ID` environment values.
- Produces: repeatable list/status traffic and terminal thresholds.

- [ ] **Step 1: Implement VU-local lazy authentication**

At module scope inside each VU isolate, keep `let token;`. `login()` posts `/auth/login` once, requires `200`, `mustChangePassword === false`, and caches `token`. Never print the token or password.

- [ ] **Step 2: Define bounded stages and thresholds**

Use exactly:

```javascript
export const options = {
  stages: [
    { duration: '30s', target: 10 },
    { duration: '60s', target: 50 },
    { duration: '90s', target: 100 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(95)<1000'],
  },
};
```

Each iteration calls `GET /work-orders?page=1&pageSize=20` and `GET /work-orders/${WORK_ORDER_ID}/status` with bearer auth, checks `200`, validates a recognized active status, and sleeps between 0.2 and 0.5 seconds.

- [ ] **Step 3: Reject unsafe/missing inputs before load**

The script throws during init when base URL, credentials, or work-order ID are missing; rejects non-HTTP URLs and non-UUID work-order ID; and prints only base URL/stage summary.

- [ ] **Step 4: Write the exact HPA demonstration runbook**

The runbook uses two terminals. Terminal A keeps the watch running; Terminal B runs load and snapshots:

```bash
kubectl -n garageflow get deployment,pods,hpa
kubectl -n garageflow top pods
kubectl -n garageflow get hpa,pods -w
k6 run scripts/load-test/work-orders.js
kubectl -n garageflow top pods
```

It requires recording: initial two pods, CPU/memory crossing target, desired/current replicas rising but never above six, test stop, and return to two after the configured scale-down window. It also contains abort/cleanup commands and budget warning.

- [ ] **Step 5: Inspect and dry-run the script**

```powershell
docker run --rm -v "${PWD}:/work" -w /work grafana/k6:1.7.1 inspect scripts/load-test/work-orders.js
```

Then run against the deployed synthetic work order with credentials supplied only in process environment. Expected: thresholds pass and HPA evidence is visible.

- [ ] **Step 6: Commit load assets**

```powershell
git add scripts/load-test docs/delivery/demo-runbook.md
git commit -m "test(performance): add bounded HPA load demonstration"
```

---

### Task 4: Rewrite README as the Phase 2 operating guide

**Files:**
- Modify: `README.md`
- Create: `docs/delivery/final-submission-checklist.md`

**Interfaces:**
- Consumes: final filenames/commands from all plans.
- Produces: one navigable source of truth for evaluator and team.

- [ ] **Step 1: Replace the Phase 1 heading and add an evaluator index**

README top links directly to: architecture images, local Compose, API/Scalar, Postman collection, tests/coverage, Terraform bootstrap, manual deploy/destroy, Kubernetes troubleshooting, HPA demo, and the final PDF path. Do not publish a fictitious video URL in this intermediate task; Task 5 inserts the real anonymously verified link.

- [ ] **Step 2: Document application behavior exactly**

Include lifecycle diagram/Portuguese mapping, both opening request/response contracts, `201/200/409` intake idempotency, status query, active queue priority, customer history distinction, webhook signature/replay rules, and outbox/SNS eventual delivery/possible duplicate.

- [ ] **Step 3: Document local operation and verification**

Include Docker Compose startup/cleanup, URLs, local bootstrap password-change flow, environment overrides, migration mode, test commands, container smoke, Postman/Newman, and no-AWS local outbox default.

- [ ] **Step 4: Document Academy deployment and budget safety**

Include prerequisites, temporary credential refresh, protected Environment inputs, one-time S3 bootstrap, deploy workflow, SNS email confirmation, EKS/RDS/ECR checks, no-TLS synthetic-data warning, k6/HPA steps, destroy workflow, retained bucket, final object-version cleanup, and common failure recovery.

- [ ] **Step 5: Add final checklist with binary evidence fields**

Checklist items are yes/no plus evidence location for: repository access, CI green, live deploy, both APIs, queue/status, webhook, SNS receipt, HPA scale/recovery, private RDS, immutable ECR SHA, destroy verification, anonymous video access, PDF render review, and all links.

- [ ] **Step 6: Validate all relative links and commands**

Create `scripts/validate-doc-links.ps1` to enumerate repository Markdown links, skip external URLs/anchors, URI-decode local targets, and fail when any referenced file is absent. Run it plus documented commands in a clean shell where practical; reject obsolete `Api`, `Infrastructure`, `Created`, work-order `Approved`, or StartWork instructions.

- [ ] **Step 7: Commit operating documentation**

```powershell
git add README.md docs/delivery/final-submission-checklist.md scripts/validate-doc-links.ps1
git commit -m "docs: publish GarageFlow Phase 2 operating guide"
```

---

### Task 5: Capture real evidence and prepare the 15-minute video

**Files:**
- Create: evidence schema/collector/actual JSON, `video-script.md`
- Modify: README video link after publication

**Interfaces:**
- Produces: validated non-secret evidence consumed by the final document.

- [ ] **Step 1: Define a strict evidence schema**

Require nonempty HTTPS repository/video URLs, commit SHA (40 lowercase hex), UTC deploy/destroy timestamps, public API URL, image SHA, status of CI/SNS/HPA/destroy, and participant/group fields. Reject localhost, example domains, empty arrays, and secret-named keys.

- [ ] **Step 2: Capture live evidence before destruction**

Run `collect-phase2-evidence.ps1 -CaptureLive` while the Service, two-or-more pods, HPA, ECR SHA, private RDS, and confirmed SNS subscription still exist. It obtains repository URL from `gh repo view`, SHA from Git, deploy run URL from `gh run list`, and non-secret service/pod/HPA/resource data from AWS CLI/kubectl, validates it, then writes `phase-2-live-evidence.json`. The schema forbids credential, token, password, key, secret-value, connection-string, or raw-log fields.

- [ ] **Step 3: Write a timed video script under 15 minutes**

Use this budget:

```text
00:00–01:30 objective and Clean Architecture
01:30–03:00 AWS/Kubernetes/Terraform diagrams and deploy workflow
03:00–05:30 EKS, private RDS, ECR SHA, two ready pods
05:30–08:00 both work-order opening endpoints, status, active queue
08:00–10:00 signed webhook, duplicate replay, SNS email
10:00–12:30 k6 and HPA scale-out/recovery
12:30–14:00 protected destroy and absence evidence
14:00–14:30 repository/PDF/access recap
```

Never reveal terminal environment, JWT, HMAC, AWS keys, connection string, or password on screen; preconfigure filtered commands.

- [ ] **Step 4: Record, publish, and test anonymously**

Rehearse until the live lifecycle succeeds, then record the final deploy/demo/destroy sequence. Run `-CaptureLive` before its destroy segment. After the protected destroy and absence verification finish, publish the video public/unlisted, open its URL in a logged-out/incognito browser, and verify playback and duration ≤15:00.

- [ ] **Step 5: Finalize evidence after protected destruction**

Run `collect-phase2-evidence.ps1 -Finalize` only after `verify-destroy.sh` passes and the video URL is anonymously verified. It requires the valid live snapshot, obtains the destroy/CI run URLs, verifies the retained state bucket evidence, interactively requests participant/group identifiers and the published video URL, validates every field, and writes UTF-8 `phase-2-evidence.json`. It must never attempt to query the destroyed cluster in this mode.

- [ ] **Step 6: Request authorization and grant evaluator access**

After explicit user confirmation for this external mutation, grant and verify read access with:

```bash
REPOSITORY="$(gh repo view --json nameWithOwner -q .nameWithOwner)"
gh api --method PUT "repos/${REPOSITORY}/collaborators/soat-architecture" \
  -f permission=pull
gh api "repos/${REPOSITORY}/collaborators/soat-architecture/permission" \
  --jq '.user.login + ":" + .permission'
gh api "repos/${REPOSITORY}/invitations" \
  --jq '.[] | select(.invitee.login == "soat-architecture") | .invitee.login + ":pending"'
```

Accept either verified `pull` permission or a verified pending invitation until the evaluator accepts it; record that non-sensitive state and do not broaden permission beyond pull/read.

- [ ] **Step 7: Commit evidence and real links**

```powershell
git add docs/delivery/phase-2-live-evidence.json docs/delivery/phase-2-evidence.json docs/delivery/video-script.md README.md
git commit -m "docs(delivery): record Phase 2 demonstration evidence"
```

---

### Task 6: Generate and visually verify the final PDF

**Files:**
- Create: generator, DOCX, PDF, rendered QA pages
- Consume: evidence JSON and architecture images

**Interfaces:**
- Produces: evaluator-ready `Tech-Challenge-Fase-2-GarageFlow.pdf` with clickable real links and no unresolved fields.

- [ ] **Step 1: Invoke document and PDF skills and load the bundled workspace dependencies**

Read both skill instruction files completely before generating artifacts. Use the bundled Python/docx/report/PDF/render tools they identify; do not substitute an unverified ad-hoc conversion path.

- [ ] **Step 2: Validate evidence before document creation**

The Python generator loads `phase-2-evidence.json`, validates it against the schema, verifies all referenced diagram/image paths, rejects example/localhost URLs and strings containing bracketed fill-in markers, and stops before writing output on any failure.

- [ ] **Step 3: Generate a concise submission document**

Create a polished 3–6 page DOCX containing:

1. participant/group, repository, video, commit, delivery date;
2. objective and feature checklist;
3. application/AWS/Kubernetes architecture with readable images;
4. both endpoint contracts and status workflow;
5. CI/CD, tests, coverage, HPA, SNS, and security evidence;
6. cleanup result and links to README/Postman.

Use real hyperlinks, page headers/footers, consistent typography, captions, and page breaks; never include credentials or raw workflow logs.

- [ ] **Step 4: Convert to PDF and render every page to PNG**

Run the skill-prescribed DOCX render/verification, export PDF, then Poppler rendering. Expected outputs are the committed DOCX, PDF, and numbered PNG pages under `rendered-phase2`.

- [ ] **Step 5: Inspect every rendered page visually**

Use image viewing on every page at original detail. Reject clipped tables, tiny diagrams, blank pages, widows/orphans that harm readability, broken accents, non-clickable-looking URLs, missing evidence, or any unresolved field. Fix generator source and regenerate all outputs after each issue.

- [ ] **Step 6: Validate PDF metadata/text/links**

Use PDF tooling to assert page count 3–6, title includes `GarageFlow` and `Fase 2`, extracted text contains repository/video URLs and all six happy-path statuses, no secret key names/values, and link annotations exist for repository/video/README references. Make `build_phase_2_delivery.py --validate-only` rerun those source/evidence/PDF assertions without regenerating the document.

- [ ] **Step 7: Commit final document sources and artifacts**

```powershell
git add docs/entrega-final docs/delivery/phase-2-evidence.json
git commit -m "docs(delivery): generate Phase 2 final submission"
```

---

### Task 7: Run the final release verification

**Files:**
- No new files unless a discovered documentation defect requires a focused fix.

- [ ] **Step 1: Run every mandatory repository command**

```powershell
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
dotnet test GarageFlow.slnx
```

Expected: all PASS with Docker available.

- [ ] **Step 2: Run delivery artifact validation**

```powershell
docker compose config --quiet
docker build -t garageflow:final-verification .
bash scripts/smoke-container.sh garageflow:final-verification
Get-Content docs/postman/GarageFlow.Phase2.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
Get-Content docs/postman/GarageFlow.Phase2.postman_environment.example.json -Raw | ConvertFrom-Json | Out-Null
./scripts/render-phase2-diagrams.ps1
./scripts/validate-doc-links.ps1
docker run --rm -v "${PWD}:/repo" -w /repo rhysd/actionlint:1.7.12
docker run --rm -v "${PWD}:/workspace" -w /workspace/infra hashicorp/terraform:1.15.7 fmt -check -recursive
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/bootstrap/state-backend init -backend=false
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/bootstrap/state-backend validate
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/environments/academy init -backend=false
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/environments/academy validate
kubectl kustomize k8s | kubectl apply --dry-run=client --validate=false -f -
kubectl kustomize k8s | docker run --rm -i ghcr.io/yannh/kubeconform:v0.7.0 -strict -summary -kubernetes-version 1.36.0 -ignore-missing-schemas -
docker run --rm -v "${PWD}:/work" -w /work grafana/k6:1.7.1 inspect scripts/load-test/work-orders.js
python docs/entrega-final/build_phase_2_delivery.py --validate-only
```

Expected: all PASS.

- [ ] **Step 3: Recheck human gates anonymously**

Verify video, repository access invitation/state, PDF link, Postman file, rendered diagrams, latest successful CI/deploy/destroy run URLs, SNS confirmation evidence, and cleanup evidence. Check the final submission checklist completely.

- [ ] **Step 4: Prove the repository is clean and release scope is intentional**

```powershell
git diff --check
git status --short
git log --oneline --decorate -15
```

Expected: no unstaged/untracked accidental files, no state/plan/environment export, and an understandable checkpoint commit history.

---

## Checkpoint Acceptance

The delivery-assets checkpoint—and therefore Phase 2—is complete only when:

- diagrams match implemented code/infrastructure and are readable in README/PDF;
- the Postman collection executes both opening methods, status/list, and signed decision without committed secrets;
- k6 passes thresholds and recorded HPA evidence shows scale-out and return to two pods;
- README contains complete local, Terraform, Kubernetes, Academy, troubleshooting, and cleanup instructions;
- actual evidence JSON contains no fictitious URL/value and validates against its schema;
- the ≤15-minute video plays anonymously and shows the required sequence without secrets;
- `soat-architecture` access is verified after authorization;
- DOCX/PDF are generated from source, every page is visually reviewed, and hyperlinks/text checks pass;
- CI/deploy/destroy evidence and all mandatory build/test/runtime validations pass;
- the repository is clean and contains no Terraform state, plan, credential, rendered secret, token, or local environment export.
