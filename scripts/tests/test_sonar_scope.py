import ast
import re
import unittest
from pathlib import Path

class SonarScopeTests(unittest.TestCase):
    def test_sonar_only_analyzes_main_and_pull_requests_targeting_main(self):
        workflow = Path(__file__).resolve().parents[2] / ".github/workflows/quality-gate.yml"
        blocks = re.findall(r"(?ms)^      - name: (.*?)(?=^      - name:|\Z)", workflow.read_text(encoding="utf-8"))
        steps = {block.splitlines()[0]: block for block in blocks}
        detect = steps["Detect SonarQube configuration"]
        guard = re.search(r"(?m)^        if: (.+)$", detect)
        for event, ref, base, expected in (
            ("push", "refs/heads/main", "", True),
            ("workflow_dispatch", "refs/heads/main", "", True),
            ("push", "refs/heads/develop", "", False),
            ("workflow_dispatch", "refs/heads/develop", "", False),
            ("pull_request", "refs/pull/1/merge", "main", True),
            ("pull_request", "refs/pull/1/merge", "develop", False),
            ("push", "refs/heads/codex/test", "", False),
            ("push", "refs/tags/main", "", False),
        ):
            with self.subTest(event=event, ref=ref, base=base):
                condition = guard[1] if guard else "True"
                for name, value in (("github.event_name", event), ("github.ref", ref), ("github.base_ref", base)):
                    condition = condition.replace(name, repr(value))
                tree = ast.parse(condition.replace("&&", " and ").replace("||", " or "), mode="eval")
                allowed = (ast.Expression, ast.BoolOp, ast.And, ast.Or, ast.Compare, ast.Eq, ast.Constant)
                self.assertTrue(all(isinstance(node, allowed) for node in ast.walk(tree)))
                self.assertEqual(expected, eval(compile(tree, "<sonar-scope>", "eval"), {"__builtins__": {}}))
        for name in ("Build", "Architecture Tests", "Unit Tests", "Integration Tests", "E2E Tests Against PostgreSQL", "Validate Coverage Threshold (80%)"):
            self.assertIsNone(re.search(r"(?m)^        if:", steps[name]), name)
