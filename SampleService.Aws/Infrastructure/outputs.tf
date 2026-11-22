output "things_table_name" {
  value = aws_dynamodb_table.things.name
}

output "things_policy_json" {
  description = "Grants what DynamoThingsPersistence does to the things table."
  value       = data.aws_iam_policy_document.things.json
}

output "things_read_policy_json" {
  description = "Grants what DynamoThingsPersistence does to read the things table."
  value       = data.aws_iam_policy_document.things_read.json
}
