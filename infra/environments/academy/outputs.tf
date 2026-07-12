output "aws_region" {
  description = "AWS region containing the Academy environment."
  value       = var.aws_region
}

output "cluster_name" {
  description = "EKS cluster name used by deployment automation."
  value       = module.eks.cluster_name
}

output "ecr_repository_url" {
  description = "ECR repository URL used for immutable SHA images."
  value       = module.ecr.repository_url
}

output "rds_endpoint" {
  description = "Private RDS hostname without credentials or port."
  value       = module.rds.endpoint
}

output "rds_port" {
  description = "Private PostgreSQL port."
  value       = module.rds.port
}

output "rds_database" {
  description = "PostgreSQL database name."
  value       = module.rds.database
}

output "sns_topic_arn" {
  description = "ARN patched into the application ConfigMap during deployment."
  value       = module.sns.topic_arn
}

output "database_secret_arn" {
  description = "ARN used by CD to retrieve database credentials."
  value       = module.secrets.database_secret_arn
}

output "jwt_secret_arn" {
  description = "ARN used by CD to retrieve the JWT signing key."
  value       = module.secrets.jwt_secret_arn
}

output "bootstrap_secret_arn" {
  description = "ARN used by CD to retrieve bootstrap administrator values."
  value       = module.secrets.bootstrap_secret_arn
}

output "webhook_secret_arn" {
  description = "ARN used by CD to retrieve the webhook HMAC key."
  value       = module.secrets.webhook_secret_arn
}

output "vpc_id" {
  description = "GarageFlow VPC ID used for lifecycle verification."
  value       = module.network.vpc_id
}

output "node_group_name" {
  description = "Deterministic managed node-group name used for lifecycle verification."
  value       = "${module.eks.cluster_name}-workers"
}

output "rds_identifier" {
  description = "RDS identifier used for lifecycle verification."
  value       = module.rds.identifier
}

output "ecr_repository_name" {
  description = "ECR repository name used for lifecycle verification."
  value       = module.ecr.repository_name
}

output "sns_topic_name" {
  description = "SNS topic name used for lifecycle verification."
  value       = module.sns.topic_name
}
