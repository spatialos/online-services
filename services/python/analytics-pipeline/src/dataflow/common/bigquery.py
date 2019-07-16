
def provisionBigQuery(client_bq, type, partitioned = True):

    """ This function provisions all required BigQuery Datasets & Tables.
    We combine datasets & tables within a single function as creating tables
    requires the same references when creating datasets.
    """

    if type not in ['batch', 'stream', 'function']:
        raise Exception('Error - Type unknown, must be one of: {stream, batch, function}!')

    from common.bigquery_schema import schema_events, schema_logs
    from google.cloud.exceptions import NotFound
    from google.cloud import bigquery

    def datasetExists(client, dataset_reference):
        try:
            client.get_dataset(dataset_reference)
            return True
        except NotFound:
            return False

    def tableExists(client, table_reference):
        try:
            client.get_table(table_reference)
            return True
        except NotFound:
            return False

    # Create dataset if not exists..
    for dataset_name in ['logs', 'events']:
        dataset_ref = client_bq.dataset(dataset_name)
        if not datasetExists(client=client_bq, dataset_reference=dataset_ref):
            dataset = bigquery.Dataset(dataset_ref)
            dataset = client_bq.create_dataset(dataset)

    # Create events_{type} if not exists.
    table_ref_events = client_bq.dataset('events').table('events_{type}'.format(type=type))
    if not tableExists(client=client_bq, table_reference=table_ref_events):
        table = bigquery.Table(table_ref_events, schema=schema_events)
        if partitioned:
            table.time_partitioning = bigquery.TimePartitioning(
              type_=bigquery.TimePartitioningType.DAY,
              field='event_timestamp')
        table = client_bq.create_table(table)

    # Create logs & debug tables if they do not exist..
    tables = ['events_logs_{type}'.format(type=type),
              'events_logs_{type}_backfill'.format(type=type),
              'events_debug_{type}'.format(type=type)]

    for table_name in tables:
        table_ref_logs = client_bq.dataset('logs').table(table_name)
        if not tableExists(client=client_bq, table_reference=table_ref_logs):
            table = bigquery.Table(table_ref_logs, schema=schema_logs)
            if partitioned:
                table.time_partitioning = bigquery.TimePartitioning(
                  type_=bigquery.TimePartitioningType.DAY,
                  field='event_ds')
            table = client_bq.create_table(table)

    return True


    def queryGenerator(gcp, table_type, ds_start, ds_stop, tuple_time_part, tuple_env, event_category, scale_test_name=''):

        """ This function generates a SQL query used to verify which files are
        already ingested into native BigQuery storage. Generally, the pipeline
        calling this function will omit these files from the total set of files
        it is tasked to ingest into native BigQuery storage.
        """

        if scale_test_name != '':
            scale_test_logs_filter = "AND file_path LIKE '%{scale_test_name}%'".format(scale_test_name=scale_test_name)
            scale_test_events_filter = "AND event_type = '{scale_test_name}'".format(scale_test_name=scale_test_name)
        else:
            scale_test_logs_filter, scale_test_events_filter = '', ''

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
            AND event_time IN {tuple_time_part}
            AND analytics_environment IN {tuple_env}
            AND event_category = '{event_category}'
            {scale_test_logs_filter}
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
            AND event_time IN {tuple_time_part}
            AND analytics_environment IN {tuple_env}
            AND event_category = '{event_category}'
            {scale_test_logs_filter}
            ) b
        ON a.batch_id = b.batch_id
        ;
        """.format(gcp=gcp, table_type=table_type, ds_start=ds_start, ds_stop=ds_stop, tuple_time_part=tuple_time_part,
                   tuple_env=tuple_env, event_category=event_category, scale_test_logs_filter=scale_test_logs_filter,
                   scale_test_events_filter=scale_test_events_filter)

        return query
