output "url" {
  value = aws_apigatewayv2_stage.default.invoke_url
}

output "token_url" {
  description = "Where clients trade their id and secret for an access token."
  value       = "https://${aws_cognito_user_pool_domain.clients.domain}.auth.${data.aws_region.current.region}.amazoncognito.com/oauth2/token"
}

output "clients" {
  description = "Each client's id and secret, to hand to the third party."
  sensitive   = true
  value = {
    for name, client in aws_cognito_user_pool_client.clients : name => {
      client_id     = client.id
      client_secret = client.client_secret
    }
  }
}
