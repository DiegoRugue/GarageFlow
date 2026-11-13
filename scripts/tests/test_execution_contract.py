import json
from pathlib import Path
import unittest
from uuid import UUID

from jsonschema import Draft202012Validator, FormatChecker


SCHEMA_PATH = Path(__file__).resolve().parents[2] / "contracts" / "execution-v1.schema.json"
SCHEDULE = "execution.schedule.v1"
ACCEPTED = "execution.scheduled.v1"
REJECTED = "execution.schedule-rejected.v1"
IDS = {
    name: str(UUID(int=index))
    for index, name in enumerate(
        ("messageId", "correlationId", "workOrderId", "estimateId", "executionId"), 1)
}
LINE = str(UUID(int=6))


def envelope(message_type, payload):
    return {
        **IDS,
        "schemaVersion": "1.0",
        "messageType": message_type,
        "occurredAt": "2026-11-11T14:00:00Z",
        "payload": payload,
    }


class ExecutionContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(schema)
        cls.validator = Draft202012Validator(schema, format_checker=FormatChecker())

    def test_v1_messages_validate(self):
        for message in (
            envelope(SCHEDULE, {"serviceLineIds": [LINE]}),
            envelope(ACCEPTED, {}),
            envelope(REJECTED, {"reasonCode": "work_order_already_scheduled"}),
            envelope("execution.start.v1", {}),
            envelope("execution.complete.v1", {}),
            envelope("execution.started.v1", {}),
            envelope("execution.completed.v1", {}),
        ):
            with self.subTest(message_type=message["messageType"]):
                self.validator.validate(message)

    def test_invalid_envelopes_are_rejected(self):
        for field, value in (
            ("schemaVersion", "2.0"),
            ("messageType", "execution.schedule.v2"),
            ("customerCpf", "00000000000"),
            ("messageId", str(UUID(int=0))),
            ("occurredAt", "2026-11-11"),
        ):
            with self.subTest(field=field):
                self.assertFalse(self.validator.is_valid({
                    **envelope(SCHEDULE, {"serviceLineIds": [LINE]}), field: value
                }))

    def test_payload_matches_message_type(self):
        cases = (
            envelope(SCHEDULE, {"serviceLineIds": []}),
            envelope(SCHEDULE, {"serviceLineIds": [LINE, LINE]}),
            envelope(SCHEDULE, {"serviceLineIds": [str(UUID(int=i)) for i in range(1, 4098)]}),
            envelope(SCHEDULE, {"serviceLineIds": [LINE], "customerName": "Synthetic"}),
            envelope(ACCEPTED, {"reasonCode": "work_order_already_scheduled"}),
            envelope("execution.complete.v1", {"serviceLineIds": [LINE]}),
            envelope(REJECTED, {"reasonCode": "temporary_failure"}),
        )
        for message in cases:
            with self.subTest(message_type=message["messageType"], fields=list(message["payload"])):
                self.assertFalse(self.validator.is_valid(message))
