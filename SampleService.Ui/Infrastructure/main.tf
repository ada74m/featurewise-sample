# The service's module for the UI shell, uploaded to the shell's bucket under modules/sample/, where the
# shell loads it from. It deploys with the service, so the module and its backend always match.

locals {
  dist = "${path.module}/../dist"

  content_types = {
    js  = "text/javascript; charset=utf-8"
    css = "text/css; charset=utf-8"
    map = "application/json"
  }
}

resource "aws_s3_object" "module" {
  for_each = fileset(local.dist, "**")

  bucket        = var.shell_bucket
  key           = "modules/${var.service_id}/${each.value}"
  source        = "${local.dist}/${each.value}"
  etag          = filemd5("${local.dist}/${each.value}")
  content_type  = lookup(local.content_types, reverse(split(".", each.value))[0], "application/octet-stream")
  cache_control = "no-cache"
}
