resource "random_password" "database" {
  length  = 32
  special = false
}

resource "random_password" "jwt" {
  length  = 64
  special = false
}

resource "random_password" "webhook" {
  length  = 64
  special = false
}

resource "random_password" "bootstrap_initial" {
  length           = 24
  special          = true
  override_special = "!@#%_-"
  min_upper        = 1
  min_lower        = 1
  min_numeric      = 1
  min_special      = 1
}

resource "random_password" "bootstrap_active" {
  length           = 24
  special          = true
  override_special = "!@#%_-"
  min_upper        = 1
  min_lower        = 1
  min_numeric      = 1
  min_special      = 1
}

resource "aws_secretsmanager_secret" "database" {
  name                    = "garageflow/academy/database"
  recovery_window_in_days = 0
  tags                    = var.tags
}

resource "aws_secretsmanager_secret_version" "database" {
  secret_id = aws_secretsmanager_secret.database.id
  secret_string = jsonencode({
    username = "garageflowadmin"
    database = "garageflow"
    password = random_password.database.result
  })
}

resource "aws_secretsmanager_secret" "jwt" {
  name                    = "garageflow/academy/jwt"
  recovery_window_in_days = 0
  tags                    = var.tags
}

resource "aws_secretsmanager_secret_version" "jwt" {
  secret_id     = aws_secretsmanager_secret.jwt.id
  secret_string = random_password.jwt.result
}

resource "aws_secretsmanager_secret" "bootstrap" {
  name                    = "garageflow/academy/bootstrap"
  recovery_window_in_days = 0
  tags                    = var.tags
}

resource "aws_secretsmanager_secret_version" "bootstrap" {
  secret_id = aws_secretsmanager_secret.bootstrap.id
  secret_string = jsonencode({
    email           = trimspace(var.bootstrap_admin_email)
    initialPassword = random_password.bootstrap_initial.result
    activePassword  = random_password.bootstrap_active.result
  })
}

resource "aws_secretsmanager_secret" "webhook" {
  name                    = "garageflow/academy/webhook"
  recovery_window_in_days = 0
  tags                    = var.tags
}

resource "aws_secretsmanager_secret_version" "webhook" {
  secret_id     = aws_secretsmanager_secret.webhook.id
  secret_string = random_password.webhook.result
}
