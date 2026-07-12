resource "aws_db_subnet_group" "this" {
  name       = "${var.identifier}-subnets"
  subnet_ids = var.private_subnet_ids

  tags = merge(var.tags, {
    Name = "${var.identifier}-subnets"
  })
}

resource "aws_security_group" "database" {
  name        = "${var.identifier}-database"
  description = "Allow PostgreSQL access only from the GarageFlow EKS cluster"
  vpc_id      = var.vpc_id

  tags = merge(var.tags, {
    Name = "${var.identifier}-database"
  })
}

resource "aws_vpc_security_group_ingress_rule" "postgres_from_eks" {
  security_group_id            = aws_security_group.database.id
  description                  = "PostgreSQL from EKS"
  from_port                    = 5432
  to_port                      = 5432
  ip_protocol                  = "tcp"
  referenced_security_group_id = var.eks_security_group_id
  tags                         = var.tags
}

resource "aws_db_instance" "this" {
  identifier                   = var.identifier
  engine                       = "postgres"
  engine_version               = "17"
  instance_class               = "db.t3.micro"
  allocated_storage            = 20
  storage_type                 = "gp2"
  storage_encrypted            = true
  multi_az                     = false
  publicly_accessible          = false
  monitoring_interval          = 0
  performance_insights_enabled = false
  deletion_protection          = false
  skip_final_snapshot          = true
  backup_retention_period      = 0
  apply_immediately            = true

  db_name  = "garageflow"
  username = "garageflowadmin"
  password = var.database_password
  port     = 5432

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [aws_security_group.database.id]

  tags = merge(var.tags, {
    Name = var.identifier
  })
}
