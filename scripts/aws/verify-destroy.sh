#!/usr/bin/env bash
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
export AWS_REGION AWS_DEFAULT_REGION="${AWS_DEFAULT_REGION:-${AWS_REGION}}"

if [[ "${AWS_REGION}" != 'us-east-1' ]]; then
  echo 'Verification refused: AWS_REGION must be us-east-1.' >&2
  exit 1
fi
if (( $# != 1 )) || [[ -z "${1:-}" ]]; then
  echo 'Usage: verify-destroy.sh BACKEND_BUCKET' >&2
  exit 1
fi

backend_bucket="$1"
cluster_name='garageflow-academy'
node_group_name='garageflow-academy-workers'
rds_identifier='garageflow-academy'
ecr_repository='garageflow'
sns_topic_name='garageflow-work-orders'
service_tag='garageflow/garageflow-api'
vpc_name='garageflow-academy-vpc'
secret_names=(
  'garageflow/academy/database'
  'garageflow/academy/jwt'
  'garageflow/academy/bootstrap'
  'garageflow/academy/webhook'
)
findings=()
temp_dir="$(mktemp -d)"
trap 'rm -rf -- "${temp_dir}"' EXIT

require_api_json() {
  local label="$1"
  shift
  local output
  if ! output="$("$@")"; then
    echo "Verification failed closed: ${label} API query failed." >&2
    return 1
  fi
  printf '%s' "${output}"
}

require_resource_absent() {
  local label="$1"
  local not_found_code="$2"
  shift 2
  local error_file="${temp_dir}/not-found-${RANDOM}.err"
  local status
  set +e
  "$@" >/dev/null 2>"${error_file}"
  status=$?
  set -e
  if (( status == 0 )); then
    findings+=("${label}")
  elif grep -q -- "${not_found_code}" "${error_file}"; then
    : # A genuine service-specific not-found response is the expected state.
  else
    echo "Verification failed closed: ${label} lookup returned an API or permission error." >&2
    return 1
  fi
}

require_resource_absent 'EKS cluster garageflow-academy' 'ResourceNotFoundException' \
  aws eks describe-cluster --region "${AWS_REGION}" --name "${cluster_name}"
require_resource_absent 'EKS node group garageflow-academy-workers' 'ResourceNotFoundException' \
  aws eks describe-nodegroup --region "${AWS_REGION}" \
  --cluster-name "${cluster_name}" --nodegroup-name "${node_group_name}"

ec2_json="$(require_api_json 'EC2 workers' aws ec2 describe-instances \
  --region "${AWS_REGION}" \
  --filters \
    'Name=tag:Project,Values=GarageFlow' \
    'Name=tag:Environment,Values=academy' \
    'Name=instance-state-name,Values=pending,running' \
  --output json)"
ec2_count="$(jq '[.Reservations[].Instances[]] | length' <<<"${ec2_json}")"
if (( ec2_count > 0 )); then
  findings+=("${ec2_count} running or pending GarageFlow EC2 worker(s)")
fi

require_resource_absent 'RDS instance garageflow-academy' 'DBInstanceNotFound' \
  aws rds describe-db-instances --region "${AWS_REGION}" \
  --db-instance-identifier "${rds_identifier}"
require_resource_absent 'ECR repository garageflow' 'RepositoryNotFoundException' \
  aws ecr describe-repositories --region "${AWS_REGION}" \
  --repository-names "${ecr_repository}"

sns_json="$(require_api_json 'SNS topics' aws sns list-topics \
  --region "${AWS_REGION}" --output json)"
sns_count="$(jq --arg suffix ":${sns_topic_name}" \
  '[.Topics[]? | select(.TopicArn | endswith($suffix))] | length' <<<"${sns_json}")"
if (( sns_count > 0 )); then
  findings+=("SNS topic ${sns_topic_name}")
fi

for secret_name in "${secret_names[@]}"; do
  require_resource_absent "Secrets Manager secret ${secret_name}" 'ResourceNotFoundException' \
    aws secretsmanager describe-secret --region "${AWS_REGION}" --secret-id "${secret_name}"
done

elbv2_json="$(require_api_json 'ELBv2 load balancers' aws elbv2 describe-load-balancers \
  --region "${AWS_REGION}" --output json)"
while IFS= read -r load_balancer_arn; do
  [[ -n "${load_balancer_arn}" ]] || continue
  tags_json="$(require_api_json 'ELBv2 tags' aws elbv2 describe-tags \
    --region "${AWS_REGION}" --resource-arns "${load_balancer_arn}" --output json)"
  if jq -e --arg service "${service_tag}" \
    '.TagDescriptions[].Tags[]? | select(.Key == "kubernetes.io/service-name" and .Value == $service)' \
    >/dev/null <<<"${tags_json}"; then
    findings+=('ELBv2 LoadBalancer for garageflow/garageflow-api')
  fi
done < <(jq -r '.LoadBalancers[]?.LoadBalancerArn' <<<"${elbv2_json}")

classic_json="$(require_api_json 'Classic ELB load balancers' aws elb describe-load-balancers \
  --region "${AWS_REGION}" --output json)"
while IFS= read -r load_balancer_name; do
  [[ -n "${load_balancer_name}" ]] || continue
  tags_json="$(require_api_json 'Classic ELB tags' aws elb describe-tags \
    --region "${AWS_REGION}" --load-balancer-names "${load_balancer_name}" --output json)"
  if jq -e --arg service "${service_tag}" \
    '.TagDescriptions[].Tags[]? | select(.Key == "kubernetes.io/service-name" and .Value == $service)' \
    >/dev/null <<<"${tags_json}"; then
    findings+=('Classic LoadBalancer for garageflow/garageflow-api')
  fi
done < <(jq -r '.LoadBalancerDescriptions[]?.LoadBalancerName' <<<"${classic_json}")

vpc_json="$(require_api_json 'GarageFlow VPC' aws ec2 describe-vpcs \
  --region "${AWS_REGION}" \
  --filters \
    "Name=tag:Name,Values=${vpc_name}" \
    'Name=tag:Project,Values=GarageFlow' \
    'Name=tag:Environment,Values=academy' \
  --output json)"
vpc_count="$(jq '.Vpcs | length' <<<"${vpc_json}")"
if (( vpc_count > 0 )); then
  findings+=("${vpc_count} GarageFlow VPC(s)")
fi

if ! aws s3api head-bucket --bucket "${backend_bucket}" >/dev/null 2>&1; then
  echo 'Verification failed closed: retained backend bucket is absent or inaccessible.' >&2
  exit 1
fi
versioning_status="$(require_api_json 'backend bucket versioning' \
  aws s3api get-bucket-versioning --bucket "${backend_bucket}" \
  --query Status --output text)"
if [[ "${versioning_status}" != 'Enabled' ]]; then
  echo 'Verification failed: retained backend bucket versioning is not Enabled.' >&2
  exit 1
fi

if (( ${#findings[@]} > 0 )); then
  echo 'Destruction verification failed; environment resources remain:' >&2
  printf ' - %s\n' "${findings[@]}" >&2
  exit 1
fi

echo 'No billable GarageFlow Academy environment resources remain; the retained backend bucket exists with versioning Enabled.'
