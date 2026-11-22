output "schedule_names" {
  value = [for schedule in aws_scheduler_schedule.jobs : schedule.name]
}

output "function_name" {
  description = "For invoking a job outside its schedule, as the service does once when it's deployed."
  value       = module.function.function_name
}
