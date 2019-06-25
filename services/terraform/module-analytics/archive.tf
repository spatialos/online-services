# This file creates a .zip file with everything required to run our Cloud Function.

data "archive_file" "cloud_function_analytics" {
  type        = "zip"
  output_path = "${path.module}/../../python/analytics-pipeline/cloud-function-analytics.zip"

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/function/main.py")}"
    filename = "main.py"
  }

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/requirements/function.txt")}"
    filename = "requirements.txt"
  }

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/dataflow/common/__init__.py")}"
    filename = "common/__init__.py"
  }

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/dataflow/common/bigquery.py")}"
    filename = "common/bigquery.py"
  }

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/dataflow/common/parser.py")}"
    filename = "common/parser.py"
  }

}
