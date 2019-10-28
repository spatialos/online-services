from google.cloud import bigquery

bigquery_table_schema_dict = {
'events': [
  bigquery.SchemaField(name='analytics_environment', field_type='STRING', mode='NULLABLE', description='Environment derived from the GCS path.'),
  bigquery.SchemaField(name='event_environment', field_type='STRING', mode='NULLABLE', description='The environment the event originated from.'),
  bigquery.SchemaField(name='event_source', field_type='STRING', mode='NULLABLE', description='Type of the worker the event originated from.'),
  bigquery.SchemaField(name='session_id', field_type='STRING', mode='NULLABLE', description='The session ID, which is unique per client/server worker session.'),
  bigquery.SchemaField(name='version_id', field_type='STRING', mode='NULLABLE', description="The version of the game's build or online service."),
  bigquery.SchemaField(name='batch_id', field_type='STRING', mode='NULLABLE', description='MD5 hash of the GCS filepath.'),
  bigquery.SchemaField(name='event_id', field_type='STRING', mode='NULLABLE', description='MD5 hash of the GCS filepath + "/{event_index_in_batch}".'),
  bigquery.SchemaField(name='event_index', field_type='INTEGER', mode='NULLABLE', description='The index of the event within its batch.'),
  bigquery.SchemaField(name='event_class', field_type='STRING', mode='NULLABLE', description='Higher order category of event.'),
  bigquery.SchemaField(name='event_type', field_type='STRING', mode='NULLABLE', description='The event type.'),
  bigquery.SchemaField(name='player_id', field_type='STRING', mode='NULLABLE', description="A player's unique identifier, if available."),
  bigquery.SchemaField(name='event_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='PARTITION - The timestamp of the event.'),
  bigquery.SchemaField(name='received_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='The timestamp of when the event was received.'),
  bigquery.SchemaField(name='inserted_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='The timestamp of when the event was ingested into BQ.'),
  bigquery.SchemaField(name='job_name', field_type='STRING', mode='NULLABLE', description='The name of the data pipeline or function that ingested the event into BQ.'),
  bigquery.SchemaField(name='event_attributes', field_type='STRING', mode='NULLABLE', description='Custom data for the event.')
 ],
 'logs': [
  bigquery.SchemaField(name='job_name', field_type='STRING', mode='NULLABLE', description='Job name.'),
  bigquery.SchemaField(name='processed_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='Time when event file was parsed.'),
  bigquery.SchemaField(name='batch_id', field_type='STRING', mode='NULLABLE', description='The batchId.'),
  bigquery.SchemaField(name='analytics_environment', field_type='STRING', mode='NULLABLE', description='Analytics environment of the GCS path.'),
  bigquery.SchemaField(name='event_category', field_type='STRING', mode='NULLABLE', description='Event category of the GCS path.'),
  bigquery.SchemaField(name='event_ds', field_type='DATE', mode='NULLABLE', description='PARTITION - Event ds of the GCS path.'),
  bigquery.SchemaField(name='event_time', field_type='STRING', mode='NULLABLE', description='Event time of the GCS path.'),
  bigquery.SchemaField(name='event', field_type='STRING', mode='NULLABLE', description='Event'),
  bigquery.SchemaField(name='gspath', field_type='STRING', mode='NULLABLE', description='GCS path of the event file.')
 ]
}
