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

  depends_on = [
    aws_route.public_default,
    aws_route_table_association.public_a,
    aws_route_table_association.public_b
  ]
}

output "db_subnet_ids" {
  description = "Private database subnet IDs ordered by the supplied availability zones."
  value = [
    aws_subnet.db_a.id,
    aws_subnet.db_b.id
  ]
}
