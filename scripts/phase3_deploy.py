#!/usr/bin/env python3
"""Deploy the application using validated Phase 3 metadata and private inputs."""

import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
import urllib.request

from infra_contract import load_contract
from phase3_inputs import ecr_repository, runner_access, runtime_secret, validate_inputs, validate_network

ROOT = Path(__file__).resolve().parents[1]
METRICS_SHA256 = "4a672c4891902573a3ff753cece5de1bf1f55dd053403dfec39df9d1636b7ff1"
LEASE_FILE_NAME = "garageflow-phase3-eks-lease.json"
LEGACY_ANNOTATION = "kubectl.kubernetes.io/last-applied-configuration"
LEGACY_ANNOTATION_TEMPLATE = f'{{{{with .metadata.annotations}}}}{{{{if index . "{LEGACY_ANNOTATION}"}}}}present{{{{end}}}}{{{{end}}}}'
REQUIRED = ("AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN", "AWS_REGION", "TF_STATE_BUCKET",
            "EXPECTED_AWS_ACCOUNT_ID", "DEPLOY_ENVIRONMENT", "GITHUB_REF", "GITHUB_SHA", "RUNNER_TEMP")


class DeploymentError(ValueError):
    """An operation failed; captured subprocess output may contain private values."""


def run(arguments, payload=None, environment=None, timeout=900):
    result = subprocess.run(arguments, input=payload, text=True, capture_output=True, cwd=ROOT,
                            env=environment, timeout=timeout, check=False)
    if result.returncode:
        raise DeploymentError(f"{Path(arguments[0]).name} operation failed; private command output was suppressed")
    return result.stdout


def aws(*arguments, timeout=900):
    return json.loads(run(["aws", *arguments, "--output", "json"], timeout=timeout))


def settings(environment):
    if any(not environment.get(name) for name in REQUIRED):
        raise DeploymentError("Required protected deployment inputs are missing")
    expected = {"refs/heads/main": "production", "refs/heads/develop": "homologation"}.get(environment["GITHUB_REF"])
    if expected is None or environment["DEPLOY_ENVIRONMENT"] != expected or environment["AWS_REGION"] != "us-east-1":
        raise DeploymentError("Deployment requires the matching protected branch, environment and region")
    if not re.fullmatch(r"[0-9a-f]{40}", environment["GITHUB_SHA"]) or not re.fullmatch(r"[0-9]{12}", environment["EXPECTED_AWS_ACCOUNT_ID"]):
        raise DeploymentError("Invalid deployment commit or expected AWS account")
    if not re.fullmatch(r"[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]", environment["TF_STATE_BUCKET"]):
        raise DeploymentError("Invalid protected state bucket")
    return dict(environment)


def inspect_network(contracts):
    platform, ingress, database = (contracts[key]["outputs"] for key in ("platform", "ingress", "database"))
    cluster = aws("eks", "describe-cluster", "--name", platform["clusterName"])["cluster"]
    listener = aws("elbv2", "describe-listeners", "--listener-arns", ingress["listenerArn"])["Listeners"][0]
    alb = aws("elbv2", "describe-load-balancers", "--load-balancer-arns", listener["LoadBalancerArn"])["LoadBalancers"][0]
    actions = listener["DefaultActions"]
    if len(actions) != 1 or actions[0]["Type"] != "forward" or not actions[0].get("TargetGroupArn"):
        raise DeploymentError("Private listener must forward to exactly one application target group")
    target_arn = actions[0]["TargetGroupArn"]
    target = aws("elbv2", "describe-target-groups", "--target-group-arns", target_arn)["TargetGroups"][0]
    ids = list(dict.fromkeys([*alb["SecurityGroups"], platform["clusterSecurityGroupId"], ingress["authenticationSecurityGroupId"], ingress["vpcLinkSecurityGroupId"]]))
    groups = aws("ec2", "describe-security-groups", "--group-ids", *ids)["SecurityGroups"]
    instances = aws("rds", "describe-db-instances")["DBInstances"]
    matching = [db for db in instances if db.get("Endpoint", {}).get("Address") == database["databaseHost"] and db["Endpoint"]["Port"] == database["databasePort"]]
    if len(matching) != 1:
        raise DeploymentError("Database endpoint cannot be verified in the active AWS account")
    validate_network(contracts, dict(cluster=cluster, listener=listener, alb=alb, target=target, groups=groups, database=matching[0]))
    return target_arn


