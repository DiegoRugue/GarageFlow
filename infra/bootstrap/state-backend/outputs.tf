output "state_bucket_name" {
  description = "Name of the retained S3 Terraform state bucket."
  value       = aws_s3_bucket.state.bucket
}

output "state_bucket_arn" {
  description = "ARN of the retained S3 Terraform state bucket."
  value       = aws_s3_bucket.state.arn
}
