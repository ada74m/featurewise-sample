# A dev user pool stands in for the estate's sign-in, which a UI shell shares across services. It has an
# editor group, whose members may delete Things, and the people in dev_users, each with a generated
# password.

resource "aws_cognito_user_pool" "dev" {
  name = "${var.name_prefix}-dev-users"
}

# Signs in with a username and password, so a token can be had from the AWS CLI. A real UI signs in
# through the hosted sign-in page instead.
resource "aws_cognito_user_pool_client" "dev" {
  name                = "${var.name_prefix}-dev-ui"
  user_pool_id        = aws_cognito_user_pool.dev.id
  explicit_auth_flows = ["ALLOW_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH"]
}

resource "aws_cognito_user_group" "editors" {
  name         = "editor"
  user_pool_id = aws_cognito_user_pool.dev.id
}

resource "random_password" "dev_users" {
  for_each = var.dev_users

  length      = 20
  min_upper   = 1
  min_lower   = 1
  min_numeric = 1
  min_special = 1
}

resource "aws_cognito_user" "dev" {
  for_each = var.dev_users

  user_pool_id = aws_cognito_user_pool.dev.id
  username     = each.key
  password     = random_password.dev_users[each.key].result
}

resource "aws_cognito_user_in_group" "dev_editors" {
  for_each = { for id, user in var.dev_users : id => user if user.editor }

  user_pool_id = aws_cognito_user_pool.dev.id
  group_name   = aws_cognito_user_group.editors.name
  username     = aws_cognito_user.dev[each.key].username
}
