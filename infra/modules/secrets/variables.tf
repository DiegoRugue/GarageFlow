variable "bootstrap_admin_email" {
  description = "Email address embedded in the bootstrap administrator secret."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", trimspace(var.bootstrap_admin_email)))
    error_message = "bootstrap_admin_email must be a valid email-shaped string."
  }
}

variable "tags" {
  description = "Additional non-sensitive tags to apply to Secrets Manager secrets."
  type        = map(string)
  default     = {}
}
