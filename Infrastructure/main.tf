# The sample service: the AWS facilities' resources and each entrypoint's, from the modules beside each project.
# This is the module a package carries, with each function's zip in its functions folder. An environment
# supplies the provider, the state, and the inputs that differ between environments.

data "aws_caller_identity" "current" {}

locals {
  # The service's id, as Service.Id has it in the code: it qualifies every link's feature, as in
  # sample/Things, and names the shell's folder for its module.
  service = "sample"

  # Every resource is named <environment>-<service>-<what it is>, so services sharing an environment,
  # and the UI shell beside them, never take each other's names.
  name_prefix = "${var.name_prefix}-${local.service}"
}

module "aws" {
  source = "../SampleService.Aws/Infrastructure"

  name_prefix = local.name_prefix
}

module "ui_backend" {
  source = "../SampleService.UiBackend/Infrastructure"

  name_prefix        = local.name_prefix
  things_table_name  = module.aws.things_table_name
  things_policy_json = module.aws.things_policy_json
  jwt_issuer         = var.jwt_issuer
  jwt_audience       = var.jwt_audience
}

module "public_api" {
  source = "../SampleService.PublicApi/Infrastructure"

  name_prefix             = local.name_prefix
  things_table_name       = module.aws.things_table_name
  things_read_policy_json = module.aws.things_read_policy_json
  auth_domain             = coalesce(var.public_api_auth_domain, "${local.name_prefix}-public-api-${data.aws_caller_identity.current.account_id}")
  clients                 = var.public_api_clients
}

locals {
  restore = jsonencode({ "$worker" = "${local.service}/HandleRestoreDue" })
}

module "scheduled_worker" {
  source = "../SampleService.ScheduledWorker/Infrastructure"

  name_prefix        = local.name_prefix
  things_table_name  = module.aws.things_table_name
  things_policy_json = module.aws.things_policy_json
  timezone           = var.timezone
  schedules = {
    restore = {
      expression = "cron(0 3 * * ? *)"
      link       = local.restore
    }
  }
}

# The service's module for the UI shell, in the shell's bucket, when the environment has a shell.
module "ui" {
  source = "../SampleService.Ui/Infrastructure"
  count  = var.shell_bucket == null ? 0 : 1

  shell_bucket = var.shell_bucket
  service_id   = local.service
}

# Restores the Things once when the service is first deployed, so a new environment doesn't wait for the
# schedule to have any.
resource "aws_lambda_invocation" "restore" {
  function_name = module.scheduled_worker.function_name
  input = jsonencode({
    Schedule = "deploy"
    Link     = jsondecode(local.restore)
  })
}
