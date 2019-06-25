
def provisionBigQuery(client_bq, type, partitioned = False):
    from google.cloud.exceptions import NotFound
    from google.cloud import bigquery

    def dataset_exists(client, dataset_reference):
        try:
            client.get_dataset(dataset_reference)
            return True
        except NotFound:
            return False

    def table_exists(client, table_reference):
        try:
            client.get_table(table_reference)
            return True
        except NotFound:
            return False

    if partitioned == True:
        partition = 'PARTITION - '
    else:
        partition = ''

    # Create dataset if not exists..
    for i in [i for i in ['logs', 'events']]:
        dataset_ref = client_bq.dataset(i)
        if not dataset_exists(client = client_bq, dataset_reference = dataset_ref):
            dataset = bigquery.Dataset(dataset_ref)
            dataset = client_bq.create_dataset(dataset)

    schema_events = [
      bigquery.SchemaField(name = 'analytics_environment', field_type = 'STRING', mode = 'NULLABLE', description = 'Environment derived from the GCS path.'),
      bigquery.SchemaField(name = 'batch_id', field_type = 'STRING', mode = 'NULLABLE', description = 'MD5 hash of the GCS filepath.'),
      bigquery.SchemaField(name = 'event_id', field_type = 'STRING', mode = 'NULLABLE', description = 'The eventId'),
      bigquery.SchemaField(name = 'event_index', field_type = 'INTEGER', mode = 'NULLABLE', description = 'The index of the event within its batch.'),
      bigquery.SchemaField(name = 'event_source', field_type = 'STRING', mode = 'NULLABLE', description = 'Worker type the event originated from.'),
      bigquery.SchemaField(name = 'event_class', field_type = 'STRING', mode = 'NULLABLE', description = 'Higher order category of event.'),
      bigquery.SchemaField(name = 'event_type', field_type = 'STRING', mode = 'NULLABLE', description = 'The eventType.'),
      bigquery.SchemaField(name = 'session_id', field_type = 'STRING', mode = 'NULLABLE', description = 'The sessionId.'),
      bigquery.SchemaField(name = 'build_version', field_type = 'STRING', mode = 'NULLABLE', description = 'The version of the game\'s build.'),
      bigquery.SchemaField(name = 'event_environment', field_type = 'STRING', mode = 'NULLABLE', description = 'The environment the event originated from.'),
      bigquery.SchemaField(name = 'event_timestamp', field_type = 'TIMESTAMP', mode = 'NULLABLE', description = '{partition}The timestamp of the event.'.format(partition = partition)),
      bigquery.SchemaField(name = 'received_timestamp', field_type = 'TIMESTAMP', mode = 'NULLABLE', description = 'The timestamp of when the event was received.'),
      bigquery.SchemaField(name = 'inserted_timestamp', field_type = 'TIMESTAMP', mode = 'NULLABLE', description = 'The timestamp of when the event was ingested into BQ.'),
      bigquery.SchemaField(name = 'job_name', field_type = 'STRING', mode = 'NULLABLE', description = 'The name of the data pipeline or function that ingested the event into BQ.'),
      bigquery.SchemaField(name = 'event_attributes', field_type = 'STRING', mode = 'NULLABLE', description = 'Custom data for the event.')
     ]

    # Create events_{type} if not exists.
    table_ref_events = client_bq.dataset('events').table('events_' + type)
    if not table_exists(client = client_bq, table_reference = table_ref_events):
        table = bigquery.Table(table_ref_events, schema = schema_events)
        if partitioned == True:
            table.time_partitioning = bigquery.TimePartitioning(
            type_ = bigquery.TimePartitioningType.DAY,
            field = 'event_timestamp')
        table = client_bq.create_table(table)

    schema_logs = [
      bigquery.SchemaField(name = 'job_name', field_type = 'STRING', mode = 'NULLABLE', description = 'Job name.'),
      bigquery.SchemaField(name = 'processed_timestamp', field_type = 'TIMESTAMP', mode = 'NULLABLE', description = 'Time when event file was parsed.'),
      bigquery.SchemaField(name = 'batch_id', field_type = 'STRING', mode = 'NULLABLE', description = 'The batchId.'),
      bigquery.SchemaField(name = 'analytics_environment', field_type = 'STRING', mode = 'NULLABLE', description = 'Analytics environment of the GCS path.'),
      bigquery.SchemaField(name = 'event_category', field_type = 'STRING', mode = 'NULLABLE', description = 'Event category of the GCS path.'),
      bigquery.SchemaField(name = 'event_ds', field_type = 'DATE', mode = 'NULLABLE', description = '{partition}Event ds of the GCS path.'.format(partition = partition)),
      bigquery.SchemaField(name = 'event_time', field_type = 'STRING', mode = 'NULLABLE', description = 'Event time of the GCS path.'),
      bigquery.SchemaField(name = 'event', field_type = 'STRING', mode = 'NULLABLE', description = 'Event'),
      bigquery.SchemaField(name = 'file_path', field_type = 'STRING', mode = 'NULLABLE', description = 'GCS Path of the event file.')
     ]

    if type in ['batch', 'stream', 'function']:

        tables = ['events_logs_{type}'.format(type = type),
                  'events_logs_{type}_backfill'.format(type = type),
                  'events_debug_{type}'.format(type = type)]

        # Create tables if they do not exist..
        for i in tables:
            table_ref_logs = client_bq.dataset('logs').table(i)
            if not table_exists(client = client_bq, table_reference = table_ref_logs):
                table = bigquery.Table(table_ref_logs, schema = schema_logs)
                if partitioned == True:
                    table.time_partitioning = bigquery.TimePartitioning(
                    type_ = bigquery.TimePartitioningType.DAY,
                    field = 'event_ds')
                table = client_bq.create_table(table)

        return True
    else:
        print('Error - Type unknown {stream, batch, function}!')
        return False

