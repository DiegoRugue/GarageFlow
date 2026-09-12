"""Validate Phase 3 deployment identity and prepare private Kubernetes inputs."""

import base64
import ipaddress
import re
from urllib.parse import urlsplit

from infra_contract import validate_contract


def validate_inputs(contracts, environment, account):
    if not re.fullmatch(r"[0-9]{12}", account):
        raise ValueError("Invalid protected AWS account")
    for producer, version in [("platform", "1.0"), ("database", "1.0"), ("ingress", "2.0")]:
        contract = validate_contract(contracts[producer], producer, environment, version)
        for value in contract["outputs"].values():
            if isinstance(value, str) and value.startswith("arn:"):
                parts = value.split(":")
                if parts[3] != "us-east-1" or parts[4] != account:
                    raise ValueError("Contract account or region mismatch")
    ecr_repository(contracts["platform"]["outputs"]["ecrRepositoryUrl"], account)


def ecr_repository(url, account, region="us-east-1"):
    registry = re.escape(f"{account}.dkr.ecr.{region}.amazonaws.com")
    match = re.fullmatch(registry + r"/([a-z0-9]+(?:[._/-][a-z0-9]+)*)", url)
    if not match or not 2 <= len(match[1]) <= 256:
        raise ValueError("ECR repository must belong to the protected account and region")
    return match[1]


def runner_access(configured, address):
    runner = ipaddress.ip_address(address.strip())
    if runner.version != 4 or not runner.is_global or not isinstance(configured, list):
        raise ValueError("A public runner IPv4 and configured CIDR list are required")
    for value in configured:
        network = ipaddress.ip_network(value, strict=True)
        if network.version != 4 or network.prefixlen == 0:
            raise ValueError("EKS access must use scoped IPv4 networks")
    return list(dict.fromkeys([*configured, f"{runner}/32"]))


def validate_network(contracts, network):
    platform, ingress, database = (contracts[name]["outputs"] for name in ("platform", "ingress", "database"))
    vpc = platform["vpcId"]
    cluster = network["cluster"]["resourcesVpcConfig"]
    alb, listener, target = (network[name] for name in ("alb", "listener", "target"))
    db = network["database"]
    if not (
        cluster["vpcId"] == vpc and cluster["clusterSecurityGroupId"] == platform["clusterSecurityGroupId"]
        and alb["VpcId"] == vpc and alb["Scheme"] == "internal"
        and alb["LoadBalancerArn"] == listener["LoadBalancerArn"]
        and urlsplit(ingress["internalApiBaseUrl"]).hostname == alb["DNSName"]
        and listener["Protocol"].lower() == ingress["transport"]
        and listener["Port"] == (80 if ingress["transport"] == "http" else 443)
        and target["VpcId"] == vpc and target["Port"] == 30080 and target["Protocol"] == "HTTP" and target["TargetType"] == "instance"
        and not db["PubliclyAccessible"] and db["DBSubnetGroup"]["VpcId"] == vpc
        and database["databaseSecurityGroupId"] in [group["VpcSecurityGroupId"] for group in db["VpcSecurityGroups"]]
    ):
        raise ValueError("Live private network does not match deployment contracts")
    groups = {group["GroupId"]: group for group in network["groups"]}
    if any(group["VpcId"] != vpc for group in groups.values()):
        raise ValueError("Security group VPC mismatch")
    allowed_callers = {ingress["authenticationSecurityGroupId"], ingress["vpcLinkSecurityGroupId"]}
    actual_callers = set()
    actual_destinations = set()
    for group_id in alb["SecurityGroups"]:
        for permission in groups[group_id]["IpPermissions"]:
            if (permission.get("IpRanges") or permission.get("Ipv6Ranges") or permission.get("PrefixListIds")
                    or permission.get("IpProtocol") != "tcp" or permission.get("FromPort") != listener["Port"] or permission.get("ToPort") != listener["Port"]):
                raise ValueError("ALB has an unexpected ingress rule")
            actual_callers.update(pair["GroupId"] for pair in permission.get("UserIdGroupPairs", []))
        for permission in groups[group_id].get("IpPermissionsEgress", []):
            if (permission.get("IpRanges") or permission.get("Ipv6Ranges") or permission.get("PrefixListIds")
                    or permission.get("IpProtocol") != "tcp" or permission.get("FromPort") != 30080 or permission.get("ToPort") != 30080):
                raise ValueError("ALB has an unexpected egress rule")
            actual_destinations.update(pair["GroupId"] for pair in permission.get("UserIdGroupPairs", []))
    if actual_destinations != {platform["clusterSecurityGroupId"]}:
        raise ValueError("ALB egress must target only the application node group")
    if actual_callers != allowed_callers:
        raise ValueError("ALB caller groups do not match ingress metadata")
    node_callers = set()
    for permission in groups[platform["clusterSecurityGroupId"]]["IpPermissions"]:
        if permission["IpProtocol"] == "-1" or (permission["IpProtocol"] == "tcp" and permission["FromPort"] <= 30080 <= permission["ToPort"]):
            if permission.get("IpRanges") or permission.get("Ipv6Ranges") or permission.get("PrefixListIds"):
                raise ValueError("NodePort must not accept CIDR ingress")
            node_callers.update(pair["GroupId"] for pair in permission.get("UserIdGroupPairs", []))
    if not set(alb["SecurityGroups"]).issubset(node_callers) or node_callers - {*alb["SecurityGroups"], platform["clusterSecurityGroupId"]}:
        raise ValueError("NodePort is not restricted to its ALB and cluster")


def _required(value):
    if not isinstance(value, str) or not value or any(ord(char) < 32 or ord(char) == 127 for char in value):
        raise ValueError("A required secret value is invalid")
    return value


def _quoted(value):
    return '"' + _required(value).replace('"', '""') + '"'


def runtime_secret(database, secrets, credentials):
    for name in ("jwt", "internal", "webhook"):
        value = _required(secrets[name])
        if len(value.encode("utf-8")) < 32 or any(marker in value.lower() for marker in ("set_me", "<set-me>", "placeholder", "replace", "example", "changeme", "your-secret")):
            raise ValueError("Runtime signing keys must be strong non-placeholder secrets")
    if secrets["jwt"] == secrets["internal"]:
        raise ValueError("User and service signing keys must be different")
    db, bootstrap = secrets["database"], secrets["bootstrap"]
    active_password = _required(bootstrap.get("activePassword"))
    if active_password == _required(bootstrap.get("initialPassword")):
        raise ValueError("Bootstrap active and initial passwords must differ")
    if db["database"] != database["databaseName"]:
        raise ValueError("Database secret does not match metadata")
    connection = (f'Host={_quoted(database["databaseHost"])};Port={database["databasePort"]};'
                  f'Database={_quoted(db["database"])};Username={_quoted(db["username"])};Password={_quoted(db["password"])}')
    values = {
        "ConnectionStrings__GarageFlow": connection,
        "Auth__Jwt__Key": secrets["jwt"], "Auth__Internal__Key": secrets["internal"],
        "Auth__BootstrapAdmin__Email": _required(bootstrap["email"]),
        "Auth__BootstrapAdmin__Password": _required(bootstrap["initialPassword"]),
        "Webhooks__EstimateDecisions__HmacSecret": secrets["webhook"],
        **{key: _required(credentials[key]) for key in ("AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN")},
    }
    return {"apiVersion": "v1", "kind": "Secret", "metadata": {"name": "garageflow-secrets", "namespace": "garageflow"},
            "type": "Opaque", "data": {key: base64.b64encode(value.encode("utf-8")).decode("ascii") for key, value in values.items()}}
