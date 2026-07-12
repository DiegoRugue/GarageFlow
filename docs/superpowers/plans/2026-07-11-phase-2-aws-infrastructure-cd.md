# GarageFlow Phase 2 AWS Infrastructure and CD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provision the approved ephemeral AWS Academy architecture with Terraform and operate a protected manual deploy/destroy lifecycle that publishes the immutable image, migrates PostgreSQL, rolls out EKS, and proves the public API.

**Architecture:** A one-time S3 bootstrap retains encrypted remote state; an Academy environment composes small network, EKS, RDS, ECR, SNS, and Secrets Manager modules without creating IAM roles. GitHub Actions consumes refreshed Learner Lab credentials, applies Terraform, injects runtime configuration directly into Kubernetes, runs migration before rollout, smokes the service, and destroys all billable resources after the demonstration.

**Tech Stack:** Terraform CLI 1.15.7, HashiCorp AWS provider 6.49.0, Random provider 3.9.0, AWS CLI v2, Amazon EKS Kubernetes 1.36 after regional preflight, RDS PostgreSQL 17, ECR, SNS, Secrets Manager, S3 backend native lockfile, GitHub Actions, Docker, kubectl.

## Global Constraints

- Begin only after the container/Kubernetes checkpoint passes; do not provision AWS while application/image/manifests are failing locally.
- Region is exactly `us-east-1`; credentials come only from `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN`.
- Validate Kubernetes `1.36` is in `STANDARD_SUPPORT` with `aws eks describe-cluster-versions` immediately before apply; expose it as a workflow input so a later standard-support version can be selected without code edits.
- Consume pre-existing Learner Lab EKS cluster/node role ARNs as variables; create no IAM role, policy, attachment, OIDC provider, IRSA, or Pod Identity resource.
- Use one VPC across two availability zones, two public EKS/LoadBalancer subnets with public IPv4, two private RDS subnets, one Internet Gateway, and no NAT Gateway.
- RDS is private and reachable on 5432 only from the EKS cluster/node security-group path.
- EKS uses one managed on-demand node group with exactly min/desired/max 2 and instance type `t3.small`; no Cluster Autoscaler.
- RDS uses PostgreSQL major 17, `db.t3.micro`, Single-AZ, 20 GB `gp2`, no public access, no Enhanced Monitoring, no deletion protection, and no final snapshot in this ephemeral lab.
- ECR uses immutable image tags and `force_delete=true`; deploy tag is the full Git commit SHA.
- SNS uses a standard topic and email subscription; the demonstration cannot pass until the recipient manually confirms it.
- Secrets Manager holds separate database, JWT, bootstrap-admin, and webhook secrets; immediate deletion is allowed only for same-day lab teardown.
- Terraform state contains generated secrets: encrypt/version/block-public-access/version the backend, never print secret outputs, and never upload plan/state artifacts.
- Keep the bootstrap S3 bucket after ordinary environment destroy; final course cleanup of all object versions is a separate explicit act.
- Pin Terraform and providers exactly; commit `.terraform.lock.hcl`, never commit actual tfvars, backend credentials, local state, or plan files.
- Tag every AWS resource with `Project=GarageFlow`, `Phase=2`, `Environment=academy`, owner, and expiration intent.
- Deployment and destruction use `workflow_dispatch`, protected GitHub Environment `aws-academy`, least GitHub token permissions, and shared lifecycle concurrency.
- Install Metrics Server 0.8.1 only from `https://github.com/kubernetes-sigs/metrics-server/releases/download/v0.8.1/components.yaml` after verifying SHA-256 `4a672c4891902573a3ff753cece5de1bf1f55dd053403dfec39df9d1636b7ff1`.
- Delete the Kubernetes Service/namespace and wait for its AWS LoadBalancer finalizer before Terraform destroy.
- Use synthetic data only, refresh credentials immediately before each workflow, and destroy the environment the same day.

---

## File Structure

### Create — backend bootstrap

