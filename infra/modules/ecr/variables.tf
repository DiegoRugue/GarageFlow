variable "repository_name" {
  description = "Name of the ECR repository."
  type        = string
  default     = "garageflow"

  validation {
    condition     = can(regex("^[a-z0-9]+(?:[._/-][a-z0-9]+)*$", var.repository_name))
    error_message = "repository_name must be a syntactically valid ECR repository name."
  }
}

variable "tags" {
  description = "Additional non-sensitive tags to apply to the ECR repository."
  type        = map(string)
  default     = {}
}
