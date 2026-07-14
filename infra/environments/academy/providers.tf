terraform {
  required_version = "= 1.15.7"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "= 6.49.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "= 3.9.0"
    }
  }
}

locals {
  default_tags = {
    Project     = "GarageFlow"
    Phase       = "2"
    Environment = "academy"
    Owner       = trimspace(var.owner)
    ExpiresOn   = var.expires_on
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = local.default_tags
  }
}
