output "topic_arn" {
  description = "ARN of the standard SNS topic."
  value       = aws_sns_topic.this.arn
}

output "topic_name" {
  description = "Name of the standard SNS topic."
  value       = aws_sns_topic.this.name
}

output "subscription_arn" {
  description = "Subscription ARN, or PendingConfirmation until the recipient confirms the email."
  value       = aws_sns_topic_subscription.email.arn
}

output "pending_confirmation" {
  description = "Whether the provider reports that manual email confirmation is still pending."
  value       = aws_sns_topic_subscription.email.pending_confirmation
}