def read_secrets(contracts):
    platform = contracts["platform"]["outputs"]
    arns = {"database": contracts["database"]["outputs"]["databaseSecretArn"],
            "jwt": platform["jwtSecretArn"], "internal": platform["internalAuthSecretArn"],
            "bootstrap": platform["bootstrapSecretArn"], "webhook": platform["webhookSecretArn"]}
    result = {name: aws("secretsmanager", "get-secret-value", "--secret-id", arn)["SecretString"] for name, arn in arns.items()}
    for name in ("database", "bootstrap"):
        result[name] = json.loads(result[name])
    return result


def apply(document):
    kind = document.get("kind")
    if kind == "Secret":
        if "stringData" in document or not isinstance(document.get("data"), dict):
            raise DeploymentError("Kubernetes Secrets require base64 data for server-side apply")
        metadata = document.get("metadata", {})
        namespace, name = metadata.get("namespace"), metadata.get("name")
        if not namespace or not name:
            raise DeploymentError("Kubernetes Secret identity is missing")
        run(["kubectl", "apply", "--server-side", "--field-manager=garageflow-phase3", "--force-conflicts", "-f", "-"],
            payload=json.dumps(document))
        run(["kubectl", "-n", namespace, "annotate", "secret", name, f"{LEGACY_ANNOTATION}-"])
        present = run(["kubectl", "-n", namespace, "get", "secret", name, "-o",
                       f"go-template={LEGACY_ANNOTATION_TEMPLATE}"])
        if present.strip():
            raise DeploymentError("Legacy client-side Secret annotation remains after takeover")
        return

    prepared = json.loads(json.dumps(document))
    if kind == "Deployment":
        metadata = prepared.get("metadata", {})
        namespace, name = metadata.get("namespace"), metadata.get("name")
        if not namespace or not name:
            raise DeploymentError("Kubernetes Deployment identity is missing")
        replicas = run(["kubectl", "-n", namespace, "get", "deployment", name, "--ignore-not-found", "-o",
                        "jsonpath={.spec.replicas}"]).strip()
        if replicas:
            if not replicas.isdigit():
                raise DeploymentError("Live Deployment replica count is invalid")
            prepared.setdefault("spec", {})["replicas"] = int(replicas)
    run(["kubectl", "apply", "-f", "-"], payload=json.dumps(prepared))


def _protected_cidrs(values):
    raw = values.get("EKS_PUBLIC_ACCESS_CIDRS")
    configured = [] if raw is None or not raw.strip() else json.loads(raw)
    if not isinstance(configured, list):
        raise DeploymentError("Protected EKS access CIDRs must be a JSON list")
    result = []
    for value in configured:
        network = ipaddress.ip_network(value, strict=True)
        if network.version != 4 or network.prefixlen == 0:
            raise DeploymentError("EKS access must use scoped IPv4 networks")
        result.append(str(network))
    return list(dict.fromkeys(result))


def _lease_path(values):
    temporary_root = Path(values["RUNNER_TEMP"]).resolve(strict=True)
    return temporary_root / LEASE_FILE_NAME


def _access_config(cidrs):
    if cidrs:
        return {"endpointPublicAccess": True, "endpointPrivateAccess": True, "publicAccessCidrs": cidrs}
    return {"endpointPublicAccess": False, "endpointPrivateAccess": True}


def _update_cluster_access(cluster_name, config, retry=False):
    deadline = time.monotonic() + 600
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise DeploymentError("Timed out configuring scoped EKS access")
        try:
            update = aws("eks", "update-cluster-config", "--name", cluster_name, "--resources-vpc-config",
                         json.dumps(config), "--cli-connect-timeout", "10", "--cli-read-timeout", "30",
                         timeout=min(60, remaining))["update"]["id"]
            while True:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise DeploymentError("Timed out configuring scoped EKS access")
                status = aws("eks", "describe-update", "--name", cluster_name, "--update-id", update,
                             "--cli-connect-timeout", "10", "--cli-read-timeout", "30",
                             timeout=min(60, remaining))["update"]["status"]
                if status == "Successful":
                    return
                if status in ("Failed", "Cancelled"):
                    raise DeploymentError("Scoped EKS access update failed")
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise DeploymentError("Timed out configuring scoped EKS access")
                time.sleep(min(10, remaining))
        except (DeploymentError, subprocess.TimeoutExpired):
            if not retry:
                raise
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise DeploymentError("Timed out restoring protected EKS access") from None
            time.sleep(min(10, remaining))


