output "ui_backend_url" {
  value = module.ui_backend.url
}

output "public_api_url" {
  value = module.public_api.url
}

output "public_api_token_url" {
  value = module.public_api.token_url
}

output "public_api_clients" {
  sensitive = true
  value     = module.public_api.clients
}

output "things_table_name" {
  value = module.aws.things_table_name
}
