import base64
import copy
import importlib
import sys
import unittest

from test_infra_contract import ROOT, manifest, producer_outputs
from test_ingress_contract_v2 import ingress_v2

sys.path.insert(0, str(ROOT / "scripts"))


class Phase3InputsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = importlib.import_module("phase3_inputs")

    def contracts(self):
        return dict(platform=manifest(), database=manifest("database", outputs=producer_outputs()["database"]), ingress=ingress_v2())

    def test_contracts_are_versioned_and_match_protected_account(self):
        contracts = self.contracts()
        self.module.validate_inputs(contracts, "homologation", "123456789012")
        for env, account in [("production", "123456789012"), ("homologation", "999999999999"), ("homologation", "bad")]:
            with self.assertRaises(ValueError):
                self.module.validate_inputs(contracts, env, account)
        contracts["ingress"]["schemaVersion"] = "1.0"
        with self.assertRaises(ValueError):
            self.module.validate_inputs(contracts, "homologation", "123456789012")

    def test_connection_string_quotes_untrusted_secret_values(self):
        database = producer_outputs()["database"]
        secrets = self.secrets()
        secrets["database"]["password"] = 'a;Host=attacker;Password="quoted"'
        secret = self.module.runtime_secret(database, secrets, self.credentials())
        decoded = {key: base64.b64decode(value, validate=True).decode() for key, value in secret["data"].items()}
        self.assertIn('Password="a;Host=attacker;Password=""quoted"""', decoded["ConnectionStrings__GarageFlow"])
        self.assertEqual(secrets["internal"], decoded["Auth__Internal__Key"])
        self.assertNotIn("stringData", secret)
        self.assertNotIn("activePassword", str(secret))

    def test_ecr_registry_must_match_protected_account_region_and_dns_suffix(self):
        for repository in ["999999999999.dkr.ecr.us-east-1.amazonaws.com/garageflow",
                           "123456789012.dkr.ecr.us-west-2.amazonaws.com/garageflow",
                           "123456789012.dkr.ecr.us-east-1.amazonaws.com.attacker.invalid/garageflow",
                           "123456789012.dkr.ecr.us-east-1.amazonaws.com/garageflow:latest"]:
            contracts = self.contracts()
            contracts["platform"]["outputs"]["ecrRepositoryUrl"] = repository
            with self.subTest(repository=repository), self.assertRaises(ValueError):
                self.module.validate_inputs(contracts, "homologation", "123456789012")

    def test_missing_or_invalid_active_password_fails_before_rendering_secret(self):
        for value in [None, "", "bad\npassword", "bad\x7fpassword", "Initial.Password#123"]:
            secrets = self.secrets()
            if value is None:
                del secrets["bootstrap"]["activePassword"]
            else:
                secrets["bootstrap"]["activePassword"] = value
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.module.runtime_secret(producer_outputs()["database"], secrets, self.credentials())

    def secrets(self):
        return dict(database=dict(username="garageflow", password="strong-password", database="garageflow"), jwt="j" * 64,
                    internal="i" * 64, webhook="w" * 64,
                    bootstrap=dict(email="admin@example.com", initialPassword="Initial.Password#123", activePassword="Active.Password#123"))

    def credentials(self):
        return dict(AWS_ACCESS_KEY_ID="temporary-access", AWS_SECRET_ACCESS_KEY="temporary-secret", AWS_SESSION_TOKEN="temporary-session")

    def test_refuses_equal_short_or_placeholder_keys_and_wrong_database(self):
        for mutate in [lambda s: s.update(internal=s["jwt"]), lambda s: s.update(jwt="short"),
                       lambda s: s.update(internal="__SET_ME_" + "x" * 50),
                       lambda s: s["database"].update(database="other"), lambda s: s["bootstrap"].update(initialPassword="")]:
            secrets = self.secrets()
            mutate(secrets)
            with self.assertRaises(ValueError):
                self.module.runtime_secret(producer_outputs()["database"], secrets, self.credentials())

    def network(self):
        contracts = self.contracts()
        outputs = contracts["platform"]["outputs"]
        alb_arn = "arn:aws:elasticloadbalancing:us-east-1:123456789012:loadbalancer/app/garageflow/0123456789abcdef"
        network = dict(
            cluster={"resourcesVpcConfig": {"vpcId": outputs["vpcId"], "clusterSecurityGroupId": outputs["clusterSecurityGroupId"]}},
            listener={"LoadBalancerArn": alb_arn, "Protocol": "HTTP", "Port": 80,
                      "DefaultActions": [{"Type": "forward", "TargetGroupArn": "target-group"}]},
            alb={"LoadBalancerArn": alb_arn, "VpcId": outputs["vpcId"], "Scheme": "internal", "DNSName": "internal-garageflow-123.us-east-1.elb.amazonaws.com", "SecurityGroups": ["sg-alb"]},
            groups=[{"GroupId": "sg-alb", "VpcId": outputs["vpcId"], "IpPermissions": [{"IpProtocol": "tcp", "FromPort": 80, "ToPort": 80,
                     "UserIdGroupPairs": [{"GroupId": contracts["ingress"]["outputs"][key]} for key in ["authenticationSecurityGroupId", "vpcLinkSecurityGroupId"]]}],
                     "IpPermissionsEgress": [{"IpProtocol": "tcp", "FromPort": 30080, "ToPort": 30080, "UserIdGroupPairs": [{"GroupId": outputs["clusterSecurityGroupId"]}]}]},
                    {"GroupId": outputs["clusterSecurityGroupId"], "VpcId": outputs["vpcId"], "IpPermissions": [{"IpProtocol": "tcp", "FromPort": 30080, "ToPort": 30080, "UserIdGroupPairs": [{"GroupId": "sg-alb"}]}]}],
            target={"VpcId": outputs["vpcId"], "Port": 30080, "Protocol": "HTTP", "TargetType": "instance"},
            database={"PubliclyAccessible": False, "DBSubnetGroup": {"VpcId": outputs["vpcId"]}, "VpcSecurityGroups": [{"VpcSecurityGroupId": contracts["database"]["outputs"]["databaseSecurityGroupId"]}]},
        )
        return contracts, network

    def test_alb_egress_is_restricted_to_node_group_on_application_port(self):
        for permission in [{"IpProtocol": "-1", "IpRanges": [{"CidrIp": "0.0.0.0/0"}]},
                           {"IpProtocol": "tcp", "FromPort": 30080, "ToPort": 30080, "Ipv6Ranges": [{"CidrIpv6": "::/0"}]},
                           {"IpProtocol": "tcp", "FromPort": 30080, "ToPort": 30080, "PrefixListIds": [{"PrefixListId": "pl-any"}]},
                           {"IpProtocol": "tcp", "FromPort": 30080, "ToPort": 30080, "UserIdGroupPairs": [{"GroupId": "sg-other"}]},
                           {"IpProtocol": "tcp", "FromPort": 80, "ToPort": 80}, None]:
            contracts, network = self.network()
            network["groups"][0]["IpPermissionsEgress"] = [permission] if permission else []
            with self.subTest(permission=permission), self.assertRaises(ValueError):
                self.module.validate_network(contracts, network)

    def test_network_identity_requires_private_alb_and_exact_caller_groups(self):
        contracts, network = self.network()
        self.module.validate_network(contracts, network)
        for mutate in [lambda n: n["alb"].update(Scheme="internet-facing"), lambda n: n["alb"].update(DNSName="wrong.example.com"),
                       lambda n: n["target"].update(Port=80), lambda n: n["cluster"]["resourcesVpcConfig"].update(vpcId="wrong"),
                       lambda n: n["groups"][0]["IpPermissions"][0].update(IpRanges=[{"CidrIp": "0.0.0.0/0"}]),
                       lambda n: n["database"].update(PubliclyAccessible=True)]:
            candidate = copy.deepcopy(network)
            mutate(candidate)
            with self.subTest(network=candidate), self.assertRaises(ValueError):
                self.module.validate_network(contracts, candidate)

    def test_runner_access_preserves_configured_scopes_and_rejects_broad_access(self):
        self.assertEqual(["203.0.113.10/32", "8.8.8.8/32"], self.module.runner_access(["203.0.113.10/32"], "8.8.8.8"))
        for cidrs, address in [([], "10.0.0.1"), (["0.0.0.0/0"], "8.8.8.8"), (["bad"], "8.8.8.8")]:
            with self.assertRaises(ValueError):
                self.module.runner_access(cidrs, address)
