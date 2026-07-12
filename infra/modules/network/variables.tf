variable "availability_zones" {
  description = "The two availability zones used by the public and private database subnets."
  type        = list(string)

  validation {
    condition = (
      length(var.availability_zones) == 2 &&
      length(distinct([for az in var.availability_zones : trimspace(az)])) == 2 &&
      alltrue([
        for az in var.availability_zones :
        can(regex("^([a-z]{2}(-[a-z0-9]+)+-[0-9]+)[a-z]$", trimspace(az)))
      ]) &&
      replace(trimspace(var.availability_zones[0]), "/[a-z]$/", "") == replace(trimspace(var.availability_zones[1]), "/[a-z]$/", "")
    )
    error_message = "availability_zones must contain exactly two distinct standard availability zones from the same region."
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
