# The public API: an HTTP API with one route per operation in openapi.yaml, all reaching one function,
# and the Cognito user pool that issues third-party clients their access tokens.

module "function" {
  source = "../../Infrastructure/modules/lambda-function"

  name        = "${var.name_prefix}-public-api"
  package_zip = "${path.module}/../../functions/SampleService.PublicApi.zip"
  handler     = "SampleService.PublicApi::SampleService.PublicApi.Function::Handle"
  policy_json = var.things_read_policy_json
  environment = {
    THINGS_TABLE = var.things_table_name
  }
}

data "aws_region" "current" {}

# Clients, not people: each third party is an app client that trades its id and secret for an access
# token by the client-credentials flow.
resource "aws_cognito_user_pool" "clients" {
  name = "${var.name_prefix}-public-api-clients"
}

resource "aws_cognito_user_pool_domain" "clients" {
  domain       = var.auth_domain
  user_pool_id = aws_cognito_user_pool.clients.id
}

# Defines the scopes tokens can grant, such as sample/things.read.
resource "aws_cognito_resource_server" "sample" {
  user_pool_id = aws_cognito_user_pool.clients.id
  identifier   = "sample"
  name         = "Sample"

  scope {
    scope_name        = "things.read"
    scope_description = "Read things"
  }
}

resource "aws_cognito_user_pool_client" "clients" {
  for_each = var.clients

  name                                 = each.key
  user_pool_id                         = aws_cognito_user_pool.clients.id
  generate_secret                      = true
  allowed_oauth_flows                  = ["client_credentials"]
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_scopes                 = each.value

  depends_on = [aws_cognito_resource_server.sample]
}

resource "aws_apigatewayv2_api" "api" {
  name          = "${var.name_prefix}-public-api"
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_integration" "function" {
  api_id                 = aws_apigatewayv2_api.api.id
  integration_type       = "AWS_PROXY"
  integration_uri        = module.function.invoke_arn
  payload_format_version = "2.0"
}

# Accepts only access tokens the pool issued to one of the clients.
resource "aws_apigatewayv2_authorizer" "clients" {
  api_id           = aws_apigatewayv2_api.api.id
  name             = "clients"
  authorizer_type  = "JWT"
  identity_sources = ["$request.header.Authorization"]

  jwt_configuration {
    issuer   = "https://${aws_cognito_user_pool.clients.endpoint}"
    audience = [for client in aws_cognito_user_pool_client.clients : client.id]
  }
}

# One route per operation in openapi.yaml, each requiring its scope.
resource "aws_apigatewayv2_route" "get_things" {
  api_id               = aws_apigatewayv2_api.api.id
  route_key            = "GET /v1/things"
  target               = "integrations/${aws_apigatewayv2_integration.function.id}"
  authorization_type   = "JWT"
  authorizer_id        = aws_apigatewayv2_authorizer.clients.id
  authorization_scopes = ["sample/things.read"]
}

# Throttles all clients together. Quotas per client would need API keys on a REST API, or WAF rules.
resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.api.id
  name        = "$default"
  auto_deploy = true

  default_route_settings {
    throttling_rate_limit  = var.throttling_rate_limit
    throttling_burst_limit = var.throttling_burst_limit
  }
}

resource "aws_lambda_permission" "api" {
  action        = "lambda:InvokeFunction"
  function_name = module.function.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.api.execution_arn}/*/*"
}
