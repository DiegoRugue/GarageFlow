import importlib
import sys
import unittest
from test_infra_contract import ROOT

sys.path.insert(0, str(ROOT / 'scripts'))


class ObservabilityInputsTests(unittest.TestCase):
    def test_configuration_is_explicitly_disabled_by_default(self):
        module = importlib.import_module('phase3_inputs')
        configure = getattr(module, 'observability_config', None)
        self.assertIsNotNone(configure, 'missing opt-in observability configuration')
        self.assertEqual({'Observability__Enabled': 'false',
                          'Observability__OtlpEndpoint': 'http://garageflow-otel.newrelic.svc.cluster.local:4318',
                          'Observability__Environment': 'production'}, configure({'DEPLOY_ENVIRONMENT': 'production'}))

    def test_enabled_requires_exact_boolean_and_deployment_environment(self):
        module = importlib.import_module('phase3_inputs')
        configure = getattr(module, 'observability_config', None)
        self.assertIsNotNone(configure, 'missing opt-in observability configuration')
        self.assertEqual('true', configure({'DEPLOY_ENVIRONMENT': 'homologation', 'OBSERVABILITY_ENABLED': 'true'})['Observability__Enabled'])
        for values in [{'DEPLOY_ENVIRONMENT': 'production', 'OBSERVABILITY_ENABLED': 'yes'},
                       {'DEPLOY_ENVIRONMENT': 'secret'}, {}]:
            with self.assertRaises(ValueError):
                configure(values)
