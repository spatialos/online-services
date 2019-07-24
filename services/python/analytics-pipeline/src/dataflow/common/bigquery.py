
def generate_bigquery_assets(client_bq, bigquery_asset_list):

    """ This function provisions all required BigQuery Datasets & Tables.
    We combine datasets & tables within a single function as creating tables
    requires the same references when creating datasets.
    """

    from common.bigquery_schema import bigquery_table_schema_dict
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

    # Create dataset if it does not exist..
    for dataset_name in set([bq_asset[0] for bq_asset in bigquery_asset_list]):
        dataset_ref = client_bq.dataset(dataset_name)
        if not dataset_exists(client=client_bq, dataset_reference=dataset_ref):
            dataset = bigquery.Dataset(dataset_ref)
            client_bq.create_dataset(dataset)

    # Create table if it does not exist..
    table_list = []
    for bq_asset in bigquery_asset_list:
        dataset_name, table_name, partition = bq_asset
        table_ref = client_bq.dataset(dataset_name).table(table_name)
        if not table_exists(client=client_bq, table_reference=table_ref):
            table = bigquery.Table(table_ref, schema=bigquery_table_schema_dict[dataset_name])
            if partition:
                table.time_partitioning = bigquery.TimePartitioning(
                  type_=bigquery.TimePartitioningType.DAY,
                  field=partition)
            table_list.append(client_bq.create_table(table))
        else:
            table_list.append(client_bq.get_table(table_ref))

    return table_list


def source_bigquery_assets(client_bq, bigquery_asset_list):

    """ This function sources BigQuery assets. If this operation fails,
    generate_bigquery_assets() should be invoked.
    """

    table_list = []
    for bq_asset in bigquery_asset_list:
        dataset_name, table_name, partition = bq_asset
        dataset_ref = client_bq.dataset(dataset_name)
        table_ref = dataset_ref.table(table_name)
        table_list.append(client_bq.get_table(table_ref))

    return table_list


def generate_backfill_query(gcp, method, ds_start, ds_stop, tuple_time_part, tuple_env, event_category, scale_test_name=''):

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
        FROM `{gcp}.logs.events_logs_{method}_native`
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
        FROM `{gcp}.events.events_{method}_native`
        WHERE analytics_environment IN {tuple_env}
        {scale_test_events_filter}
        UNION DISTINCT
        SELECT batch_id
        FROM `{gcp}.logs.events_debug_{method}_native`
        WHERE event_ds BETWEEN '{ds_start}' AND '{ds_stop}'
        AND event_time IN {tuple_time_part}
        AND analytics_environment IN {tuple_env}
        AND event_category = '{event_category}'
        {scale_test_logs_filter}
        ) b
    ON a.batch_id = b.batch_id
    ;
    """.format(gcp=gcp, method=method, ds_start=ds_start, ds_stop=ds_stop, tuple_time_part=tuple_time_part,
               tuple_env=tuple_env, event_category=event_category, scale_test_logs_filter=scale_test_logs_filter,
               scale_test_events_filter=scale_test_events_filter)

    return query
