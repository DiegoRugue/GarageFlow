output "endpoint" {
  description = "RDS endpoint hostname without a port or credentials."
  value       = aws_db_instance.this.address
}

output "port" {
  description = "PostgreSQL port exposed inside the VPC."
  value       = aws_db_instance.this.port
}

output "database" {
  description = "Name of the GarageFlow PostgreSQL database."
  value       = aws_db_instance.this.db_name
}

output "identifier" {
  description = "Stable identifier of the RDS instance."
  value       = aws_db_instance.this.identifier
}
