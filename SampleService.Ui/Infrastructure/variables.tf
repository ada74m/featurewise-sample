variable "shell_bucket" {
  description = "The UI shell's bucket, which its distribution serves."
  type        = string
}

variable "service_id" {
  description = "The service's id, which names the folder the shell loads its module from."
  type        = string
}
