variable "repository_name" {
  description = "Name of the ECR repository."
  type        = string
  default     = "garageflow"

  validation {
    condition = (
      length(var.repository_name) >= 2 &&
      length(var.repository_name) <= 256 &&
      can(regex("^[a-z0-9]+(?:[._/-][a-z0-9]+)*$", var.repository_name))
    )
    error_message = "repository_name must contain 2 to 256 characters and be a syntactically valid ECR repository name."
  }
}

variable "tags" {
  description = "Additional non-sensitive tags to apply to the ECR repository."
  type        = map(string)
  default     = {}
}
