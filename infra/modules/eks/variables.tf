variable "cluster_name" {
  description = "Name of the EKS cluster."
  type        = string

  validation {
    condition     = trimspace(var.cluster_name) != ""
    error_message = "cluster_name must not be blank."
  }
}

variable "kubernetes_version" {
  description = "Kubernetes version for the EKS control plane."
  type        = string

  validation {
    condition     = can(regex("^[0-9]+\\.[0-9]+$", var.kubernetes_version))
    error_message = "kubernetes_version must use major.minor notation, for example 1.36."
  }
}

variable "public_subnet_ids" {
  description = "Exactly two distinct public subnet IDs for the cluster and node group."
  type        = list(string)

  validation {
    condition = (
      length(var.public_subnet_ids) == 2 &&
      length(distinct(var.public_subnet_ids)) == 2 &&
      alltrue([for subnet_id in var.public_subnet_ids : can(regex("^subnet-[0-9a-fA-F]+$", subnet_id))])
    )
    error_message = "public_subnet_ids must contain exactly two distinct, syntactically valid subnet IDs."
  }
}

variable "cluster_role_arn" {
  description = "ARN of the pre-existing IAM role used by the EKS control plane."
  type        = string

  validation {
    condition     = can(regex("^arn:(aws|aws-us-gov|aws-cn):iam::[0-9]{12}:role/.+$", var.cluster_role_arn))
    error_message = "cluster_role_arn must be a syntactically valid IAM role ARN."
  }
}

variable "node_role_arn" {
  description = "ARN of the pre-existing IAM role used by the managed node group."
  type        = string

  validation {
    condition     = can(regex("^arn:(aws|aws-us-gov|aws-cn):iam::[0-9]{12}:role/.+$", var.node_role_arn))
    error_message = "node_role_arn must be a syntactically valid IAM role ARN."
  }
}

variable "public_access_cidrs" {
  description = "IPv4 CIDR blocks allowed to reach the public EKS API endpoint."
  type        = list(string)

  validation {
    condition = (
      length(var.public_access_cidrs) > 0 &&
      alltrue([for cidr in var.public_access_cidrs : can(cidrnetmask(cidr))])
    )
    error_message = "public_access_cidrs must contain at least one valid CIDR block."
  }
}

variable "tags" {
  description = "Additional non-sensitive tags to apply to EKS resources."
  type        = map(string)
  default     = {}
}
