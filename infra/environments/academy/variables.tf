variable "aws_region" {
  description = "AWS Academy region; the approved Phase 2 architecture is restricted to us-east-1."
  type        = string
  default     = "us-east-1"

  validation {
    condition     = var.aws_region == "us-east-1"
    error_message = "aws_region must be us-east-1."
  }
}

variable "cluster_name" {
  description = "Name shared by the GarageFlow EKS cluster and its network discovery tags."
  type        = string
  default     = "garageflow-academy"

  validation {
    condition = (
      length(var.cluster_name) >= 1 &&
      length(var.cluster_name) <= 100 &&
      can(regex("^[A-Za-z0-9][A-Za-z0-9_-]*$", var.cluster_name))
    )
    error_message = "cluster_name must be a valid EKS cluster name containing 1 to 100 letters, numbers, underscores, or hyphens."
  }
}

variable "kubernetes_version" {
  description = "EKS Kubernetes version in major.minor notation; CD verifies STANDARD_SUPPORT before apply."
  type        = string
  default     = "1.36"

  validation {
    condition     = can(regex("^[0-9]+\\.[0-9]+$", var.kubernetes_version))
    error_message = "kubernetes_version must use major.minor notation, for example 1.36."
  }
}

variable "eks_cluster_role_arn" {
  description = "ARN of the pre-existing AWS Academy role used by the EKS control plane."
  type        = string

  validation {
    condition     = can(regex("^arn:[a-z0-9-]+:iam::[0-9]{12}:role/[A-Za-z0-9+=,.@_/-]*[A-Za-z0-9+=,.@_-]$", var.eks_cluster_role_arn))
    error_message = "eks_cluster_role_arn must be a syntactically valid IAM role ARN."
  }
}

variable "eks_node_role_arn" {
  description = "ARN of the pre-existing AWS Academy role used by the EKS managed node group."
  type        = string

  validation {
    condition     = can(regex("^arn:[a-z0-9-]+:iam::[0-9]{12}:role/[A-Za-z0-9+=,.@_/-]*[A-Za-z0-9+=,.@_-]$", var.eks_node_role_arn))
    error_message = "eks_node_role_arn must be a syntactically valid IAM role ARN."
  }
}

variable "owner" {
  description = "Nonempty owner tag used to identify the student responsible for Academy resources."
  type        = string

  validation {
    condition     = trimspace(var.owner) != ""
    error_message = "owner must not be blank."
  }
}

variable "expires_on" {
  description = "ISO calendar date recording the intended same-day Academy cleanup."
  type        = string

  validation {
    condition = (
      can(regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", var.expires_on)) &&
      can(formatdate("YYYY-MM-DD", "${var.expires_on}T00:00:00Z"))
    )
    error_message = "expires_on must be a valid ISO date in YYYY-MM-DD format."
  }
}

variable "notification_email" {
  description = "Email address that manually confirms the GarageFlow SNS subscription."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", trimspace(var.notification_email)))
    error_message = "notification_email must be a valid email-shaped string."
  }
}

variable "bootstrap_admin_email" {
  description = "Synthetic administrator email stored in the bootstrap secret."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", trimspace(var.bootstrap_admin_email)))
    error_message = "bootstrap_admin_email must be a valid email-shaped string."
  }
}

variable "public_access_cidrs" {
  description = "LAB ONLY: IPv4 CIDRs allowed to reach the public EKS API endpoint; narrow this for demonstrations when possible."
  type        = list(string)
  default     = ["0.0.0.0/0"]

  validation {
    condition = (
      length(var.public_access_cidrs) > 0 &&
      length(distinct(var.public_access_cidrs)) == length(var.public_access_cidrs) &&
      alltrue([
        for cidr in var.public_access_cidrs :
        can(regex("^([0-9]{1,3}\\.){3}[0-9]{1,3}/([0-9]|[12][0-9]|3[0-2])$", cidr)) &&
        can(cidrnetmask(cidr))
      ])
    )
    error_message = "public_access_cidrs must contain one or more distinct valid IPv4 CIDR blocks."
  }
}
