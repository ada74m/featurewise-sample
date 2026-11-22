output "ui_backend_url" {
  value = module.sample.ui_backend_url
}

output "public_api_url" {
  value = module.sample.public_api_url
}

output "public_api_token_url" {
  value = module.sample.public_api_token_url
}

output "public_api_clients" {
  sensitive = true
  value     = module.sample.public_api_clients
}

output "dev_ui_client_id" {
  description = "The dev user pool's app client, for signing in from the AWS CLI."
  value       = aws_cognito_user_pool_client.dev.id
}

output "dev_user_passwords" {
  description = "Each dev user's password, by username."
  sensitive   = true
  value       = { for id, password in random_password.dev_users : id => password.result }
}
