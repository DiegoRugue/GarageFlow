import importlib
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
from contextlib import redirect_stderr
from unittest.mock import patch

from test_infra_contract import ROOT, manifest, producer_outputs, trusted_temp_directory
from test_ingress_contract_v2 import ingress_v2
import test_phase3_inputs

sys.path.insert(0, str(ROOT / "scripts"))


class Phase3DeploymentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = importlib.import_module("phase3_deploy")

    def test_command_errors_never_include_sensitive_output_or_stdin(self):
        result = subprocess.CompletedProcess(["kubectl"], 1, "PRIVATE-VALUE", "PRIVATE-VALUE")
        with patch.object(self.module.subprocess, "run", return_value=result):
            with self.assertRaises(self.module.DeploymentError) as error:
                self.module.run(["kubectl", "apply", "-f", "-"], payload="PRIVATE-VALUE")
        self.assertNotIn("PRIVATE-VALUE", str(error.exception))

    def test_untrusted_ref_or_missing_credentials_never_calls_cloud(self):
        with patch.dict(os.environ, {"GITHUB_REF": "refs/pull/1/merge"}, clear=True), patch.object(self.module, "run") as command:
            with redirect_stderr(io.StringIO()):
                self.assertEqual(2, self.module.main())
            command.assert_not_called()

    def test_protected_inputs_require_matching_branch_and_full_sha(self):
        env = {name: "test" for name in self.module.REQUIRED}
        env.update(GITHUB_REF="refs/heads/main", DEPLOY_ENVIRONMENT="homologation", GITHUB_SHA="a" * 40,
                   EXPECTED_AWS_ACCOUNT_ID="123456789012", AWS_REGION="us-east-1", TF_STATE_BUCKET="test-state-bucket")
        with self.assertRaises(ValueError):
            self.module.settings(env)
        env["DEPLOY_ENVIRONMENT"] = "production"
        self.assertEqual("production", self.module.settings(env)["DEPLOY_ENVIRONMENT"])
        env["GITHUB_SHA"] = "short"
        with self.assertRaises(ValueError):
            self.module.settings(env)

    def test_protected_eks_cidrs_are_optional(self):
        env = {name: "test" for name in self.module.REQUIRED}
        env.update(GITHUB_REF="refs/heads/develop", DEPLOY_ENVIRONMENT="homologation", GITHUB_SHA="a" * 40,
                   EXPECTED_AWS_ACCOUNT_ID="123456789012", AWS_REGION="us-east-1", TF_STATE_BUCKET="test-state-bucket")
        env.pop("EKS_PUBLIC_ACCESS_CIDRS", None)
        self.assertNotIn("EKS_PUBLIC_ACCESS_CIDRS", self.module.settings(env))

    def test_overlay_keeps_private_nodeport_and_does_not_change_legacy_service(self):
        result = subprocess.run(["kubectl", "kustomize", "k8s/phase3", "--load-restrictor", "LoadRestrictionsNone"], cwd=ROOT, capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("nodePort: 30080", result.stdout)
        self.assertIn("type: NodePort", result.stdout)
        self.assertIn('Auth__Internal__Enabled: "true"', result.stdout)
        self.assertNotIn("kind: Secret", result.stdout)
        self.assertIn("type: LoadBalancer", (ROOT / "k8s/service.yaml").read_text())

    def test_deployment_validates_network_before_secret_reads_and_requires_targets(self):
        contracts = test_phase3_inputs.Phase3InputsTests().contracts()
        fixtures = test_phase3_inputs.Phase3InputsTests()
        values = dict(EXPECTED_AWS_ACCOUNT_ID="123456789012", DEPLOY_ENVIRONMENT="homologation", TF_STATE_BUCKET="test-state-bucket", **fixtures.credentials())
        for network_failure, targets in [(False, [dict(TargetHealth=dict(State="healthy"))]), (False, []), (True, [])]:
            with self.subTest(network_failure=network_failure, targets=targets), trusted_temp_directory() as directory:
                def command(arguments, **kwargs):
                    if arguments[:3] == ["aws", "s3", "cp"]:
                        destination = Path(arguments[4])
                        destination.write_text(json.dumps(contracts[destination.stem]), encoding="utf-8")
                    return ""
                with patch.object(self.module, "run", side_effect=command), patch.object(self.module, "aws", side_effect=[{"Account": "123456789012"}, {"TargetHealthDescriptions": targets}]), \
                     patch.object(self.module, "inspect_network", side_effect=self.module.DeploymentError() if network_failure else None, return_value="target") as inspect, \
                     patch.object(self.module, "configure_cluster"), patch.object(self.module, "install_metrics"), \
                     patch.object(self.module, "read_secrets", return_value=fixtures.secrets()) as read, \
                     patch.object(self.module, "deploy_workload"), patch.object(self.module, "smoke_private_application") as smoke, \
                     patch.object(self.module, "restore_cluster_access"):
                    if network_failure or not targets:
                        with self.assertRaises(self.module.DeploymentError):
                            self.module.deploy(values, directory)
                        smoke.assert_not_called()
                    else:
                        self.module.deploy(values, directory)
                        smoke.assert_called_once()
                    if network_failure:
                        read.assert_not_called()

    def test_complete_runtime_secret_is_validated_before_cluster_or_kubernetes_mutation(self):
        fixtures = test_phase3_inputs.Phase3InputsTests()
        contracts = fixtures.contracts()
        secrets = fixtures.secrets()
        del secrets["bootstrap"]["activePassword"]
        values = dict(EXPECTED_AWS_ACCOUNT_ID="123456789012", DEPLOY_ENVIRONMENT="homologation",
                      TF_STATE_BUCKET="test-state-bucket", RUNNER_TEMP="unused", **fixtures.credentials())

        def command(arguments, **kwargs):
            if arguments[:3] == ["aws", "s3", "cp"]:
                destination = Path(arguments[4])
                destination.write_text(json.dumps(contracts[destination.stem]), encoding="utf-8")
            return ""

        with trusted_temp_directory() as directory, patch.object(self.module, "run", side_effect=command), \
             patch.object(self.module, "aws", return_value={"Account": "123456789012"}), \
             patch.object(self.module, "inspect_network", return_value="target"), \
             patch.object(self.module, "read_secrets", return_value=secrets), \
             patch.object(self.module, "configure_cluster") as configure, patch.object(self.module, "install_metrics") as metrics, \
             patch.object(self.module, "deploy_workload") as workload:
            with self.assertRaises(ValueError):
                self.module.deploy(values, directory)
            configure.assert_not_called()
            metrics.assert_not_called()
            workload.assert_not_called()

    def test_kubectl_json_parser_accepts_single_list_and_concatenated_documents(self):
        namespace = {"apiVersion": "v1", "kind": "Namespace", "metadata": {"name": "garageflow"}}
        config_map = {"apiVersion": "v1", "kind": "ConfigMap", "metadata": {"name": "garageflow-config"}}
        cases = [
            ("single", json.dumps(namespace, indent=2), ["Namespace"]),
            ("list", json.dumps({"apiVersion": "v1", "kind": "List", "items": [namespace, config_map]}, indent=2),
             ["Namespace", "ConfigMap"]),
            ("stream", f"{json.dumps(namespace, indent=2)}\n{json.dumps(config_map, indent=2)}\n",
             ["Namespace", "ConfigMap"]),
        ]

        parser = getattr(self.module, "parse_kubectl_json_stream", None)
        self.assertIsNotNone(parser, "deployment parser does not support kubectl JSON streams")
        for name, payload, expected_kinds in cases:
            with self.subTest(name=name):
                objects = parser(payload)
                self.assertEqual(expected_kinds, [item["kind"] for item in objects])

    def test_malformed_kubectl_json_stream_stops_before_any_workload_apply(self):
        fixtures = test_phase3_inputs.Phase3InputsTests()
        values = dict(fixtures.credentials(), GITHUB_SHA="a" * 40, EXPECTED_AWS_ACCOUNT_ID="123456789012",
                      AWS_REGION="us-east-1")
        malformed_streams = [
            '{"apiVersion":"v1","kind":"Namespace"}\n{"apiVersion":"apps/v1","kind":"Deployment"',
            '{"apiVersion":"v1","kind":"Namespace"}\ntrailing-garbage',
        ]

        for payload in malformed_streams:
            commands = []

            def command(arguments, **kwargs):
                commands.append(arguments)
                if arguments[:3] == ["aws", "ecr", "get-login-password"]:
                    return "PRIVATE-ECR"
                if arguments[:2] == ["kubectl", "create"]:
                    return payload
                return "rendered-manifests"

            with self.subTest(payload=payload), trusted_temp_directory() as directory, \
                 patch.object(self.module, "verify_ecr_repository", return_value="123456789012.dkr.ecr.us-east-1.amazonaws.com/garageflow"), \
                 patch.object(self.module, "run", side_effect=command):
                error = None
                try:
                    self.module.deploy_workload(fixtures.contracts(), values, fixtures.secrets(), directory)
                except ValueError as caught:
                    error = caught
                self.assertFalse(any(item[:2] == ["kubectl", "apply"] for item in commands))
                self.assertIsInstance(error, self.module.DeploymentError)

    def test_workload_migrates_before_rollout_and_uses_safe_apply_transitions(self):
        fixtures = test_phase3_inputs.Phase3InputsTests()
        objects = [{"kind": kind, "data": {}} for kind in ["Namespace", "ConfigMap", "Service", "HorizontalPodAutoscaler"]]
        objects.append({"kind": "Deployment", "metadata": {"name": "garageflow-api", "namespace": "garageflow"},
                        "spec": {"template": {"spec": {"containers": [{"image": "placeholder"}]}}}})
        for fail_migration in [False, True]:
            commands, applied = [], []
            def command(arguments, **kwargs):
                commands.append(arguments)
                if arguments[:3] == ["aws", "ecr", "get-login-password"]:
                    return "PRIVATE-ECR"
                if arguments[:2] == ["kubectl", "create"]:
                    return "\n".join(json.dumps(item, indent=2) for item in objects)
                if arguments[:3] == ["kubectl", "set", "image"]:
                    return '{"kind":"Job"}'
                if arguments[:5] == ["kubectl", "-n", "garageflow", "get", "deployment"]:
                    return "5"
                if arguments[:2] == ["kubectl", "apply"]:
                    applied.append(json.loads(kwargs["payload"]))
                if arguments[:4] == ["kubectl", "-n", "garageflow", "get"] and "secret" in arguments:
                    return ""
                if "job/garageflow-migration" in arguments and fail_migration:
                    raise self.module.DeploymentError()
                return "rendered-manifests"
            repository_url = fixtures.contracts()["platform"]["outputs"]["ecrRepositoryUrl"]
            with trusted_temp_directory() as directory, patch.object(self.module, "run", side_effect=command), \
                 patch.object(self.module, "aws", return_value={"repositories": [{"repositoryUri": repository_url}]}), patch.dict(os.environ):
                values = dict(fixtures.credentials(), GITHUB_SHA="a" * 40, EXPECTED_AWS_ACCOUNT_ID="123456789012", AWS_REGION="us-east-1")
                if fail_migration:
                    with self.assertRaises(self.module.DeploymentError):
                        self.module.deploy_workload(fixtures.contracts(), values, fixtures.secrets(), directory)
                    self.assertNotIn("Deployment", [obj["kind"] for obj in applied])
                else:
                    self.module.deploy_workload(fixtures.contracts(), values, fixtures.secrets(), directory)
                    self.assertEqual(["Namespace", "Secret", "ConfigMap", "Job", "Service", "Deployment", "HorizontalPodAutoscaler"], [obj["kind"] for obj in applied])
                    self.assertIn(":", applied[-2]["spec"]["template"]["spec"]["containers"][0]["image"])
                    self.assertEqual(5, applied[-2]["spec"]["replicas"])
                    apply_commands = [item for item in commands if item[:2] == ["kubectl", "apply"]]
                    self.assertNotIn("--server-side", apply_commands[0])
                    self.assertEqual(["kubectl", "apply", "--server-side", "--field-manager=garageflow-phase3",
                                      "--force-conflicts", "-f", "-"], apply_commands[1])
                self.assertNotIn("PRIVATE-ECR", str(commands))
                self.assertNotIn("temporary-secret", str(commands))

    def test_unverified_ecr_repository_stops_before_registry_login(self):
        fixtures = test_phase3_inputs.Phase3InputsTests()
        contracts = fixtures.contracts()
        repository_url = contracts["platform"]["outputs"]["ecrRepositoryUrl"]
        values = dict(fixtures.credentials(), GITHUB_SHA="a" * 40, EXPECTED_AWS_ACCOUNT_ID="123456789012", AWS_REGION="us-east-1")
        for repositories in [[], [{"repositoryUri": repository_url + "-other"}]]:
            commands = []
            with self.subTest(repositories=repositories), trusted_temp_directory() as directory, \
                 patch.object(self.module, "aws", return_value={"repositories": repositories}) as cloud, \
                 patch.object(self.module, "run", side_effect=lambda arguments, **kwargs: commands.append(arguments) or ""):
                with self.assertRaises(self.module.DeploymentError):
                    self.module.deploy_workload(contracts, values, fixtures.secrets(), directory)
                cloud.assert_called_once_with("ecr", "describe-repositories", "--registry-id", "123456789012",
                                              "--repository-names", repository_url.rsplit("/", 1)[1])
                self.assertFalse(any(command[:2] == ["docker", "login"] for command in commands))

    def test_secret_takeover_removes_and_verifies_legacy_client_apply_annotation(self):
        secret = {"apiVersion": "v1", "kind": "Secret", "metadata": {"name": "garageflow-secrets", "namespace": "garageflow"},
                  "data": {"key": "dmFsdWU="}}
        commands = []

        def command(arguments, **kwargs):
            commands.append(arguments)
            if arguments[:4] == ["kubectl", "-n", "garageflow", "get"]:
                return ""
            return ""

        with patch.object(self.module, "run", side_effect=command):
            self.module.apply(secret)
        self.assertEqual(["kubectl", "apply", "--server-side", "--field-manager=garageflow-phase3",
                          "--force-conflicts", "-f", "-"], commands[0])
        self.assertIn("kubectl.kubernetes.io/last-applied-configuration-", commands[1])
        self.assertEqual("get", commands[2][3])

        with patch.object(self.module, "run", side_effect=["", "", "present"]), self.assertRaises(self.module.DeploymentError):
            self.module.apply(secret)

    def test_secret_annotation_template_handles_absent_annotation_map(self):
        result = subprocess.run(["kubectl", "create", "secret", "generic", "template-test", "--dry-run=client", "-o",
                                 f"go-template={self.module.LEGACY_ANNOTATION_TEMPLATE}"], text=True, capture_output=True,
                                cwd=ROOT, check=False)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("", result.stdout)

    def test_secret_reads_parse_only_json_owned_by_database_and_bootstrap(self):
        values = test_phase3_inputs.Phase3InputsTests().secrets()
        serialized = [json.dumps(values[name]) if isinstance(values[name], dict) else values[name] for name in ["database", "jwt", "internal", "bootstrap", "webhook"]]
        with patch.object(self.module, "aws", side_effect=[{"SecretString": value} for value in serialized]):
            self.assertEqual(values, self.module.read_secrets(test_phase3_inputs.Phase3InputsTests().contracts()))

    def test_scoped_cluster_access_is_a_temporary_lease_and_restores_protected_cidrs(self):
        response = unittest.mock.MagicMock()
        response.__enter__.return_value.read.return_value = b"8.8.8.8\n"
        with trusted_temp_directory() as directory, patch.dict(os.environ), patch.object(self.module.urllib.request, "urlopen", return_value=response), \
             patch.object(self.module, "aws", side_effect=[{"update": {"id": "lease"}}, {"update": {"status": "Successful"}},
                                                           {"update": {"id": "restore"}}, {"update": {"status": "Successful"}}]) as cloud, \
             patch.object(self.module, "run"), patch.object(self.module.time, "sleep"):
            values = {"EKS_PUBLIC_ACCESS_CIDRS": '["203.0.113.10/32"]', "RUNNER_TEMP": str(directory)}
            self.module.configure_cluster(manifest()["outputs"], values, directory)
            lease = directory / self.module.LEASE_FILE_NAME
            self.assertTrue(lease.exists())
            self.assertEqual(str(directory / "kubeconfig"), os.environ["KUBECONFIG"])
            self.module.restore_cluster_access(values)
            self.assertFalse(lease.exists())
        self.assertIn("8.8.8.8/32", str(cloud.call_args_list[0]))
        self.assertNotIn("8.8.8.8/32", str(cloud.call_args_list[2]))
        for call in cloud.call_args_list:
            self.assertIn("--cli-connect-timeout", call.args)
            self.assertIn("--cli-read-timeout", call.args)
            self.assertLessEqual(call.kwargs["timeout"], 60)

    def test_empty_protected_cidrs_disable_public_endpoint_during_cleanup(self):
        response = unittest.mock.MagicMock()
        response.__enter__.return_value.read.return_value = b"8.8.8.8\n"
        with trusted_temp_directory() as directory, patch.dict(os.environ), patch.object(self.module.urllib.request, "urlopen", return_value=response), \
             patch.object(self.module, "aws", side_effect=[{"update": {"id": "lease"}}, {"update": {"status": "Successful"}},
                                                           {"update": {"id": "restore"}}, {"update": {"status": "Successful"}}]) as cloud, \
             patch.object(self.module, "run"), patch.object(self.module.time, "sleep"):
            values = {"RUNNER_TEMP": str(directory)}
            self.module.configure_cluster(manifest()["outputs"], values, directory)
            self.module.restore_cluster_access(values)
        cleanup_args = cloud.call_args_list[2].args
        cleanup_config = json.loads(cleanup_args[cleanup_args.index("--resources-vpc-config") + 1])
        self.assertEqual({"endpointPublicAccess": False, "endpointPrivateAccess": True}, cleanup_config)

    def test_cluster_cleanup_retries_transient_update_conflicts_within_a_bound(self):
        with trusted_temp_directory() as directory:
            marker = directory / "garageflow-phase3-eks-lease.json"
            marker.write_text(json.dumps({"clusterName": "garageflow-homologation", "protectedCidrs": ["203.0.113.10/32"]}), encoding="utf-8")
            values = {"RUNNER_TEMP": str(directory)}
            responses = [self.module.DeploymentError(), {"update": {"id": "restore"}}, {"update": {"status": "Successful"}}]
            with patch.object(self.module, "aws", side_effect=responses), patch.object(self.module.time, "monotonic", side_effect=[0, 1, 2, 3, 4]), \
                 patch.object(self.module.time, "sleep") as sleep:
                self.module.restore_cluster_access(values)
                sleep.assert_called_once()
            self.assertFalse(marker.exists())

            marker.write_text(json.dumps({"clusterName": "garageflow-homologation", "protectedCidrs": []}), encoding="utf-8")
            with patch.object(self.module, "aws", side_effect=self.module.DeploymentError()), \
                 patch.object(self.module.time, "monotonic", side_effect=[0, 601]), patch.object(self.module.time, "sleep") as sleep, \
                 self.assertRaises(self.module.DeploymentError):
                self.module.restore_cluster_access(values)
            sleep.assert_not_called()
            self.assertTrue(marker.exists())

    def test_cluster_cleanup_runs_after_success_and_failures_including_configuration(self):
        fixtures = test_phase3_inputs.Phase3InputsTests()
        contracts = fixtures.contracts()
        values = dict(EXPECTED_AWS_ACCOUNT_ID="123456789012", DEPLOY_ENVIRONMENT="homologation",
                      TF_STATE_BUCKET="test-state-bucket", RUNNER_TEMP="unused", **fixtures.credentials())

        def command(arguments, **kwargs):
            if arguments[:3] == ["aws", "s3", "cp"]:
                destination = Path(arguments[4])
                destination.write_text(json.dumps(contracts[destination.stem]), encoding="utf-8")
            return ""

        for failing_stage in [None, "configure_cluster", "install_metrics", "deploy_workload", "smoke_private_application"]:
            with self.subTest(failing_stage=failing_stage), trusted_temp_directory() as directory, \
                 patch.object(self.module, "run", side_effect=command), \
                 patch.object(self.module, "aws", side_effect=[{"Account": "123456789012"}, {"TargetHealthDescriptions": [{}]}]), \
                 patch.object(self.module, "inspect_network", return_value="target"), \
                 patch.object(self.module, "read_secrets", return_value=fixtures.secrets()), \
                 patch.object(self.module, "configure_cluster", side_effect=self.module.DeploymentError() if failing_stage == "configure_cluster" else None), \
                 patch.object(self.module, "install_metrics", side_effect=self.module.DeploymentError() if failing_stage == "install_metrics" else None), \
                 patch.object(self.module, "deploy_workload", side_effect=self.module.DeploymentError() if failing_stage == "deploy_workload" else None), \
                 patch.object(self.module, "smoke_private_application", side_effect=self.module.DeploymentError() if failing_stage == "smoke_private_application" else None), \
                 patch.object(self.module, "restore_cluster_access") as restore:
                if failing_stage:
                    with self.assertRaises(self.module.DeploymentError):
                        self.module.deploy(values, directory)
                else:
                    self.module.deploy(values, directory)
                restore.assert_called_once_with(values)

    def test_metrics_asset_is_verified_before_apply(self):
        response = unittest.mock.MagicMock()
        response.__enter__.return_value.read.return_value = b"fixture"
        for checksum in ["wrong", hashlib.sha256(b"fixture").hexdigest()]:
            with trusted_temp_directory() as directory, patch.object(self.module.urllib.request, "urlopen", return_value=response), \
                 patch.object(self.module, "METRICS_SHA256", checksum), patch.object(self.module, "run") as command:
                if checksum == "wrong":
                    with self.assertRaises(self.module.DeploymentError):
                        self.module.install_metrics(directory)
                    command.assert_not_called()
                else:
                    self.module.install_metrics(directory)
                    self.assertEqual(3, command.call_count)

    def test_metrics_api_readiness_retries_and_has_a_bounded_timeout(self):
        response = unittest.mock.MagicMock()
        response.__enter__.return_value.read.return_value = b"fixture"
        timeout = subprocess.TimeoutExpired(["kubectl", "top", "nodes"], 10)
        for outcomes, monotonic, succeeds in [(["", "", timeout, self.module.DeploymentError(), "nodes"], [0, 1, 2, 3, 4, 5], True),
                                                (["", "", timeout], [0, 1, 181], False)]:
            with self.subTest(succeeds=succeeds), trusted_temp_directory() as directory, \
                 patch.object(self.module.urllib.request, "urlopen", return_value=response), \
                 patch.object(self.module, "METRICS_SHA256", hashlib.sha256(b"fixture").hexdigest()), \
                 patch.object(self.module, "run", side_effect=outcomes), patch.object(self.module.time, "monotonic", side_effect=monotonic), \
                 patch.object(self.module.time, "sleep") as sleep:
                if succeeds:
                    self.module.install_metrics(directory)
                    self.assertEqual(2, sleep.call_count)
                else:
                    with self.assertRaises(self.module.DeploymentError):
                        self.module.install_metrics(directory)
                    sleep.assert_not_called()
                top_calls = [call for call in self.module.run.call_args_list if "top" in call.args[0]]
                self.assertTrue(top_calls)
                for call in top_calls:
                    self.assertIn("--request-timeout=10s", call.args[0])
                    self.assertLessEqual(call.kwargs["timeout"], 15)

    def test_private_smoke_always_closes_the_tunnel(self):
        for failure in [None, self.module.DeploymentError()]:
            process = unittest.mock.MagicMock()
            process.poll.return_value = None
            with patch.object(self.module.subprocess, "Popen", return_value=process), patch.object(self.module, "run", side_effect=failure):
                if failure:
                    with self.assertRaises(self.module.DeploymentError):
                        self.module.smoke_private_application(test_phase3_inputs.Phase3InputsTests().secrets(), {})
                else:
                    self.module.smoke_private_application(test_phase3_inputs.Phase3InputsTests().secrets(), {})
                process.terminate.assert_called_once()
                process.wait.assert_called_once()

    def test_live_inventory_is_matched_before_trusting_ingress_and_database(self):
        contracts, network = test_phase3_inputs.Phase3InputsTests().network()
        database = contracts["database"]["outputs"]
        network["database"]["Endpoint"] = {"Address": database["databaseHost"], "Port": database["databasePort"]}
        responses = [{"cluster": network["cluster"]}, {"Listeners": [network["listener"]]}, {"LoadBalancers": [network["alb"]]},
                     {"TargetGroups": [network["target"]]}, {"SecurityGroups": network["groups"]}, {"DBInstances": [network["database"]]}]
        with patch.object(self.module, "aws", side_effect=responses):
            self.assertEqual("target-group", self.module.inspect_network(contracts))
        responses[-1] = {"DBInstances": []}
        with patch.object(self.module, "aws", side_effect=responses), self.assertRaises(self.module.DeploymentError):
            self.module.inspect_network(contracts)
        network["listener"]["DefaultActions"] = [{"Type": "redirect"}]
        with patch.object(self.module, "aws", side_effect=responses), self.assertRaises(self.module.DeploymentError):
            self.module.inspect_network(contracts)

    def test_main_cleans_private_workspace_on_success_and_failure(self):
        for failure in [None, self.module.DeploymentError()]:
            with trusted_temp_directory() as directory:
                values = {name: "test" for name in self.module.REQUIRED}
                values.update(GITHUB_REF="refs/heads/main", DEPLOY_ENVIRONMENT="production", GITHUB_SHA="a" * 40,
                              EXPECTED_AWS_ACCOUNT_ID="123456789012", AWS_REGION="us-east-1", TF_STATE_BUCKET="test-state-bucket", RUNNER_TEMP=str(directory))
                with patch.dict(os.environ, values), patch.object(self.module, "deploy", side_effect=failure) as deployment, redirect_stderr(io.StringIO()):
                    self.assertEqual(0 if failure is None else 2, self.module.main())
                self.assertFalse(deployment.call_args.args[1].exists())

    def test_cleanup_entrypoint_uses_the_lease_without_starting_a_deployment(self):
        with trusted_temp_directory() as directory:
            values = {name: "test" for name in self.module.REQUIRED}
            values.update(GITHUB_REF="refs/heads/develop", DEPLOY_ENVIRONMENT="homologation", GITHUB_SHA="a" * 40,
                          EXPECTED_AWS_ACCOUNT_ID="123456789012", AWS_REGION="us-east-1", TF_STATE_BUCKET="test-state-bucket",
                          RUNNER_TEMP=str(directory))
            with patch.dict(os.environ, values), patch.object(self.module, "aws", return_value={"Account": "123456789012"}), \
                 patch.object(self.module, "restore_cluster_access") as restore, patch.object(self.module, "deploy") as deployment:
                self.assertEqual(0, self.module.main(["--cleanup-cluster-access"]))
            restore.assert_called_once()
            deployment.assert_not_called()
