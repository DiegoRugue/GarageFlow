output "vpc_id" {
  description = "ID of the GarageFlow VPC."
  value       = aws_vpc.this.id
}

output "public_subnet_ids" {
  description = "Public subnet IDs ordered by the supplied availability zones."
  value = [
    aws_subnet.public_a.id,
    aws_subnet.public_b.id
  ]
}

output "db_subnet_ids" {
  description = "Private database subnet IDs ordered by the supplied availability zones."
  value = [
    aws_subnet.db_a.id,
    aws_subnet.db_b.id
  ]
}
