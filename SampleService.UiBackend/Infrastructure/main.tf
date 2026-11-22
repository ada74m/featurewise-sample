# The UI backend: an HTTP API whose routes, POST /{feature} and GET /{feature}, reach one function.

module "function" {
  source = "../../Infrastructure/modules/lambda-function"

  name        = "${var.name_prefix}-ui-backend"
  package_zip = "${path.module}/../../functions/SampleService.UiBackend.zip"
  handler     = "SampleService.UiBackend::SampleService.UiBackend.Function::Handle"
  policy_json = var.things_policy_json
  environment = {
    THINGS_TABLE = var.things_table_name
  }
}

resource "aws_apigatewayv2_api" "api" {
  name          = "${var.name_prefix}-ui-backend"
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_integration" "function" {
  api_id                 = aws_apigatewayv2_api.api.id
  integration_type       = "AWS_PROXY"
  integration_uri        = module.function.invoke_arn
  payload_format_version = "2.0"
}

# Verifies callers' tokens, so the function trusts the claims it's given. API Gateway refuses a request
# without a valid token before it reaches the function.
resource "aws_apigatewayv2_authorizer" "jwt" {
  api_id           = aws_apigatewayv2_api.api.id
  name             = "jwt"
  authorizer_type  = "JWT"
  identity_sources = ["$request.header.Authorization"]

  jwt_configuration {
    issuer   = var.jwt_issuer
    audience = var.jwt_audience
  }
}

# Every feature, query or command, by POST with its arguments as the body, and queries by GET with
# them in the link parameter. The function answers a GET for a command with 405.
resource "aws_apigatewayv2_route" "features" {
  for_each = toset(["POST", "GET"])

  api_id             = aws_apigatewayv2_api.api.id
  route_key          = "${each.key} /{feature}"
  target             = "integrations/${aws_apigatewayv2_integration.function.id}"
  authorization_type = "JWT"
  authorizer_id      = aws_apigatewayv2_authorizer.jwt.id
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.api.id
  name        = "$default"
  auto_deploy = true
}

resource "aws_lambda_permission" "api" {
  action        = "lambda:InvokeFunction"
  function_name = module.function.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.api.execution_arn}/*/*"
}