- `infra/bootstrap/state-backend/providers.tf`
- `infra/bootstrap/state-backend/main.tf`
- `infra/bootstrap/state-backend/variables.tf`
- `infra/bootstrap/state-backend/outputs.tf`
- `infra/bootstrap/state-backend/terraform.tfvars.example`
- `infra/bootstrap/state-backend/backend.tf.example`
- `infra/bootstrap/state-backend/.terraform.lock.hcl`

### Create — reusable modules

- `infra/modules/network/{main.tf,variables.tf,outputs.tf}`
- `infra/modules/eks/{main.tf,variables.tf,outputs.tf}`
- `infra/modules/rds/{main.tf,variables.tf,outputs.tf}`
- `infra/modules/ecr/{main.tf,variables.tf,outputs.tf}`
- `infra/modules/sns/{main.tf,variables.tf,outputs.tf}`
- `infra/modules/secrets/{main.tf,variables.tf,outputs.tf}`

### Create — Academy environment and automation

- `infra/environments/academy/backend.tf`
- `infra/environments/academy/providers.tf`
- `infra/environments/academy/main.tf`
- `infra/environments/academy/variables.tf`
- `infra/environments/academy/outputs.tf`
- `infra/environments/academy/terraform.tfvars.example`
- `infra/environments/academy/.terraform.lock.hcl`
- `.github/workflows/deploy-aws-academy.yml`
- `.github/workflows/destroy-aws-academy.yml`
- `scripts/smoke-aws.sh`
- `scripts/aws/verify-destroy.sh`

### Modify

- `.gitignore` — Terraform state/plan/config exclusions while retaining locks/examples.
- `.github/workflows/quality-gate.yml` — Terraform format/init/validate and workflow syntax.
- `k8s/configmap.yaml` is not rewritten; deploy patches its empty SNS ARN before workloads start.

---

### Task 1: Create and migrate the retained S3 state bootstrap

**Files:**
- Create: all `infra/bootstrap/state-backend/*` files
- Modify: `.gitignore`

**Interfaces:**
- Produces: private/versioned/encrypted S3 bucket name and a partial S3 backend using native `use_lockfile=true`.
- Consumes: temporary AWS credentials and unique bucket name supplied at execution time.

- [ ] **Step 1: Protect Terraform local/sensitive files**

Add:

```gitignore
**/.terraform/
*.tfstate
*.tfstate.*
*.tfplan
crash.log
crash.*.log
override.tf
override.tf.json
*_override.tf
*_override.tf.json
*.auto.tfvars
infra/**/terraform.tfvars
infra/bootstrap/state-backend/backend.tf
```

Do not ignore `.terraform.lock.hcl`, `terraform.tfvars.example`, or `backend.tf.example`.

- [ ] **Step 2: Pin bootstrap Terraform/provider versions**

`providers.tf` contains:

```hcl
terraform {
  required_version = "= 1.15.7"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "= 6.49.0"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = var.tags
  }
}
```

Validate `aws_region == "us-east-1"` and require nonempty `state_bucket_name`, owner, and expiration values.

- [ ] **Step 3: Define the retained S3 bucket**

`main.tf` creates:

```hcl
resource "aws_s3_bucket" "state" {
  bucket = var.state_bucket_name

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_versioning" "state" {
  bucket = aws_s3_bucket.state.id
  versioning_configuration { status = "Enabled" }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "state" {
  bucket = aws_s3_bucket.state.id
  rule {
    apply_server_side_encryption_by_default { sse_algorithm = "AES256" }
  }
}

resource "aws_s3_bucket_public_access_block" "state" {
  bucket                  = aws_s3_bucket.state.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "state" {
  bucket = aws_s3_bucket.state.id
  rule { object_ownership = "BucketOwnerEnforced" }
}
```

Output only bucket name/ARN. The example variables use `us-east-1`, synthetic owner `garageflow-team`, and a future ISO expiration date; they contain no credentials.

- [ ] **Step 4: Define the post-create partial backend**

`backend.tf.example` contains only:

```hcl
terraform {
  backend "s3" {}
}
```

The active `backend.tf` is intentionally ignored because the bucket must exist before S3 initialization.

