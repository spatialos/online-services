
def datesGenerator(ds_start, ds_stop):
    import datetime
    start = datetime.datetime.strptime(ds_start, '%Y-%m-%d')
    end = datetime.datetime.strptime(ds_stop, '%Y-%m-%d')
    step = datetime.timedelta(days = 1)
    while start <= end:
        yield str(start.date())
        start += step

def gcsFileListGenerator(datesGenerator, ds_start, ds_stop, gcs_bucket, list_env, event_category, list_time_part, scale_test_name = ''):
    for env in list_env:
        for ds in datesGenerator(ds_start, ds_stop):
            for time_part in list_time_part:
                yield 'gs://{gcs_bucket}/data_type=json/analytics_environment={env}/event_category={event_category}/event_ds={ds}/event_time={time_part}/{scale_test_name}'.format(
                  gcs_bucket = gcs_bucket, env = env, event_category = event_category, ds = ds, time_part = time_part, scale_test_name = scale_test_name)
