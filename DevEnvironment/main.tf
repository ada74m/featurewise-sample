# A developer's own environment, deployed from a laptop by deploy.sh. It uses the package
# package.sh builds, unpacked in out/sample, the same as a real environment uses a published one.
# State is local. Real environments live in the environments repo, with state in S3.

module "sample" {
  source = "../out/sample/Infrastructure"

  name_prefix  = var.name_prefix
  jwt_issuer   = "https://${aws_cognito_user_pool.dev.endpoint}"
  jwt_audience = [aws_cognito_user_pool_client.dev.id]
}