- [ ] **Step 5: Validate without AWS and generate the provider lock**

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/bootstrap/state-backend fmt -check
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/bootstrap/state-backend init -backend=false
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/bootstrap/state-backend validate
```

Expected: format/validation PASS and `.terraform.lock.hcl` pins AWS 6.49.0.

- [ ] **Step 6: Apply locally, activate S3, and migrate the bootstrap state**

With freshly exported Academy credentials:

```bash
aws sts get-caller-identity
terraform -chdir=infra/bootstrap/state-backend init
terraform -chdir=infra/bootstrap/state-backend apply \
  -var="state_bucket_name=${TF_STATE_BUCKET}" \
  -var="owner=${TF_OWNER}" \
  -var="expires_on=${TF_EXPIRES_ON}"
cp infra/bootstrap/state-backend/backend.tf.example infra/bootstrap/state-backend/backend.tf
terraform -chdir=infra/bootstrap/state-backend init -migrate-state \
  -backend-config="bucket=${TF_STATE_BUCKET}" \
  -backend-config="key=bootstrap/state-backend.tfstate" \
  -backend-config="region=us-east-1" \
  -backend-config="encrypt=true" \
  -backend-config="use_lockfile=true"
```

Expected: migration confirmation succeeds; `aws s3api get-bucket-versioning` reports `Enabled`; public-access-block is all true; no local `terraform.tfstate` remains.

- [ ] **Step 7: Commit bootstrap source and lock only**

```powershell
git add .gitignore infra/bootstrap/state-backend
git status --short
git commit -m "infra(terraform): add encrypted state bootstrap"
```

Expected staged scope excludes active `backend.tf`, local state, and `.terraform`.

---

### Task 2: Build the network and EKS modules

**Files:**
- Create: `infra/modules/network/*`
- Create: `infra/modules/eks/*`

**Interfaces:**
- Network produces VPC ID, two public subnet IDs, two private DB subnet IDs.
- EKS consumes public subnets and supplied role ARNs; produces cluster name/endpoint/CA and primary security-group ID.

- [ ] **Step 1: Define exact network variables and subnets**

Use VPC `10.42.0.0/16`; public CIDRs `10.42.0.0/24`, `10.42.1.0/24`; DB CIDRs `10.42.10.0/24`, `10.42.11.0/24`. Require exactly two distinct AZ names. Public subnets set `map_public_ip_on_launch = true` and tags:

```hcl
"kubernetes.io/role/elb"                     = "1"
"kubernetes.io/cluster/${var.cluster_name}" = "shared"
```

Private DB subnets do not get a default route or public IP assignment.

- [ ] **Step 2: Implement the no-NAT route topology**

Create one VPC with DNS support/hostnames, one Internet Gateway, one public `0.0.0.0/0` route, and associate both public subnets. Do not create NAT, EIP, private default route, VPC endpoint, or bastion resources.

- [ ] **Step 3: Define the managed EKS cluster without IAM creation**

Core resource:

```hcl
resource "aws_eks_cluster" "this" {
  name     = var.cluster_name
  role_arn = var.cluster_role_arn
  version  = var.kubernetes_version

  vpc_config {
    subnet_ids              = var.public_subnet_ids
    endpoint_public_access  = true
    endpoint_private_access = true
    public_access_cidrs     = var.public_access_cidrs
  }

  access_config {
    authentication_mode                         = "API_AND_CONFIG_MAP"
    bootstrap_cluster_creator_admin_permissions = true
  }

  upgrade_policy { support_type = "STANDARD" }
}
```

No `aws_iam_*` resource is allowed anywhere under `infra/`.

- [ ] **Step 4: Add the fixed-size managed node group**

```hcl
resource "aws_eks_node_group" "this" {
  cluster_name    = aws_eks_cluster.this.name
  node_group_name = "${var.cluster_name}-workers"
  node_role_arn   = var.node_role_arn
  subnet_ids      = var.public_subnet_ids
  capacity_type   = "ON_DEMAND"
  instance_types  = ["t3.small"]

  scaling_config {
    min_size     = 2
    desired_size = 2
    max_size     = 2
  }

  update_config { max_unavailable = 1 }
}
```

Output `aws_eks_cluster.this.vpc_config[0].cluster_security_group_id` for RDS ingress.

- [ ] **Step 5: Format/validate modules and commit**

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace/infra hashicorp/terraform:1.15.7 fmt -check -recursive
if (rg -n 'resource\s+"aws_iam_' infra) { exit 1 }
git add infra/modules/network infra/modules/eks
git commit -m "infra(terraform): add academy network and eks modules"
```

Expected: formatted and no IAM resources.

---

### Task 3: Add database, registry, notification, and secret modules

**Files:**
- Create: `infra/modules/rds/*`, `ecr/*`, `sns/*`, `secrets/*`

**Interfaces:**
- Secrets produces a sensitive database password for RDS and four secret ARNs for CD.
- RDS produces endpoint/port/name without credentials.
- ECR produces repository URL; SNS produces topic/subscription identifiers.

- [ ] **Step 1: Generate and store four independent secret sets**

The secrets module pins Random through the environment root and creates:

```text
garageflow/academy/database   JSON: username, database, password
garageflow/academy/jwt        SecretString: 64-char generated value
garageflow/academy/bootstrap  JSON: email, initialPassword, activePassword
garageflow/academy/webhook    SecretString: 64-char generated value
```

Use independent `random_password` resources: DB 32 alphanumeric; JWT/webhook 64 alphanumeric; bootstrap initial and active passwords each 24 characters with special set `!@#%_-`. Every `aws_secretsmanager_secret` uses `recovery_window_in_days = 0`. Output ARNs and the DB password marked `sensitive = true`; never output JWT/bootstrap/webhook values.

- [ ] **Step 2: Implement private Single-AZ RDS**

Create a DB subnet group over both private subnets and a security group with only this ingress:

```hcl
ingress {
  protocol                 = "tcp"
  from_port                = 5432
  to_port                  = 5432
  source_security_group_id = var.eks_security_group_id
}
```

DB instance values:

```hcl
engine                    = "postgres"
engine_version            = "17"
instance_class            = "db.t3.micro"
allocated_storage         = 20
storage_type              = "gp2"
storage_encrypted         = true
multi_az                  = false
publicly_accessible       = false
monitoring_interval       = 0
performance_insights_enabled = false
deletion_protection       = false
skip_final_snapshot       = true
backup_retention_period   = 0
apply_immediately         = true
```

Use database `garageflow`, username `garageflowadmin`, and the sensitive module password.

- [ ] **Step 3: Implement immutable ECR**

Create one repository with `image_tag_mutability = "IMMUTABLE"`, `force_delete = true`, scan-on-push enabled, and AES256 encryption.

- [ ] **Step 4: Implement SNS email subscription**

Create one standard topic and one `protocol = "email"` subscription from the required `notification_email` variable. Set `endpoint_auto_confirms = false`; output enough state for the workflow to detect `PendingConfirmation` and poll for a real ARN.

- [ ] **Step 5: Format and commit regional modules**

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace/infra hashicorp/terraform:1.15.7 fmt -check -recursive
git add infra/modules/rds infra/modules/ecr infra/modules/sns infra/modules/secrets
git commit -m "infra(terraform): add academy data and regional services"
```

---

### Task 4: Compose and statically validate the Academy environment

**Files:**
- Create: all `infra/environments/academy/*` files
- Modify: `.github/workflows/quality-gate.yml`

**Interfaces:**
- Consumes: six modules, role ARNs, confirmed region/version/owner/email.
- Produces: only non-secret outputs required by deployment and destruction.

- [ ] **Step 1: Pin root tooling and partial backend**

`providers.tf` pins:

```hcl
terraform {
  required_version = "= 1.15.7"
  required_providers {
    aws    = { source = "hashicorp/aws",    version = "= 6.49.0" }
    random = { source = "hashicorp/random", version = "= 3.9.0" }
  }
}
```

`backend.tf` contains only `terraform { backend "s3" {} }`. AWS provider uses `var.aws_region` and default tags from project/phase/environment/owner/expiration.

- [ ] **Step 2: Validate every environment input**

Required variables:

```text
aws_region default us-east-1 and only allowed us-east-1
cluster_name default garageflow-academy
kubernetes_version default 1.36
eks_cluster_role_arn and eks_node_role_arn valid ARN strings
owner nonempty
expires_on ISO date string
notification_email valid email-shaped string
bootstrap_admin_email valid email-shaped string
public_access_cidrs default ["0.0.0.0/0"] with lab-only description
```

- [ ] **Step 3: Compose modules without a dependency cycle**

Use the first two available AZs. Order by data flow: network; secrets; EKS; RDS consumes secret DB password and EKS SG; ECR; SNS. The database secret intentionally excludes endpoint, so CD combines RDS output with its stored username/password.

- [ ] **Step 4: Expose deployment outputs without values marked sensitive**

Output:

```text
aws_region, cluster_name, ecr_repository_url
rds_endpoint, rds_port, rds_database
sns_topic_arn
database_secret_arn, jwt_secret_arn, bootstrap_secret_arn, webhook_secret_arn
vpc_id, node_group_name, rds_identifier, ecr_repository_name, sns_topic_name
```

Never output generated secret strings or the complete connection string.

- [ ] **Step 5: Initialize without backend and validate offline**

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/environments/academy init -backend=false
docker run --rm -v "${PWD}:/workspace" -w /workspace hashicorp/terraform:1.15.7 -chdir=infra/environments/academy validate
docker run --rm -v "${PWD}:/workspace" -w /workspace/infra hashicorp/terraform:1.15.7 fmt -check -recursive
```

Expected: PASS and lock file pins AWS 6.49.0/Random 3.9.0.

- [ ] **Step 6: Add Terraform gates to CI**

After checkout, use `hashicorp/setup-terraform@v3` with `terraform_version: 1.15.7`, then run recursive format, bootstrap `init -backend=false`/validate, and Academy `init -backend=false`/validate. No plan/apply and no AWS environment variables in pull-request CI.

- [ ] **Step 7: Commit the composed environment**

```powershell
git add infra/environments/academy .github/workflows/quality-gate.yml
git commit -m "infra(terraform): compose AWS Academy environment"
```

---

### Task 5: Add protected manual deployment

**Files:**
- Create: `.github/workflows/deploy-aws-academy.yml`
- Create: `scripts/smoke-aws.sh`

**Interfaces:**
- Consumes: protected Environment secrets/variables, remote Terraform state, app image/manifests.
- Produces: deployed SHA image, migrated database, ready LoadBalancer URL, and smoke evidence.

- [ ] **Step 1: Define the protected workflow boundary**

Use:

```yaml
on:
  workflow_dispatch:
    inputs:
      confirmation:
        description: Type APPLY to provision billable lab resources
        required: true
        type: string
      kubernetes_version:
        required: true
        default: "1.36"
        type: string

permissions:
  contents: read

concurrency:
  group: aws-academy-lifecycle
  cancel-in-progress: false
```

The only job uses `environment: aws-academy`, rejects confirmation other than `APPLY`, and has a 90-minute timeout.

- [ ] **Step 2: Document exact Environment inputs in the workflow comments**

Secrets:

```text
AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY
AWS_SESSION_TOKEN
TF_STATE_BUCKET
EKS_CLUSTER_ROLE_ARN
EKS_NODE_ROLE_ARN
```

Variables:

```text
TF_OWNER
TF_EXPIRES_ON
SNS_NOTIFICATION_EMAIL
BOOTSTRAP_ADMIN_EMAIL
```

No long-lived GitHub token or AWS key is introduced.

- [ ] **Step 3: Preflight identity, support, quota, and offerings**

Export the three AWS secrets as job environment variables and run:

```bash
aws sts get-caller-identity
test "${AWS_REGION}" = "us-east-1"
STATUS="$(aws eks describe-cluster-versions --region us-east-1 \
  --cluster-versions "${EKS_VERSION}" --version-status STANDARD_SUPPORT \
  --query 'clusterVersions[0].versionStatus' --output text)"
test "${STATUS}" = "STANDARD_SUPPORT"
aws ec2 describe-instance-type-offerings --region us-east-1 \
  --location-type availability-zone --filters Name=instance-type,Values=t3.small
aws rds describe-orderable-db-instance-options --region us-east-1 \
  --engine postgres --db-instance-class db.t3.micro --query 'OrderableDBInstanceOptions[0]'
```

Also count existing running/pending EC2 and DB instances and fail with a clear budget message if adding two nodes would exceed Academy limits.

- [ ] **Step 4: Initialize, plan, and apply remote Terraform state**

Use `hashicorp/setup-terraform@v3` version 1.15.7. Export `TF_VAR_*` from protected inputs, initialize with bucket/key `academy/terraform.tfstate`, region, encryption, and `use_lockfile=true`; validate; create a plan only under `$RUNNER_TEMP`; apply it; never upload it.

- [ ] **Step 5: Build or reuse the immutable SHA image**

Read `ecr_repository_url`. Authenticate with `aws ecr get-login-password`. If `describe-images --image-ids imageTag=${GITHUB_SHA}` succeeds, reuse it; otherwise build and push `${repository}:${GITHUB_SHA}`. Never push `latest`.

- [ ] **Step 6: Configure EKS and install verified Metrics Server**

Update kubeconfig, download the pinned asset to `$RUNNER_TEMP`, verify its exact SHA-256, apply it, wait for Deployment availability, and require `kubectl top nodes` to succeed before HPA rollout.

- [ ] **Step 7: Apply runtime configuration without persisting rendered secrets**

Apply namespace and static ConfigMap, then patch `Integrations__Sns__TopicArn` from Terraform output. Fetch the four Secrets Manager entries with AWS CLI; extract and mask both bootstrap passwords as well as every other sensitive shell value. Build the Npgsql connection string from RDS output plus database secret. Stream only the initial password into application bootstrap configuration:

```bash
kubectl -n garageflow create secret generic garageflow-secrets \
  --from-literal="ConnectionStrings__GarageFlow=${CONNECTION_STRING}" \
  --from-literal="Auth__Jwt__Key=${JWT_KEY}" \
  --from-literal="Auth__BootstrapAdmin__Email=${BOOTSTRAP_EMAIL}" \
  --from-literal="Auth__BootstrapAdmin__Password=${BOOTSTRAP_INITIAL_PASSWORD}" \
  --from-literal="Webhooks__EstimateDecisions__HmacSecret=${WEBHOOK_SECRET}" \
  --from-literal="AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}" \
  --from-literal="AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}" \
  --from-literal="AWS_SESSION_TOKEN=${AWS_SESSION_TOKEN}" \
  --dry-run=client -o yaml | kubectl apply -f -
```

Disable shell tracing around this command. Do not write the YAML to the workspace or artifacts.

- [ ] **Step 8: Gate rollout on the migration Job**

Delete any old fixed-name Job, set its image locally to the SHA, apply, wait up to 10 minutes for `condition=complete`, and print only Job/pod logs on failure. Stop the workflow before Deployment if migration fails.

- [ ] **Step 9: Roll out app, Service, HPA, and refreshed secrets**

Set the Deployment image locally to the SHA; apply Deployment, Service, HPA; force `kubectl rollout restart` after Secret refresh; wait for two ready replicas and rollout completion. Poll Service ingress until it returns a hostname/IP.

- [ ] **Step 10: Require SNS confirmation and run AWS smoke**

Poll `list-subscriptions-by-topic` for up to 10 minutes and fail with an explicit email-confirmation instruction if it remains `PendingConfirmation`. Then invoke `scripts/smoke-aws.sh` with base URL, email, initial password, and active password in environment variables.

The script first tries the active password so reruns work. If that returns `401`, it logs in with the initial password, requires `mustChangePassword=true`, changes to the active password, and logs in again. It then repeats the container smoke's health/OpenAPI/intake/status journey, uses a new valid synthetic CPF/UUID, never echoes tokens/passwords, and returns the created work-order ID for evidence.

- [ ] **Step 11: Validate workflow and commit deploy automation**

```powershell
docker run --rm -v "${PWD}:/repo" -w /repo rhysd/actionlint:1.7.12
git add .github/workflows/deploy-aws-academy.yml scripts/smoke-aws.sh
git commit -m "ci(aws): deploy GarageFlow to Academy"
```

---

### Task 6: Add protected teardown and verify zero billable resources

**Files:**
- Create: `.github/workflows/destroy-aws-academy.yml`
- Create: `scripts/aws/verify-destroy.sh`

**Interfaces:**
- Consumes: same protected credentials/state/variables as deploy.
- Produces: removal evidence for workload LoadBalancer and all Terraform-managed environment resources; retains only bootstrap S3.

- [ ] **Step 1: Require explicit destruction confirmation**

Use `workflow_dispatch` input `confirmation`, exact value `DESTROY`, protected `aws-academy` Environment, same concurrency group, `contents: read`, and 60-minute timeout.

- [ ] **Step 2: Refresh and validate identity/state before mutation**

Run STS, verify `us-east-1`, initialize the existing S3 backend, and run `terraform state list`. Fail before cleanup if state cannot be read; never attempt an untracked manual teardown as the normal path.

- [ ] **Step 3: Remove Kubernetes-owned AWS resources first**

If the cluster exists, update kubeconfig, delete Service `garageflow-api`, wait for its deletion/finalizer, then delete namespace `garageflow` and wait. Poll both ELBv2 and Classic ELB APIs until no LoadBalancer tagged/named for the service remains. This step is required before VPC/subnet/security-group destroy.

- [ ] **Step 4: Destroy the Terraform environment**

Export the same required `TF_VAR_*` values and run:

```bash
terraform -chdir=infra/environments/academy destroy -auto-approve -input=false
```

The ECR module force-deletes SHA images; Secrets Manager uses immediate deletion; RDS skips final snapshot only because this is the approved ephemeral lab.

- [ ] **Step 5: Verify absence by API, not only Terraform output**

`scripts/aws/verify-destroy.sh` queries exact environment names/tags and fails if it finds EKS cluster/node group, running EC2 workers, RDS identifier, ECR repository, SNS topic, four Secrets Manager entries, service LoadBalancer, or GarageFlow VPC. It separately verifies the bootstrap S3 bucket still exists and has versioning enabled.

- [ ] **Step 6: Validate and commit teardown automation**

```powershell
docker run --rm -v "${PWD}:/repo" -w /repo rhysd/actionlint:1.7.12
git add .github/workflows/destroy-aws-academy.yml scripts/aws/verify-destroy.sh
git commit -m "ci(aws): destroy GarageFlow Academy environment"
```

- [ ] **Step 7: Execute the full live lifecycle once**

With newly refreshed credentials and protected approvals:

1. run deploy;
2. confirm the SNS email;
3. capture Terraform outputs without secrets, two EKS nodes, two ready pods, private RDS, immutable ECR SHA, and successful smoke;
4. perform the load/HPA demonstration from the delivery-assets checkpoint;
5. run destroy;
6. retain the successful verification log and bootstrap bucket evidence.

Expected: no billable Phase 2 environment resource remains after the workflow.

---

## Checkpoint Acceptance

The AWS/CD checkpoint is complete only when:

- backend bootstrap is private, encrypted, versioned, lockfile-enabled, remotely migrated, and retained;
- offline Terraform format/validation and CI gates pass with exact version locks;
- Terraform creates no IAM or NAT resources and matches every selected Academy size/availability constraint;
- deploy rejects stale/wrong identity, unsupported EKS version, unavailable instance types, and missing protected inputs before apply;
- one immutable SHA image is pushed/reused and the database Job completes before two-replica rollout;
- Metrics Server, probes, HPA, SNS confirmation, and public smoke succeed;
- rendered Kubernetes secrets/state/plans never enter GitHub artifacts or logs;
- destroy removes Kubernetes-owned LoadBalancer resources before Terraform, then verifies every environment service absent;
- only the retained S3 bootstrap remains and the live lifecycle evidence is captured.
