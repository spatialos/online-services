# This file creates a .zip file with everything required to run our Cloud Function.

data "archive_file" "cloud_function_general_schema" {
  type        = "zip"
  output_path = "${path.module}/../../python/analytics-pipeline/cloud-function-general-schema-${var.environment}.zip"

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/functions/general/main.py")}"
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
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/dataflow/common/bigquery_schema.py")}"
    filename = "common/bigquery_schema.py"
  }

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/dataflow/common/functions.py")}"
    filename = "common/functions.py"
  }
}

data "archive_file" "cloud_function_playfab_schema" {
  type        = "zip"
  output_path = "${path.module}/../../python/analytics-pipeline/cloud-function-playfab-schema-${var.environment}.zip"

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/functions/playfab/main.py")}"
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
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/dataflow/common/bigquery_schema.py")}"
    filename = "common/bigquery_schema.py"
  }

  source {
    content  = "${file("${path.module}/../../python/analytics-pipeline/src/dataflow/common/functions.py")}"
    filename = "common/functions.py"
  }
}
