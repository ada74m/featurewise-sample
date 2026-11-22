variable "name_prefix" {
  description = "The environment's prefix, such as adam-scratchpad. The service adds its own id, so its resources are named <name_prefix>-sample-…, apart from other services' and the UI shell's."
  type        = string
}

variable "jwt_issuer" {
  description = "The issuer of UI users' tokens: the estate's sign-in, such as a Cognito user pool's URL."
  type        = string
}

variable "jwt_audience" {
  description = "The audiences a UI user's token may be for, such as the UI's Cognito app client id."
  type        = list(string)
}

variable "public_api_auth_domain" {
  description = "The prefix of the public API's token endpoint domain, which must be unique in the region across all AWS accounts. Defaults to <name_prefix>-sample-public-api-<account id>."
  type        = string
  default     = null
}

variable "public_api_clients" {
  description = "The third-party clients of the public API, each mapped to the scopes it's granted."
  type        = map(list(string))
  default = {
    example-partner = ["sample/things.read"]
  }
}

variable "timezone" {
  description = "The time zone the job schedules are in."
  type        = string
  default     = "Europe/London"
}

variable "shell_bucket" {
  description = "The UI shell's bucket, where the service uploads its module under modules/sample/. Without one, the service has no UI, only its backends."
  type        = string
  default     = null
}
