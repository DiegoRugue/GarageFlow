output "database_secret_arn" {
  description = "ARN of the database credentials secret."
  value       = aws_secretsmanager_secret.database.arn
}

output "jwt_secret_arn" {
  description = "ARN of the JWT signing secret."
  value       = aws_secretsmanager_secret.jwt.arn
}

output "bootstrap_secret_arn" {
  description = "ARN of the bootstrap administrator secret."
  value       = aws_secretsmanager_secret.bootstrap.arn
}

output "webhook_secret_arn" {
  description = "ARN of the estimate-decision webhook secret."
  value       = aws_secretsmanager_secret.webhook.arn
}

output "database_password" {
  description = "Generated RDS password passed directly to the RDS module by the environment root."
  value       = random_password.database.result
  sensitive   = true
}
