import datetime
import json
import time

def generate_date_range(ds_start, ds_stop):
    start = datetime.datetime.strptime(ds_start, '%Y-%m-%d')
    end = datetime.datetime.strptime(ds_stop, '%Y-%m-%d')
    step = datetime.timedelta(days=1)
    while start <= end:
        yield str(start.date())
        start += step


def generate_gcs_file_list(datesGenerator, ds_start, ds_stop, gcs_bucket, list_env, event_category, list_time_part, scale_test_name=''):
    for env in list_env:
        for ds in generate_date_range(ds_start, ds_stop):
            for time_part in list_time_part:
                yield 'gs://{gcs_bucket}/data_type=json/analytics_environment={env}/event_category={event_category}/event_ds={ds}/event_time={time_part}/{scale_test_name}'.format(
                  gcs_bucket=gcs_bucket, env=env, event_category=event_category, ds=ds, time_part=time_part, scale_test_name=scale_test_name)


def parse_gcs_uri(path, key):
    try:
        value = path.split(key)[1].split('/')[0]
        if key == 'event_ds=':
            try:
                x = datetime.datetime.strptime(value, '%Y-%m-%d')
            except Exception:
                value = None
    except Exception:
        value = None
    return value


def parse_analytics_environment(environment):
    if environment in ['all', '']:
        list_env, name_env = ['testing', 'development', 'staging', 'production', 'live'], 'all-envs'
    else:
        list_env, name_env = [environment], environment
    return list_env, name_env


def parse_event_time(time_part):
    if time_part == 'all':
        list_time_part, name_time = ['0-8', '8-16', '16-24'], '{time_part}-times'.format(time_part=time_part)
    else:
        list_time_part, name_time = [time_part], time_part
    return list_time_part, name_time


def convert_list_to_sql_tuple(sql_filter_list):
    if isinstance(sql_filter_list, list):
        return str(sql_filter_list).replace('[', '(').replace(']', ')')
    else:
        raise Exception('listToSqlTuple did not receive a list!')


def parse_json(text):

    """ If our Analytics Cloud Endpoint is used, events will be written in Google Cloud Storage
    as newline delimited JSON files. If however the endpoint is changed, it might no longer
    write as newline delimited, but normal JSON. Hence we try to parse this as well before giving up.
    """

    # First, try to parse as newline delimited JSON:
    try:
        x = []
        for i in text.split('\n'):
            x.append(json.loads(i))
    except Exception:
        # Second, try to parse as JSON list or dict:
        try:
            x = json.loads(text)
        # Otherwise, fail:
        except Exception as e:
            x = 'Error: {e} -- The following string could not be parsed as JSON: {text}'.format(e=e, text=text)
    return x


def parse_dict_key(event_dict, option1, option2=''):
    value = event_dict.get(option1, event_dict.get(option2, None))
    return value


def verify_unix_timestamp(ts):

    """ This function ensures we will pass timestamps to BigQuery as either integers
    or floats (unix timestamps), or None otherwise, to avoid ingestion failures.
    """

    if isinstance(ts, float) or isinstance(ts, int):
        return ts

    ts_list = []
    if isinstance(ts, str):
        for i in ['%Y-%m-%dT%H:%M:%SZ', '%Y-%m-%d %H:%M:%S %Z']:
            try:
                d = datetime.datetime.strptime(ts, i)
                ts_list.append(time.mktime(d.timetuple()))
            except Exception:
                pass
    ts_list.append(None)
    return ts_list[0]


def cast_dict_to_json_string(element):
    if isinstance(element, dict):
        return json.dumps(element)
    else:
        return element