def restore_cluster_access(values):
    marker = _lease_path(values)
    if not marker.exists():
        return
    lease = json.loads(marker.read_text(encoding="utf-8"))
    if set(lease) != {"clusterName", "protectedCidrs"} or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,99}", lease["clusterName"]):
        raise DeploymentError("EKS access lease marker is invalid")
    protected = _protected_cidrs({"EKS_PUBLIC_ACCESS_CIDRS": json.dumps(lease["protectedCidrs"])})
    _update_cluster_access(lease["clusterName"], _access_config(protected), retry=True)
    marker.unlink()


def configure_cluster(platform, values, workspace):
    restore_cluster_access(values)
    with urllib.request.urlopen("https://checkip.amazonaws.com", timeout=10) as response:
        address = response.read(128).decode("ascii")
    protected = _protected_cidrs(values)
    cidrs = runner_access(protected, address)
    _lease_path(values).write_text(json.dumps({"clusterName": platform["clusterName"], "protectedCidrs": protected}), encoding="utf-8")
    _update_cluster_access(platform["clusterName"], _access_config(cidrs))
    os.environ["KUBECONFIG"] = str(workspace / "kubeconfig")
    run(["aws", "eks", "update-kubeconfig", "--name", platform["clusterName"], "--kubeconfig", os.environ["KUBECONFIG"]])
    run(["kubectl", "wait", "--for=condition=Ready", "nodes", "--all", "--timeout=5m"])


def install_metrics(workspace):
    asset = workspace / "metrics-server.yaml"
    with urllib.request.urlopen("https://github.com/kubernetes-sigs/metrics-server/releases/download/v0.8.1/components.yaml", timeout=30) as response:
        content = response.read(1_000_000)
    if hashlib.sha256(content).hexdigest() != METRICS_SHA256:
        raise DeploymentError("Metrics Server release checksum mismatch")
    asset.write_bytes(content)
    run(["kubectl", "apply", "-f", str(asset)])
    run(["kubectl", "-n", "kube-system", "rollout", "status", "deployment/metrics-server", "--timeout=5m"])
    deadline = time.monotonic() + 180
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise DeploymentError("Metrics API did not serve node metrics within 180 seconds")
        try:
            run(["kubectl", "--request-timeout=10s", "top", "nodes"], timeout=min(15, remaining))
            return
        except (DeploymentError, subprocess.TimeoutExpired):
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise DeploymentError("Metrics API did not serve node metrics within 180 seconds")
            time.sleep(min(10, remaining))


def verify_ecr_repository(platform, values):
    url = platform["ecrRepositoryUrl"]
    name = ecr_repository(url, values["EXPECTED_AWS_ACCOUNT_ID"], values["AWS_REGION"])
    repositories = aws("ecr", "describe-repositories", "--registry-id", values["EXPECTED_AWS_ACCOUNT_ID"],
                       "--repository-names", name).get("repositories", [])
    if len(repositories) != 1 or repositories[0].get("repositoryUri") != url:
        raise DeploymentError("Live ECR repository does not match protected deployment metadata")
    return url


def deploy_workload(contracts, values, secrets, workspace):
    platform = contracts["platform"]["outputs"]
    repository = verify_ecr_repository(platform, values)
    image = f'{repository}:{values["GITHUB_SHA"]}'
    registry = repository.split("/")[0]
    password = run(["aws", "ecr", "get-login-password"])
    os.environ["DOCKER_CONFIG"] = str(workspace / "docker")
    run(["docker", "login", "--username", "AWS", "--password-stdin", registry], payload=password)
    run(["docker", "build", "--platform", "linux/amd64", "-t", image, "."])
    run(["docker", "push", image])
    rendered = run(["kubectl", "kustomize", "k8s/phase3", "--load-restrictor", "LoadRestrictionsNone"])
    objects = json.loads(run(["kubectl", "create", "--dry-run=client", "--validate=false", "-f", "-", "-o", "json"], payload=rendered))["items"]
    by_kind = {item["kind"]: item for item in objects}
    by_kind["ConfigMap"]["data"]["Integrations__Sns__TopicArn"] = platform["snsTopicArn"]
    by_kind["Deployment"]["spec"]["template"]["spec"]["containers"][0]["image"] = image
    # A new pod template also reloads changed Secrets after an Academy session refresh.
    by_kind["Deployment"]["spec"]["template"].setdefault("metadata", {}).setdefault("annotations", {})["garageflow/deployment"] = str(time.time_ns())
    apply(by_kind["Namespace"])
    apply(runtime_secret(contracts["database"]["outputs"], secrets, values))
    apply(by_kind["ConfigMap"])
    migration = run(["kubectl", "set", "image", "--local", "-f", "k8s/migration-job.yaml", f"migration={image}", "-o", "json"])
    run(["kubectl", "-n", "garageflow", "delete", "job", "garageflow-migration", "--ignore-not-found", "--wait=true"])
    apply(json.loads(migration))
    run(["kubectl", "-n", "garageflow", "wait", "--for=condition=complete", "job/garageflow-migration", "--timeout=10m"])
    for kind in ("Service", "Deployment", "HorizontalPodAutoscaler"):
        apply(by_kind[kind])
    run(["kubectl", "-n", "garageflow", "rollout", "status", "deployment/garageflow-api", "--timeout=10m"])


