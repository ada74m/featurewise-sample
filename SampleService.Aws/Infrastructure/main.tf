# What the AWS facilities need, whichever function uses them: the things table, for DynamoThingsPersistence.

resource "aws_dynamodb_table" "things" {
  name         = "${var.name_prefix}-things"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"

  attribute {
    name = "Id"
    type = "S"
  }

  point_in_time_recovery {
    enabled = true
  }
}

# What DynamoThingsPersistence does, for each function's policy.
data "aws_iam_policy_document" "things" {
  statement {
    actions   = ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:DeleteItem", "dynamodb:Scan"]
    resources = [aws_dynamodb_table.things.arn]
  }
}

# Only what DynamoThingsPersistence does to read, for functions that never write.
data "aws_iam_policy_document" "things_read" {
  statement {
    actions   = ["dynamodb:GetItem", "dynamodb:Scan"]
    resources = [aws_dynamodb_table.things.arn]
  }
}
