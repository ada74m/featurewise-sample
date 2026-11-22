variable "region" {
  type    = string
  default = "eu-west-2"
}

variable "name_prefix" {
  description = "Prefixes every resource's name: your own, so your stack doesn't collide with anyone else's."
  type        = string
}

variable "dev_users" {
  description = "The people to create in the dev user pool, by username, each with a generated password. Editors join the editor group, and may delete Things."
  type = map(object({
    editor = optional(bool, false)
  }))
  default = {
    ann = { editor = true }
    bob = {}
  }
}
