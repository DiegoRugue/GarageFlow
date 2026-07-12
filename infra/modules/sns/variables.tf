variable "topic_name" {
  description = "Name of the standard SNS topic."
  type        = string
  default     = "garageflow-work-orders"

  validation {
    condition     = can(regex("^[A-Za-z0-9_-]{1,256}$", var.topic_name))
    error_message = "topic_name must be a syntactically valid standard SNS topic name."
  }
}

variable "notification_email" {
  description = "Email address that must manually confirm the SNS subscription."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", trimspace(var.notification_email)))
    error_message = "notification_email must be a valid email-shaped string."
  }
}

variable "tags" {
  description = "Additional non-sensitive tags to apply to the SNS topic."
  type        = map(string)
  default     = {}
}
