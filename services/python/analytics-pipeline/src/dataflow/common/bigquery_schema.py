from google.cloud import bigquery

bigquery_table_schema_dict = {
    'general': [
        bigquery.SchemaField(name='analytics_environment', field_type='STRING', mode='NULLABLE', description='Environment derived from the GCS path.'),
        bigquery.SchemaField(name='event_environment', field_type='STRING', mode='NULLABLE', description='The build configuration that the event was sent from, e.g. {debug, profile, release}.'),
        bigquery.SchemaField(name='event_source', field_type='STRING', mode='NULLABLE', description='Type of the worker the event originated from.'),
        bigquery.SchemaField(name='session_id', field_type='STRING', mode='NULLABLE', description='The session ID, which is unique per client/server worker session.'),
        bigquery.SchemaField(name='version_id', field_type='STRING', mode='NULLABLE', description="The version of the game's build or online service."),
        bigquery.SchemaField(name='batch_id', field_type='STRING', mode='NULLABLE', description='MD5 hexdigest of the GCS filepath.'),
        bigquery.SchemaField(name='event_id', field_type='STRING', mode='NULLABLE', description='MD5 hexdigest of the GCS filepath + "/{event_index_in_batch}".'),
        bigquery.SchemaField(name='event_index', field_type='INTEGER', mode='NULLABLE', description='The index of the event within its batch.'),
        bigquery.SchemaField(name='event_class', field_type='STRING', mode='NULLABLE', description='Higher order category of event.'),
        bigquery.SchemaField(name='event_type', field_type='STRING', mode='NULLABLE', description='The event type.'),
        bigquery.SchemaField(name='player_id', field_type='STRING', mode='NULLABLE', description="A player's unique identifier, if available."),
        bigquery.SchemaField(name='event_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='PARTITION - The UTC timestamp when the event took place.'),
        bigquery.SchemaField(name='received_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='The UTC timestamp when the event was received.'),
        bigquery.SchemaField(name='inserted_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='The UTC timestamp when the event was ingested into BQ.'),
        bigquery.SchemaField(name='job_name', field_type='STRING', mode='NULLABLE', description='The name of the data pipeline or function that ingested the event into BQ.'),
        bigquery.SchemaField(name='event_attributes', field_type='STRING', mode='NULLABLE', description='Custom data for the event.')
    ],
    'playfab': [
        bigquery.SchemaField(name='analytics_environment', field_type='STRING', mode='NULLABLE', description='Environment derived from the GCS path.'),
        bigquery.SchemaField(name='playfab_environment', field_type='STRING', mode='NULLABLE', description='Your PlayFab environment.'),
        bigquery.SchemaField(name='source_type', field_type='STRING', mode='NULLABLE', description='The type of source of this event (PlayFab partner, other backend, or from the PlayFab API).'),
        bigquery.SchemaField(name='source', field_type='STRING', mode='NULLABLE', description='The name of the source of this PlayStream event.'),
        bigquery.SchemaField(name='event_namespace', field_type='STRING', mode='NULLABLE', description="The assigned namespacing for this event. For example: 'com.myprogram.ads'"),
        bigquery.SchemaField(name='title_id', field_type='STRING', mode='NULLABLE', description='The ID of your PlayFab title.'),
        bigquery.SchemaField(name='batch_id', field_type='STRING', mode='NULLABLE', description='MD5 hexdigest of the GCS filepath.'),
        bigquery.SchemaField(name='event_id', field_type='STRING', mode='NULLABLE', description='PlayFab event ID.'),
        bigquery.SchemaField(name='event_name', field_type='STRING', mode='NULLABLE', description='The name of this event.'),
        bigquery.SchemaField(name='entity_type', field_type='STRING', mode='NULLABLE', description='The type of entity (player, title, etc.) to which this event applies.'),
        bigquery.SchemaField(name='entity_id', field_type='STRING', mode='NULLABLE', description='The identifier for the entity (title, player, etc) to which this event applies.'),
        bigquery.SchemaField(name='event_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='PARTITION - The UTC timestamp when the event took place.'),
        bigquery.SchemaField(name='received_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='The UTC timestamp when the event was received.'),
        bigquery.SchemaField(name='inserted_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='The UTC timestamp when the event was ingested into BQ.'),
        bigquery.SchemaField(name='job_name', field_type='STRING', mode='NULLABLE', description='The name of the data pipeline or function that ingested the event into BQ.'),
        bigquery.SchemaField(name='event_attributes', field_type='STRING', mode='NULLABLE', description='Custom data for the event.')
    ],
    'logs': [
       bigquery.SchemaField(name='job_name', field_type='STRING', mode='NULLABLE', description='Job name.'),
       bigquery.SchemaField(name='processed_timestamp', field_type='TIMESTAMP', mode='NULLABLE', description='The UTC timestamp when the event batch file was parsed.'),
       bigquery.SchemaField(name='batch_id', field_type='STRING', mode='NULLABLE', description='MD5 hexdigest of the GCS filepath.'),
       bigquery.SchemaField(name='analytics_environment', field_type='STRING', mode='NULLABLE', description='The value of `analytics_environment` in the GCS path.'),
       bigquery.SchemaField(name='event_category', field_type='STRING', mode='NULLABLE', description='The value of `event_category` in the GCS path.'),
       bigquery.SchemaField(name='event_ds', field_type='DATE', mode='NULLABLE', description='PARTITION - The value of `event_ds` in the GCS path.'),
       bigquery.SchemaField(name='event_time', field_type='STRING', mode='NULLABLE', description='The value of `event_time` in the GCS path.'),
       bigquery.SchemaField(name='event', field_type='STRING', mode='NULLABLE', description='The event type.'),
       bigquery.SchemaField(name='gspath', field_type='STRING', mode='NULLABLE', description='The full GCS path of the event file.')
   ]
}