def queryGenerator(gcp, table_type, ds_start, ds_stop, time_part_list, env_list, event_category, scale_test_name = ''):

    time_part_tuple = str(time_part_list).replace('[','(').replace(']',')')
    env_tuple = str(env_list).replace('[','(').replace(']',')')
    if scale_test_name != '':
        scale_test_logs = "AND file_path LIKE '%{scale_test_name}%'".format(scale_test_name = scale_test_name)
        scale_test_events = "AND event_type = '{scale_test_name}'".format(scale_test_name = scale_test_name)
    else:
        scale_test_logs, scale_test_events = '', ''

    query = """
    SELECT DISTINCT
      a.file_path
    FROM
        (
        SELECT DISTINCT
          file_path,
          batch_id
        FROM `{gcp}.logs.events_logs_{table_type}*`
        WHERE event = 'parse_initiated'
        AND event_ds BETWEEN '{ds_start}' AND '{ds_stop}'
        AND event_time IN {time_part_list}
        AND analytics_environment IN {env_list}
        AND event_category = '{event_category}'
        {scale_test_logs}
        ) a
    INNER JOIN
        (
        SELECT batch_id
        FROM `{gcp}.events.events_{table_type}*`
        WHERE analytics_environment IN {env_list}
        {scale_test_events}
        UNION DISTINCT
        SELECT batch_id
        FROM `{gcp}.logs.events_debug_{table_type}*`
        WHERE event_ds BETWEEN '{ds_start}' AND '{ds_stop}'
        AND event_time IN {time_part_list}
        AND analytics_environment IN {env_list}
        AND event_category = '{event_category}'
        {scale_test_logs}
        ) b
    ON a.batch_id = b.batch_id
    ;
    """.format(gcp = gcp, table_type = table_type, ds_start = ds_start, ds_stop = ds_stop, time_part_list = time_part_tuple,
               env_list = env_tuple, event_category = event_category, scale_test_logs = scale_test_logs, scale_test_events = scale_test_events)

    return query