def smoke_private_application(secrets, values):
    bootstrap = secrets["bootstrap"]
    env = dict(os.environ, BASE_URL="http://127.0.0.1:18080", BOOTSTRAP_EMAIL=bootstrap["email"],
               BOOTSTRAP_INITIAL_PASSWORD=bootstrap["initialPassword"], BOOTSTRAP_ACTIVE_PASSWORD=bootstrap["activePassword"])
    process = subprocess.Popen(["kubectl", "-n", "garageflow", "port-forward", "service/garageflow-api", "18080:80", "--address=127.0.0.1"],
                               cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        # Existing smoke has a bounded readiness retry and changes only synthetic study data.
        run(["bash", "scripts/smoke-aws.sh"], environment=env, timeout=600)
        if process.poll() is not None:
            raise DeploymentError("Private smoke tunnel terminated unexpectedly")
    finally:
        process.terminate()
        process.wait(timeout=10)


def deploy(values, workspace):
    account = aws("sts", "get-caller-identity")["Account"]
    if account != values["EXPECTED_AWS_ACCOUNT_ID"]:
        raise DeploymentError("Live AWS account differs from protected deployment metadata")
    contracts = {}
    for producer, version in [("platform", 1), ("database", 1), ("ingress", 2)]:
        path = workspace / f"{producer}.json"
        key = f'contracts/v{version}/{values["DEPLOY_ENVIRONMENT"]}/{producer}.json'
        run(["aws", "s3", "cp", f's3://{values["TF_STATE_BUCKET"]}/{key}', str(path), "--only-show-errors"])
        contracts[producer] = load_contract(str(path), producer, values["DEPLOY_ENVIRONMENT"], f"{version}.0")
    validate_inputs(contracts, values["DEPLOY_ENVIRONMENT"], account)
    target = inspect_network(contracts)
    secrets = read_secrets(contracts)
    # Validate everything before applying any workload or Kubernetes Secret.
    runtime_secret(contracts["database"]["outputs"], secrets, values)
    print("Deployment contracts, runtime inputs and live private network verified.", flush=True)
    try:
        configure_cluster(contracts["platform"]["outputs"], values, workspace)
        install_metrics(workspace)
        deploy_workload(contracts, values, secrets, workspace)
        health = aws("elbv2", "describe-target-health", "--target-group-arn", target)["TargetHealthDescriptions"]
        if not health:
            raise DeploymentError("No worker targets registered with the private ALB")
        run(["aws", "elbv2", "wait", "target-in-service", "--target-group-arn", target])
        smoke_private_application(secrets, values)
    finally:
        restore_cluster_access(values)
    print("Application migration, rollout, private ALB targets and application smoke passed. Gateway acceptance is a separate final check.", flush=True)


def main(arguments=()):
    try:
        values = settings(os.environ)
        if arguments == ["--cleanup-cluster-access"]:
            account = aws("sts", "get-caller-identity")["Account"]
            if account != values["EXPECTED_AWS_ACCOUNT_ID"]:
                raise DeploymentError("Live AWS account differs from protected deployment metadata")
            restore_cluster_access(values)
            return 0
        if arguments:
            raise DeploymentError("Unsupported deployment arguments")
        temporary_root = Path(values["RUNNER_TEMP"]).resolve(strict=True)
        os.umask(0o077)
        with tempfile.TemporaryDirectory(prefix="garageflow-phase3-", dir=temporary_root) as directory:
            workspace = Path(directory).resolve(strict=True)
            if not workspace.is_relative_to(temporary_root):
                raise DeploymentError("Deployment workspace escaped trusted temporary storage")
            deploy(values, workspace)
        return 0
    except (ValueError, KeyError, TypeError, IndexError, OSError, subprocess.SubprocessError) as error:
        # Never render arbitrary provider output, secret JSON or exception details.
        print(f"Phase 3 application deployment failed ({type(error).__name__}); verify prerequisite contracts, access and resource health.", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
