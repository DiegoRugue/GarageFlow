data "aws_availability_zones" "available" {
  state = "available"
}

locals {
  availability_zones = slice(sort(data.aws_availability_zones.available.names), 0, 2)
}

module "network" {
  source = "../../modules/network"

  availability_zones = local.availability_zones
  cluster_name       = var.cluster_name
  tags               = local.default_tags
}

module "secrets" {
  source = "../../modules/secrets"

  bootstrap_admin_email = var.bootstrap_admin_email
  tags                  = local.default_tags
}

module "eks" {
  source = "../../modules/eks"

  cluster_name        = var.cluster_name
  kubernetes_version  = var.kubernetes_version
  public_subnet_ids   = module.network.public_subnet_ids
  cluster_role_arn    = var.eks_cluster_role_arn
  node_role_arn       = var.eks_node_role_arn
  public_access_cidrs = var.public_access_cidrs
  tags                = local.default_tags
}

module "rds" {
  source = "../../modules/rds"

  identifier            = "garageflow-academy"
  vpc_id                = module.network.vpc_id
  private_subnet_ids    = module.network.db_subnet_ids
  eks_security_group_id = module.eks.cluster_security_group_id
  database_password     = module.secrets.database_password
  tags                  = local.default_tags
}

module "ecr" {
  source = "../../modules/ecr"

  repository_name = "garageflow"
  tags            = local.default_tags
}

module "sns" {
  source = "../../modules/sns"

  topic_name         = "garageflow-work-orders"
  notification_email = var.notification_email
  tags               = local.default_tags
}
