resource "aws_sns_topic" "this" {
  name = var.topic_name

  tags = merge(var.tags, {
    Name = var.topic_name
  })
}

resource "aws_sns_topic_subscription" "email" {
  topic_arn              = aws_sns_topic.this.arn
  protocol               = "email"
  endpoint               = trimspace(var.notification_email)
  endpoint_auto_confirms = false
}
