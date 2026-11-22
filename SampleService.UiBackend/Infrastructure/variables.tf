variable "name_prefix" {
  description = "Prefixes every resource's name."
  type        = string
}

variable "things_table_name" {
  type = string
}

variable "things_policy_json" {
  description = "Grants the function what DynamoThingsPersistence does."
  type        = string
}

variable "jwt_issuer" {
  description = "The issuer of callers' tokens, such as a Cognito user pool's URL."
  type        = string
}

variable "jwt_audience" {
  description = "The audiences a caller's token may be for, such as a Cognito app client's id."
  type        = list(string)
}
