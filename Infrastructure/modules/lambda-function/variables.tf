variable "name" {
  description = "The function's name, which also names its role and log group."
  type        = string
}

variable "package_zip" {
  description = "The function's code: a zip of what dotnet publish wrote, from the service's package."
  type        = string
}

variable "handler" {
  description = "The handler, as <assembly>::<type>::<method>."
  type        = string
}

variable "environment" {
  description = "The function's environment variables."
  type        = map(string)
  default     = {}
}

variable "policy_json" {
  description = "An IAM policy granting what the function's code uses, beyond writing logs."
  type        = string
}

variable "timeout" {
  description = "Seconds the function may run per invocation."
  type        = number
  default     = 30
}

variable "memory_size" {
  description = "Megabytes of memory, which also sets the function's share of CPU."
  type        = number
  default     = 512
}
