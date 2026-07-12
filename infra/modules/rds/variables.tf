variable "vpc_id" {
  description = "ID of the VPC that hosts the private database."
  type        = string

  validation {
    condition     = can(regex("^vpc-[0-9a-fA-F]+$", var.vpc_id))
    error_message = "vpc_id must be a syntactically valid VPC ID."
  }
}

variable "private_subnet_ids" {
  description = "Exactly two distinct private subnet IDs for the RDS subnet group."
  type        = list(string)

  validation {
    condition = (
      length(var.private_subnet_ids) == 2 &&
      length(distinct(var.private_subnet_ids)) == 2 &&
      alltrue([for subnet_id in var.private_subnet_ids : can(regex("^subnet-[0-9a-fA-F]+$", subnet_id))])
    )
    error_message = "private_subnet_ids must contain exactly two distinct, syntactically valid subnet IDs."
  }
}

variable "eks_security_group_id" {
  description = "ID of the EKS security group allowed to connect to PostgreSQL."
  type        = string

  validation {
    condition     = can(regex("^sg-[0-9a-fA-F]+$", var.eks_security_group_id))
    error_message = "eks_security_group_id must be a syntactically valid security group ID."
  }
}

variable "database_password" {
  description = "Password for the fixed garageflowadmin database user."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.database_password) >= 8
    error_message = "database_password must contain at least 8 characters."
  }
}

variable "identifier" {
  description = "Stable identifier for the RDS instance and related resources."
  type        = string
  default     = "garageflow-academy"

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{0,61}[a-z0-9]$", var.identifier)) && !strcontains(var.identifier, "--")
    error_message = "identifier must be a valid lowercase RDS identifier between 2 and 63 characters."
  }
}

variable "tags" {
  description = "Additional non-sensitive tags to apply to RDS resources."
  type        = map(string)
  default     = {}
}
