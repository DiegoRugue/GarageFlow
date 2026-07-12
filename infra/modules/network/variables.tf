variable "availability_zones" {
  description = "The two availability zones used by the public and private database subnets."
  type        = list(string)

  validation {
    condition = (
      length(var.availability_zones) == 2 &&
      length(distinct(var.availability_zones)) == 2 &&
      alltrue([for az in var.availability_zones : trimspace(az) != ""])
    )
    error_message = "availability_zones must contain exactly two distinct, nonblank availability zone names."
  }
}

variable "cluster_name" {
  description = "EKS cluster name used to tag public subnets for load balancer discovery."
  type        = string

  validation {
    condition     = trimspace(var.cluster_name) != ""
    error_message = "cluster_name must not be blank."
  }
}

variable "tags" {
  description = "Additional non-sensitive tags to apply to network resources."
  type        = map(string)
  default     = {}
}
