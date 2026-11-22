variable "name_prefix" {
  description = "Prefixes every resource's name."
  type        = string
}

variable "things_table_name" {
  type = string
}

variable "things_read_policy_json" {
  description = "Grants the function what DynamoThingsPersistence does to read."
  type        = string
}

variable "auth_domain" {
  description = "The prefix of the token endpoint's domain, <auth_domain>.auth.<region>.amazoncognito.com, which must be unique in the region."
  type        = string
}

variable "clients" {
  description = "The third-party clients that may call the API, each mapped to the scopes it's granted, such as [\"sample/things.read\"]. Each gets a client id and secret."
  type        = map(list(string))
}

variable "throttling_rate_limit" {
  description = "Requests per second the API accepts across all clients."
  type        = number
  default     = 50
}

variable "throttling_burst_limit" {
  description = "Requests the API accepts in a burst across all clients."
  type        = number
  default     = 100
}
